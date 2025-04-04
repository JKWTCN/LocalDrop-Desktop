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
using Windows.Foundation;


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
            CleanupResources();
            base.OnNavigatedFrom(e);

        }
        private void CleanupResources()
        {
            Debug.WriteLine("���������Դ");
            // 1. ֹͣWiFi�㲥
            if (_publisher.Status == WiFiDirectAdvertisementPublisherStatus.Started)
            {
                _publisher.Stop();
                Debug.WriteLine("��ֹͣWiFi�㲥");
            }

            // 2. ֹͣ�豸����
            StopWatcher();

            // 3. ֹͣ�ļ�������
            if (receiver != null)
            {
                receiver.FileTransferStarted -= OnFileTransferStarted;
                receiver.FileProgressChanged -= OnFileProgressChanged;
                receiver.FileTransferCompleted -= OnFileTransferCompleted;
                receiver.Stop();
                Debug.WriteLine("��ֹͣ�ļ����շ���");
            }

            // 4. ȡ�����Ӽ���
            if (connectionListener != null)
            {
                connectionListener.ConnectionRequested -= ConnectionRequestedHandler;
            }

            // 5. �������б�
            ActiveTransfers.Clear();

            // 6. �ͷ��ź���
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

                    // �ӳ��Ƴ��������
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
                Debug.WriteLine("��ʼ�㲥");
                _publisher.StatusChanged += PublicStatusChanged;
                _publisher.Start();
            }
            else
            {
                _publisher.Stop();
                StopWatcher();
                Debug.WriteLine("ֹͣ�㲥");
            }

        }

        private async void PublicStatusChanged(WiFiDirectAdvertisementPublisher sender, WiFiDirectAdvertisementPublisherStatusChangedEventArgs args)
        {
            if (args.Status == WiFiDirectAdvertisementPublisherStatus.Started)
            {
                Debug.WriteLine("wifiDirect��������");
                //AssociationEndpoint �������ս�㡣 ������������ԡ�ƽ����Ժ��ֻ���
                //DeviceInterface �豸�ӿڡ�
                //WiFiDirectDeviceSelectorType wiFiDirectDeviceSelectorType = WiFiDirectDeviceSelectorType.AssociationEndpoint;
                //string deviceSelector = WiFiDirectDevice.GetDeviceSelector(wiFiDirectDeviceSelectorType);
                //_deviceWatcher = DeviceInformation.CreateWatcher(
                //    deviceSelector,
                //    new string[] { "System.Devices.WiFiDirect.InformationElements" }
                //);
                //WiFiDirectConnectionListener connectionListener = new WiFiDirectConnectionListener();
                connectionListener.ConnectionRequested += ConnectionRequestedHandler;
                //�����ļ�������
                if (receiver.IsListening == false)
                {
                    await receiver.StartAsync(port);
                }
            }
            else
            {
                Debug.WriteLine($"wifiDirect״̬{args.Status}");
            }
        }
        bool is_listen = false;
        private async void ConnectionRequestedHandler(
      WiFiDirectConnectionListener sender,
      WiFiDirectConnectionRequestedEventArgs args)
        {
            Debug.WriteLine("����������");
            var request = args.GetConnectionRequest();
            var device = await WiFiDirectDevice.FromIdAsync(request.DeviceInformation.Id);
            device.ConnectionStatusChanged += new TypedEventHandler<Windows.Devices.WiFiDirect.WiFiDirectDevice, object>(OnConnectionChanged);
            //try
            //{
            //    if (device == null)
            //        return;
            //    if (device.ConnectionStatus == WiFiDirectConnectionStatus.Connected)
            //    {
            //    }
            //}
            //catch (Exception ex)
            //{
            //    Debug.WriteLine($"��Դ���: {ex.Message}");
            //}
        }

        private void OnConnectionChanged(WiFiDirectDevice device, object arg)
        {
            Windows.Devices.WiFiDirect.WiFiDirectConnectionStatus status =
                (Windows.Devices.WiFiDirect.WiFiDirectConnectionStatus)arg;

            if (status == Windows.Devices.WiFiDirect.WiFiDirectConnectionStatus.Connected)
            {
                var endpointPairs = device.GetConnectionEndpointPairs();
                foreach (var pair in endpointPairs)
                {
                    Debug.WriteLine($"LocalServiceName:{pair.LocalServiceName} RemoteServiceName:{pair.RemoteServiceName}");
                    Debug.WriteLine($"LocalHostName:{pair.LocalHostName} RemoteHostName:{pair.RemoteHostName}");
                }
            }
            else
            {
                Debug.WriteLine("���ʧ��");
            }
        }


        public void Stop()
        {
            _publisher?.Stop();
            Console.WriteLine("��ֹͣ���з���");
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
                var localFolder = Environment.CurrentDirectory;
                var historyFile = Path.Combine(localFolder, HistoryFileName);

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
                Debug.WriteLine($"������ʷ��¼ʧ��: {ex.Message}");
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
            });
            SaveHistory();
            // ȷ�������ں�����ͼ��״̬��ȷ
            if (Window.Current is MainWindow mainWindow)
            {
                mainWindow.EnsureTrayIconVisible();
            }

        }

        private void SaveHistory()
        {
            try
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    var localFolder = Environment.CurrentDirectory;
                    var historyFile = Path.Combine(localFolder, HistoryFileName);
                    var json = JsonConvert.SerializeObject(ReceivedFiles);
                    File.WriteAllText(historyFile, json);
                    if (Window.Current is MainWindow mainWindow)
                    {
                        mainWindow.EnsureTrayIconVisible();
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"������ʷ��¼ʧ��: {ex.Message}");
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
                    Debug.WriteLine($"���ļ�ʧ��: {ex.Message}");
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

