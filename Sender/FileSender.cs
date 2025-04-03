using Microsoft.UI.Dispatching;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LocalDrop.Sender
{
    public class FileSender
    {
        public event Action<SendTransferItem> ProgressChanged;
        public event Action<SendTransferItem> TransferCompleted;
        public DispatcherQueue DispatcherQueue { get; set; }
        public async Task SendFileAsync(FileInfo fileInfo, string serverIp, int port, SendTransferItem transferItem)
        {
            try
            {
                string filePath = fileInfo.info;
                if (fileInfo.fileType != FileType.QUICK_MESSAGE)
                {// 验证文件路径
                    if (!File.Exists(filePath))
                        throw new FileNotFoundException("File not found", filePath);
                    // 获取实际文件信息
                    System.IO.FileInfo ioFile = new System.IO.FileInfo(filePath);
                    fileInfo.fileSize = ioFile.Length;
                }
                else
                {
                    fileInfo.fileSize = 0;
                }

                // 序列化元数据
                var metadata = new
                {
                    fileInfo.fileName,
                    fileInfo.fileSize,
                    fileType = fileInfo.fileType.ToString(),
                    fileInfo.info
                };

                string json = JsonConvert.SerializeObject(metadata);
                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

                // 处理长度头（网络字节序）
                byte[] lengthHeader = BitConverter.GetBytes(jsonBytes.Length);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(lengthHeader);

                using (TcpClient client = new TcpClient(serverIp, port))
                using (NetworkStream stream = client.GetStream())
                {
                    // 发送协议头
                    stream.Write(lengthHeader, 0, 4);

                    // 发送元数据
                    stream.Write(jsonBytes, 0, jsonBytes.Length);
                    if (fileInfo.fileType != FileType.QUICK_MESSAGE)
                    {
                        var totalSent = 0;
                        // 流式发送文件内容
                        using (FileStream fileStream = File.OpenRead(filePath))
                        {
                            byte[] buffer = new byte[8192];
                            int bytesRead;
                            while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                stream.Write(buffer, 0, bytesRead);
                                totalSent += bytesRead;
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    transferItem.BytesSent = totalSent;
                                    ProgressChanged?.Invoke(transferItem);
                                });
                            }
                        }
                    }
                }
                DispatcherQueue.TryEnqueue(() =>
                {
                    TransferCompleted?.Invoke(transferItem);
                });

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"发送错误: {ex.Message}");
                throw;
            }
        }
        public void SendFile(FileInfo fileInfo, string serverIp, int port)
        {
            // 验证文件路径
            string filePath = fileInfo.info;
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            // 获取实际文件信息
            System.IO.FileInfo ioFile = new System.IO.FileInfo(filePath);
            fileInfo.fileSize = ioFile.Length;

            // 序列化元数据
            var metadata = new
            {
                fileInfo.fileName,
                fileInfo.fileSize,
                fileType = fileInfo.fileType.ToString(),
                fileInfo.info
            };

            string json = JsonConvert.SerializeObject(metadata);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

            // 处理长度头（网络字节序）
            byte[] lengthHeader = BitConverter.GetBytes(jsonBytes.Length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthHeader);

            using (TcpClient client = new TcpClient(serverIp, port))
            using (NetworkStream stream = client.GetStream())
            {
                // 发送协议头
                stream.Write(lengthHeader, 0, 4);

                // 发送元数据
                stream.Write(jsonBytes, 0, jsonBytes.Length);

                // 流式发送文件内容
                using (FileStream fileStream = File.OpenRead(filePath))
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        stream.Write(buffer, 0, bytesRead);
                    }
                }
            }
        }
    }
}
