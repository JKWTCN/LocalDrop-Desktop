using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
namespace LocalDrop.Receiver
{

    public class FileReceiver
    {
        public event Action<FileTransferItem> FileTransferStarted;
        public event Action<FileTransferItem> FileProgressChanged;
        public event Action<FileTransferItem> FileTransferCompleted;

        private const int HeaderSize = 4; // 4字节长度头
        private StreamSocketListener _listener;
        private string _saveDirectory = MySettings.ReadJsonToDictionary()["save_path"].ToString();
        public bool IsListening => _listener != null;

        public string SaveDirectory
        {
            get => _saveDirectory;
            set
            {
                _saveDirectory = value;
                Directory.CreateDirectory(_saveDirectory); // 确保目录存在
            }
        }
        public void Stop()
        {
            _listener?.Dispose();
        }
        public async Task StartAsync(int port = 27431)
        {
            Debug.WriteLine($"开始监听端口{port}");
            _listener = new StreamSocketListener();
            _listener.ConnectionReceived += OnConnectionReceived;
            await _listener.BindServiceNameAsync(port.ToString());
        }

        private async void OnConnectionReceived(
            StreamSocketListener sender,
            StreamSocketListenerConnectionReceivedEventArgs args)
        {
            Debug.WriteLine("收到连接");
            using (var socket = args.Socket)
            {
                try
                {
                    await ProcessFileTransfer(socket);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"传输错误: {ex.Message}");
                }
            }
        }
        public DispatcherQueue DispatcherQueue { get; set; }


        private async Task ProcessFileTransfer(StreamSocket socket)
        {
            FileTransferItem transferItem = null;
            try
            {
                using (var inputStream = socket.InputStream)
                using (var reader = new DataReader(inputStream))
                {
                    Debug.WriteLine("开始读取流");
                    //reader.ByteOrder = ByteOrder.LittleEndian;

                    // 读取协议头
                    Debug.WriteLine("读取协议头");
                    await reader.LoadAsync(HeaderSize);
                    var jsonLength = reader.ReadInt32();

                    // 读取JSON元数据
                    Debug.WriteLine("JSON元数据");
                    await reader.LoadAsync((uint)jsonLength);
                    var jsonData = reader.ReadString((uint)jsonLength);
                    var fileInfo = JsonConvert.DeserializeObject<FileInfo>(jsonData);
                    Debug.WriteLine($"json:{jsonData}");
                    transferItem = new FileTransferItem
                    {
                        FileName = fileInfo.fileName,
                        FilePath = Path.Combine(SaveDirectory, fileInfo.info ?? fileInfo.fileName),
                        FileSize = fileInfo.fileSize,
                        StartTime = DateTime.Now,
                        Status = TransferReceivingStatus.Receiving
                    };

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        FileTransferStarted?.Invoke(transferItem);
                    });

                    // 验证文件信息
                    if (fileInfo == null || fileInfo.fileSize <= 0)
                    {
                        throw new InvalidDataException("无效的文件元数据");
                    }

                    // 构建保存路径
                    var savePath = Path.Combine(SaveDirectory, fileInfo.fileName);

                    // 确保目录存在
                    Directory.CreateDirectory(Path.GetDirectoryName(savePath));
                    if (fileInfo.fileType != FileType.QUICK_MESSAGE)
                    { // 接收文件数据
                        using (var fileStream = File.Create(savePath))
                        {
                            long remainingBytes = fileInfo.fileSize;
                            const uint bufferSize = 8192; // 8KB 缓冲区
                            while (remainingBytes > 0)
                            {
                                uint readSize = (uint)Math.Min(bufferSize, remainingBytes);
                                await reader.LoadAsync(readSize);
                                uint actualRead = reader.UnconsumedBufferLength;
                                byte[] data = new byte[actualRead];
                                reader.ReadBytes(data);
                                await fileStream.WriteAsync(data, 0, data.Length);
                                remainingBytes -= actualRead;
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    transferItem.BytesReceived += actualRead;
                                    FileProgressChanged?.Invoke(transferItem);
                                });

                            }
                            Debug.WriteLine($"接收成功,文件位置为：{savePath}");
                            // 传输完成后确保托盘图标状态
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                transferItem.Status = TransferReceivingStatus.Completed;
                                FileTransferCompleted?.Invoke(transferItem);

                                if (Window.Current is MainWindow mainWindow)
                                {
                                    mainWindow.EnsureTrayIconVisible();
                                }
                            });
                        }
                    }
                    else
                    {
                        using (var fileStream = File.Create(savePath))
                        {
                            using (var writer = new StreamWriter(fileStream, Encoding.UTF8))
                            {
                                await writer.WriteAsync(fileInfo.info);
                            }
                        }
                        Debug.WriteLine($"文本接收成功,文件位置为：{savePath}");
                    }
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        transferItem.Status = TransferReceivingStatus.Completed;
                        FileTransferCompleted?.Invoke(transferItem);
                    });

                }
            }
            catch (Exception ex)
            {
                transferItem.Status = TransferReceivingStatus.Failed;
                FileTransferCompleted?.Invoke(transferItem);
                Debug.WriteLine($"传输错误: {ex.Message}");
            }
        }

        // 文件接收完成事件
        public event Action<string> FileReceived;
    }

}