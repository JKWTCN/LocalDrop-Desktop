using LocalDrop.Receiver;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Windows.Devices.Enumeration;
using Windows.Devices.WiFiDirect;
using Windows.Storage;


namespace LocalDrop
{
    public sealed partial class NavItemReceiver : Page
    {
        private int port = int.Parse(MySettings.ReadJsonToDictionary()["port"].ToString());
        WiFiDirectAdvertisementPublisher _publisher = new WiFiDirectAdvertisementPublisher();
        DeviceWatcher? _deviceWatcher = null;
        bool _fWatcherStarted = false;
        FileReceiver receiver = new FileReceiver();
        WiFiDirectConnectionListener connectionListener = new WiFiDirectConnectionListener();

        private ObservableCollection<FileTransferItem> ActiveTransfers { get; } = new ObservableCollection<FileTransferItem>();
        private SemaphoreSlim _dialogLock = new SemaphoreSlim(1);
        private bool _isDialogShowing;
        public NavItemReceiver()
        {
            this.InitializeComponent();
            LoadHistory();
            receiver.FileTransferStarted += OnFileTransferStarted;
            receiver.FileProgressChanged += OnFileProgressChanged;
            receiver.FileTransferCompleted += OnFileTransferCompleted;
            receiver.DispatcherQueue = this.DispatcherQueue;
            BeginBroadcast();
        }
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            CleanupResources();
        }
        private void CleanupResources()
        {
            Debug.WriteLine("清理资源");
            // 1. 停止WiFi广播
            if (_publisher.Status == WiFiDirectAdvertisementPublisherStatus.Started)
            {
                _publisher.Stop();
                Debug.WriteLine("已停止WiFi广播");
            }

            // 2. 停止设备监听
            StopWatcher();

            // 3. 停止文件接收器
            if (receiver != null)
            {
                receiver.FileTransferStarted -= OnFileTransferStarted;
                receiver.FileProgressChanged -= OnFileProgressChanged;
                receiver.FileTransferCompleted -= OnFileTransferCompleted;
                receiver.Stop();
                Debug.WriteLine("已停止文件接收服务");
            }

            // 4. 取消连接监听
            if (connectionListener != null)
            {
                connectionListener.ConnectionRequested -= ConnectionRequestedHandler;
            }

            // 5. 清理传输列表
            ActiveTransfers.Clear();

            // 6. 释放信号量
            _dialogLock?.Dispose();
        }
        private int _activeTransferCount = 0;
        private void OnFileTransferStarted(FileTransferItem transferItem)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _activeTransferCount++;
                ActiveTransfers.Add(transferItem);
                if (_activeTransferCount == 1)
                    ShowTransferDialog();
            });
        }

        private void OnFileProgressChanged(FileTransferItem transferItem)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var existing = ActiveTransfers.FirstOrDefault(t => t.FilePath == transferItem.FilePath);
                if (existing != null)
                {
                    existing.BytesReceived = transferItem.BytesReceived;
                }
            });
        }

        private void OnFileTransferCompleted(FileTransferItem transferItem)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var existing = ActiveTransfers.FirstOrDefault(t => t.FilePath == transferItem.FilePath);
                if (existing != null)
                {
                    _activeTransferCount--;
                    existing.Status = transferItem.Status;
                    if (transferItem.Status == TransferReceivingStatus.Completed)
                    {
                        ReceivedFiles.Insert(0, new FileHistoryItem
                        {
                            FileName = transferItem.FileName,
                            FilePath = transferItem.FilePath,
                            ReceivedTime = DateTime.Now
                        });
                        SaveHistory();
                    }

                    // 延迟移除已完成项
                    DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        ActiveTransfers.Remove(existing);
                        if (!ActiveTransfers.Any()) TransferDialog.Hide();
                    };
                    timer.Start();
                }
            });
        }

        private async void ShowTransferDialog()
        {
            await _dialogLock.WaitAsync();
            try
            {
                if (!_isDialogShowing)
                {
                    _isDialogShowing = true;
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await TransferDialog.ShowAsync();
                        _isDialogShowing = false;
                    });
                }
            }
            finally
            {
                _dialogLock.Release();
            }
        }


        private void BeginBroadcast()
        {
            if (_deviceWatcher == null)
            {
                _publisher.Advertisement.IsAutonomousGroupOwnerEnabled = true;
                _publisher.Advertisement.ListenStateDiscoverability =
                    WiFiDirectAdvertisementListenStateDiscoverability.Normal;
                Debug.WriteLine("开始扫描");
                _publisher.Start();
                if (_publisher.Status != WiFiDirectAdvertisementPublisherStatus.Started)
                {
                    Debug.WriteLine("扫描失败");
                }

                //AssociationEndpoint 关联的终结点。 这包括其他电脑、平板电脑和手机。
                //DeviceInterface 设备接口。
                WiFiDirectDeviceSelectorType wiFiDirectDeviceSelectorType = WiFiDirectDeviceSelectorType.AssociationEndpoint;
                //string deviceSelector = WiFiDirectDevice.GetDeviceSelector(wiFiDirectDeviceSelectorType);
                string deviceSelector = WiFiDirectDevice.GetDeviceSelector(wiFiDirectDeviceSelectorType);
                _deviceWatcher = DeviceInformation.CreateWatcher(
                    deviceSelector,
                    new string[] { "System.Devices.WiFiDirect.InformationElements" }
                );
                connectionListener.ConnectionRequested += ConnectionRequestedHandler;
            }
            else
            {
                _publisher.Stop();
                StopWatcher();
                Debug.WriteLine("停止广播");
            }

        }
        bool is_listen = false;
        private async void ConnectionRequestedHandler(
      WiFiDirectConnectionListener sender,
      WiFiDirectConnectionRequestedEventArgs args)
        {
            var request = args.GetConnectionRequest();
            try
            {
                var device = await WiFiDirectDevice.FromIdAsync(request.DeviceInformation.Id);
                if (device.ConnectionStatus == WiFiDirectConnectionStatus.Connected)
                {
                    var endpointPairs = device.GetConnectionEndpointPairs();
                    foreach (var pair in endpointPairs)
                    {
                        // 启动文件接收器
                        if (receiver.IsListening == false)
                        {
                            Debug.WriteLine($"LocalServiceName:{pair.LocalServiceName} RemoteServiceName:{pair.RemoteServiceName}");
                            Debug.WriteLine($"LocalHostName:{pair.LocalHostName} RemoteHostName:{pair.RemoteHostName}");

                            await receiver.StartAsync(port);

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"配对错误: {ex.Message}");
            }


        }

        public void Stop()
        {
            _publisher?.Stop();
            Console.WriteLine("已停止所有服务");
        }
        private void StopWatcher()
        {

            _deviceWatcher = null;
            connectionListener.ConnectionRequested -= ConnectionRequestedHandler;
        }



        private ObservableCollection<FileHistoryItem> ReceivedFiles { get; } = new ObservableCollection<FileHistoryItem>();
        private const string HistoryFileName = "receive_history.json";

        private void LoadHistory()
        {
            try
            {
                var localFolder = ApplicationData.Current.LocalFolder;
                var historyFile = Path.Combine(localFolder.Path, HistoryFileName);

                if (File.Exists(historyFile))
                {
                    var json = File.ReadAllText(historyFile);
                    var items = JsonConvert.DeserializeObject<FileHistoryItem[]>(json);

                    foreach (var item in items)
                    {
                        ReceivedFiles.Insert(0, item);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载历史记录失败: {ex.Message}");
            }
        }

        private void OnFileReceived(string filePath)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var historyItem = new FileHistoryItem
                {
                    FilePath = filePath,
                    ReceivedTime = DateTime.Now,
                    FileName = Path.GetFileName(filePath)
                };

                ReceivedFiles.Insert(0, historyItem);
                SaveHistory();
            });

        }

        private void SaveHistory()
        {
            try
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    var localFolder = ApplicationData.Current.LocalFolder;
                    var historyFile = Path.Combine(localFolder.Path, HistoryFileName);
                    var json = JsonConvert.SerializeObject(ReceivedFiles);
                    File.WriteAllText(historyFile, json);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存历史记录失败: {ex.Message}");
            }
        }

        private void HistoryItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is FileHistoryItem item)
            {
                try
                {
                    Process.Start("explorer.exe", $"/select,\"{item.FilePath}\\{item.FileName}\"");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"打开文件失败: {ex.Message}");
                }
            }
        }
        public class FileHistoryItem
        {
            public string FileName { get; set; }
            public string FilePath { get; set; }
            public DateTime ReceivedTime { get; set; }
        }
    }
}

