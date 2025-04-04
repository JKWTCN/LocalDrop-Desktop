using LocalDrop.Sender;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Enumeration;
using Windows.Devices.WiFiDirect;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LocalDrop
{

    public class DeviceInfo
    {
        public required string DeviceName { get; set; }
        public required string DeviceId { get; set; }
    }

    public static class FileTypeToIconConverter
    {
        public static Symbol Convert(FileType fileType)
        {
            return fileType switch
            {
                FileType.TEXT => Symbol.Document,
                FileType.IMG => Symbol.Pictures,
                FileType.FILE => Symbol.Document,
                FileType.AUDIO => Symbol.Audio,
                FileType.VIDEO => Symbol.Video,
                FileType.DIR => Symbol.Folder,
                FileType.QUICK_MESSAGE => Symbol.Message,
                _ => Symbol.Document
            };
        }
    }
    public static class FileSizeConverter
    {
        public static string Convert(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            while (bytes >= 1024 && order < sizes.Length - 1)
            {
                order++;
                bytes /= 1024;
            }
            return $"{bytes:0.##} {sizes[order]}";
        }
    }


    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class NavItemSend : Page
    {
        private ObservableCollection<SendTransferItem> ActiveSendTransfers { get; } = new();
        private double _totalProgress = 0;
        private string _totalProgressText = "";
        private int port = int.Parse(MySettings.ReadJsonToDictionary()["port"].ToString());

        public double TotalProgress
        {
            get => _totalProgress;
            set => SetProperty(ref _totalProgress, value);
        }

        public string TotalProgressText
        {
            get => _totalProgressText;
            set => SetProperty(ref _totalProgressText, value);
        }


        ObservableCollection<DeviceInfo> deviceInfoes = new ObservableCollection<DeviceInfo>();
        ObservableCollection<FileInfo> fileInfoes = new ObservableCollection<FileInfo>();

        WiFiDirectAdvertisementPublisher _publisher = new WiFiDirectAdvertisementPublisher();
        DeviceWatcher? _deviceWatcher;
        bool _fWatcherStarted = false;
        public NavItemSend()
        {
            this.InitializeComponent();
            deviceInfoes.Clear();
            NearbyDevice.ItemsSource = deviceInfoes;
            WaitSendFileListView.DragOver += OnDragOver;
            WaitSendFileListView.Drop += OnDrop;

            BeginScanner();
        }
        private async void OnDragOver(object sender, DragEventArgs e)
        {
            // 检查是否包含可接受的文件类型
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                // 获取拖动的文件信息
                var deferral = e.GetDeferral();
                try
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    if (items.Count > 0)
                    {
                        e.AcceptedOperation = DataPackageOperation.Copy;
                        e.DragUIOverride.Caption = "添加文件到发送列表";
                        e.DragUIOverride.IsGlyphVisible = true;
                    }
                }
                finally
                {
                    deferral.Complete();
                }
            }
        }

        private async void OnDrop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var deferral = e.GetDeferral();
                try
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    foreach (var item in items)
                    {
                        if (item is StorageFile file)
                        {
                            var properties = await file.GetBasicPropertiesAsync();
                            long fileSize = (long)properties.Size;

                            // 确保在UI线程更新集合
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                fileInfoes.Add(new FileInfo()
                                {
                                    fileName = file.Name,
                                    fileSize = fileSize,
                                    fileType = FileType.FILE,
                                    info = file.Path
                                });
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"拖放文件错误: {ex.Message}");
                    await ShowMessageDialog($"无法添加文件: {ex.Message}");
                }
                finally
                {
                    deferral.Complete();
                }
            }
        }
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            CleanupResources();
            base.OnNavigatedFrom(e);

        }
        private void CleanupResources()
        {
            Debug.WriteLine("开始清理发送资源");
            // 1. 停止设备扫描和广播
            if (_deviceWatcher != null)
            {
                StopWatcher(); // 调用现有的停止方法
            }
            if (_publisher.Status == WiFiDirectAdvertisementPublisherStatus.Started)
            {
                _publisher.Stop();
            }

            // 2. 断开WiFiDirect连接
            Disconnect();


            ActiveSendTransfers.Clear();

            // 3. 清理文件列表
            fileInfoes.Clear();


        }

        private async void WaitSendFileListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var selectedFile = e.ClickedItem as FileInfo;
            if (selectedFile == null) return;

            ContentDialog deleteDialog = new ContentDialog
            {
                Title = "删除确认",
                Content = $"确定要删除 {selectedFile.fileName} 吗？",
                PrimaryButtonText = "删除",
                SecondaryButtonText = "查看",
                CloseButtonText = "取消"
            };
            deleteDialog.XamlRoot = this.Content.XamlRoot;
            var result = await deleteDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                fileInfoes.Remove(selectedFile);
            }
            else if (result == ContentDialogResult.Secondary)
            {
                Process p = new Process();
                p.StartInfo.FileName = "explorer.exe";
                p.StartInfo.Arguments = selectedFile.info;
                p.Start();
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button == SendFile)
                {
                    var window = new Microsoft.UI.Xaml.Window();
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                    FileOpenPicker files = new FileOpenPicker();
                    files.SuggestedStartLocation = PickerLocationId.Desktop;
                    files.FileTypeFilter.Add("*");

                    WinRT.Interop.InitializeWithWindow.Initialize(files, hwnd);
                    var selectedFiles = await files.PickMultipleFilesAsync();
                    foreach (var selectedFile in selectedFiles)
                    {
                        var properties = await selectedFile.GetBasicPropertiesAsync();
                        long fileSize = (long)properties.Size;
                        fileInfoes.Add(new FileInfo()
                        {
                            fileName = selectedFile.Name,
                            fileSize = fileSize,
                            fileType = FileType.FILE,
                            info = selectedFile.Path,

                        });
                        Debug.WriteLine($"添加文件{selectedFile.Name},大小{fileSize},地址{selectedFile.Path}");
                    }
                }
                else if (button == ReScanner)
                {
                    deviceInfoes.Clear();
                }
                else if (button == SendText)
                {
                    //todo 弹窗输入文本
                    var dialog = new ContentDialog
                    {
                        Title = "文本",
                        Content = new TextBox { AcceptsReturn = true },
                        PrimaryButtonText = "发送",
                        CloseButtonText = "取消",
                        XamlRoot = this.Content.XamlRoot
                    };
                    if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        var textBox = dialog.Content as TextBox;
                        var the_text = textBox.Text;
                        fileInfoes.Add(new FileInfo()
                        {
                            fileName = "QUICK_MESSAGE.txt",
                            fileSize = the_text.Length,
                            fileType = FileType.QUICK_MESSAGE,
                            info = the_text,

                        });
                        Debug.WriteLine($"添加文本：{the_text}");
                    }
                }
                else if (button == SendCut)
                {
                    try
                    {
                        DataPackageView dataPackageView = Clipboard.GetContent();
                        bool contentAdded = false;

                        // 处理剪切板文件
                        if (dataPackageView.Contains(StandardDataFormats.StorageItems))
                        {
                            IReadOnlyList<IStorageItem> items = await dataPackageView.GetStorageItemsAsync();
                            foreach (var item in items)
                            {
                                if (item is StorageFile file)
                                {
                                    var properties = await file.GetBasicPropertiesAsync();
                                    long fileSize = (long)properties.Size;
                                    fileInfoes.Add(new FileInfo()
                                    {
                                        fileName = file.Name,
                                        fileSize = fileSize,
                                        fileType = FileType.FILE,
                                        info = file.Path,
                                    });
                                    Debug.WriteLine($"添加剪切板文件：{file.Name}, 地址：{file.Path}");
                                    contentAdded = true;
                                }
                            }
                        }

                        // 处理剪切板文本
                        if (dataPackageView.Contains(StandardDataFormats.Text))
                        {
                            string textContent = await dataPackageView.GetTextAsync();
                            if (!string.IsNullOrWhiteSpace(textContent))
                            {
                                fileInfoes.Add(new FileInfo()
                                {
                                    fileName = "QUICK_MESSAGE.txt",
                                    fileSize = textContent.Length,
                                    fileType = FileType.QUICK_MESSAGE,
                                    info = textContent,
                                });
                                Debug.WriteLine($"添加剪切板文本：{textContent}");
                                contentAdded = true;
                            }
                        }

                        // 无内容提示
                        if (!contentAdded)
                        {
                            await ShowMessageDialog("剪切板中未找到文件或文本内容");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"剪切板操作异常: {ex.Message}");
                        await ShowMessageDialog($"操作失败: {ex.Message}");
                    }
                }
            }
        }
        private async Task ShowMessageDialog(string message)
        {
            ContentDialog dialog = new ContentDialog()
            {
                Title = "提示",
                Content = message,
                CloseButtonText = "确定",
                XamlRoot = this.Content.XamlRoot // 确保对话框正确显示
            };

            await dialog.ShowAsync();
        }


        private void BeginScanner()
        {
            if (_deviceWatcher == null)
            {
                Debug.WriteLine("开始广播");
                _publisher.StatusChanged += PublicStatusChanged;
                _publisher.Start();
            }
            else
            {
                _publisher.Stop();
                StopWatcher();
                Debug.WriteLine("停止扫描");
            }

        }
        private async void PublicStatusChanged(WiFiDirectAdvertisementPublisher sender, WiFiDirectAdvertisementPublisherStatusChangedEventArgs args)
        {
            if (args.Status == WiFiDirectAdvertisementPublisherStatus.Started)
            {
                //AssociationEndpoint 关联的终结点。 这包括其他电脑、平板电脑和手机。
                //DeviceInterface 设备接口。
                WiFiDirectDeviceSelectorType wiFiDirectDeviceSelectorType = WiFiDirectDeviceSelectorType.AssociationEndpoint;
                string deviceSelector = WiFiDirectDevice.GetDeviceSelector(wiFiDirectDeviceSelectorType);
                _deviceWatcher = DeviceInformation.CreateWatcher(
                    deviceSelector,
                    new string[] { "System.Devices.WiFiDirect.InformationElements" }
                );
                _deviceWatcher.Added += OnDeviceAdded;
                _deviceWatcher.Removed += OnDeviceRemoved;
                _deviceWatcher.Updated += OnDeviceUpdated;
                _deviceWatcher.EnumerationCompleted += OnEnumerationCompleted;
                _deviceWatcher.Stopped += OnStopped;
                _deviceWatcher.Start();
                _fWatcherStarted = true;


            }
            else
            {
                Debug.WriteLine($"wifiDirect状态{args.Status}");
            }
        }

        private void NearbyDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NearbyDevice.SelectedItem is DeviceInfo selectedDevice)
            {
                System.Diagnostics.Debug.WriteLine($"选中设备：{selectedDevice.DeviceName} (ID: {selectedDevice.DeviceId})");
                if (fileInfoes.Count != 0)
                {
                    _ = Connect(selectedDevice.DeviceId);
                    NearbyDevice.SelectedIndex = -1;
                }
                else
                {
                    //todo 添加弹窗 请选择要发送的文件
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        ContentDialog dialog = new ContentDialog
                        {
                            Title = "文件未选择",
                            Content = "请先选择要发送的文件",
                            CloseButtonText = "确定",
                            XamlRoot = this.Content.XamlRoot // 确保在WinUI中正确设置XamlRoot
                        };

                        _ = dialog.ShowAsync();
                        NearbyDevice.SelectedIndex = -1;
                    });
                }

            }
        }
        private void StopWatcher()
        {
            _deviceWatcher.Added -= OnDeviceAdded;
            _deviceWatcher.Removed -= OnDeviceRemoved;
            _deviceWatcher.Updated -= OnDeviceUpdated;
            _deviceWatcher.EnumerationCompleted -= OnEnumerationCompleted;
            _deviceWatcher.Stopped -= OnStopped;
            _deviceWatcher.Stop();
            _deviceWatcher = null;
        }

        //#region DeviceWatcherEvents
        private void OnDeviceAdded(DeviceWatcher deviceWatcher, DeviceInformation deviceInfo)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var name = deviceInfo.Name;
                var id = deviceInfo.Id;
                deviceInfoes.Add(new DeviceInfo()
                {
                    DeviceId = id,
                    DeviceName = name,
                });
                Debug.WriteLine($"成功添加设备：{name} ({id})");

            });

        }

        private void OnDeviceRemoved(
            DeviceWatcher deviceWatcher,
            DeviceInformationUpdate deviceInfoUpdate
        )
        {


        }

        private void OnDeviceUpdated(
            DeviceWatcher deviceWatcher,
            DeviceInformationUpdate deviceInfoUpdate
        )
        {

        }

        private void OnEnumerationCompleted(DeviceWatcher deviceWatcher, object o)
        {

        }

        private void OnStopped(DeviceWatcher deviceWatcher, object o)
        {
        }
        //#endregion

        Windows.Devices.WiFiDirect.WiFiDirectDevice? wfdDevice;

        private async System.Threading.Tasks.Task<String> Connect(string deviceId)
        {
            string result = "";
            Debug.WriteLine("开始连接并发送文件");
            try
            {
                // No device ID specified.
                if (String.IsNullOrEmpty(deviceId))
                {
                    Debug.WriteLine("Please specify a Wi-Fi Direct device ID.");
                    return "Please specify a Wi-Fi Direct device ID.";
                }

                // Connect to the selected Wi-Fi Direct device.
                wfdDevice = await Windows.Devices.WiFiDirect.WiFiDirectDevice.FromIdAsync(deviceId);

                if (wfdDevice == null)
                {
                    result = "Connection to " + deviceId + " failed.";
                    Debug.WriteLine("Connection to " + deviceId + " failed.");
                }

                // Register for connection status change notification.
                wfdDevice.ConnectionStatusChanged += new TypedEventHandler<Windows.Devices.WiFiDirect.WiFiDirectDevice, object>(OnConnectionChanged);

                // Get the EndpointPair information.
                var EndpointPairCollection = wfdDevice.GetConnectionEndpointPairs();

                if (EndpointPairCollection.Count > 0)
                {
                    var endpointPair = EndpointPairCollection[0];
                    if (fileInfoes.Count != 0)
                    {
                        FileSender fileSender = new FileSender();
                        fileSender.ProgressChanged += OnSendProgressChanged;
                        fileSender.TransferCompleted += OnSendCompleted;
                        fileSender.DispatcherQueue = DispatcherQueue;
                        // 初始化发送任务
                        var transfers = fileInfoes.Select(f => new SendTransferItem
                        {
                            FileName = f.fileName,
                            FilePath = f.info,
                            FileSize = f.fileSize,
                            Status = TransferSendingStatus.Pending
                        }).ToList();

                        foreach (var transfer in transfers)
                        {
                            ActiveSendTransfers.Add(transfer);
                        }

                        // 显示进度弹窗
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            _ = SendProgressDialog.ShowAsync();
                        });
                        UpdateTotalProgress();

                        // 开始发送（异步）
                        _ = Task.Run(async () =>
                        {
                            foreach (var (fileInfo, transfer) in fileInfoes.Zip(transfers, (f, t) => (f, t)))
                            {
                                try
                                {
                                    DispatcherQueue.TryEnqueue(() =>
                                   {
                                       transfer.Status = TransferSendingStatus.Sending;
                                   });

                                    await fileSender.SendFileAsync(fileInfo,
                                        endpointPair.RemoteHostName.ToString(),
                                        port,
                                        transfer);

                                    DispatcherQueue.TryEnqueue(() =>
                                   {
                                       fileInfoes.Remove(fileInfo);
                                       transfer.Status = TransferSendingStatus.Completed;
                                   });
                                }
                                catch (Exception ex)
                                {
                                    DispatcherQueue.TryEnqueue(() =>
                                   {
                                       transfer.Status = TransferSendingStatus.Failed;
                                   });
                                    Debug.WriteLine($"发送失败: {ex.Message}");
                                }
                                finally
                                {
                                    UpdateTotalProgress();
                                }
                            }
                        });
                    }

                    //var endpointPair = EndpointPairCollection[0];
                    //result = "Local IP address " + endpointPair.LocalHostName.ToString() +
                    //    " connected to remote IP address " + endpointPair.RemoteHostName.ToString();
                    //if (fileInfoes.Count != 0)
                    //{
                    //    FileSender fileSender = new FileSender();
                    //    foreach (var fileInfo in fileInfoes)
                    //    {
                    //        DispatcherQueue.TryEnqueue(() =>
                    //        {
                    //            NowSendFileText.Text = $"正在发送{fileInfo.fileName}";
                    //        });
                    //        Debug.WriteLine($"开始发送{fileInfo.fileName}");
                    //        fileSender.SendFile(fileInfo, endpointPair.RemoteHostName.ToString(), 27431);
                    //        DispatcherQueue.TryEnqueue(() =>
                    //        {
                    //            NowSendFileText.Text = $"传输完成{fileInfo.fileName}，等待发送下一个文件。";
                    //            fileInfoes.Remove(fileInfo);

                    //        });
                    //    }
                    //    DispatcherQueue.TryEnqueue(() =>
                    //    {
                    //        NowSendFileText.Text = $"发送列表";
                    //    });

                    //}
                }
                else
                {
                    result = "Connection to " + deviceId + " failed.";
                }
            }

            catch (Exception err)
            {
                // Handle error.
                result = "Error occurred: " + err.Message;
            }
            Debug.WriteLine(result);
            return result;
        }
        private void OnSendProgressChanged(SendTransferItem transferItem)
        {
            UpdateTotalProgress();
        }

        private void OnSendCompleted(SendTransferItem transferItem)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (ActiveSendTransfers.All(t => t.Status == TransferSendingStatus.Completed ||
                                               t.Status == TransferSendingStatus.Failed))
                {
                    SendProgressDialog.Hide();
                }
            });
        }

        private void UpdateTotalProgress()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (ActiveSendTransfers.Count == 0) return;

                var totalSize = ActiveSendTransfers.Sum(t => t.FileSize);
                var sentSize = ActiveSendTransfers.Sum(t => t.BytesSent);
                TotalProgress = (sentSize / (double)totalSize) * 100;
                TotalProgressText = $"{TotalProgress:0.0}% ({sentSize:N0}/{totalSize:N0} bytes)";
            });
        }

        private void OnSendDialogClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            if (ActiveSendTransfers.Any(t => t.Status == TransferSendingStatus.Sending))
            {
                args.Cancel = true;
                SendProgressDialog.Hide();
            }
        }

        // 添加INotifyPropertyChanged支持
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnConnectionChanged(object sender, object arg)
        {
            if (arg != null)
            {
                Windows.Devices.WiFiDirect.WiFiDirectConnectionStatus status =
                      (Windows.Devices.WiFiDirect.WiFiDirectConnectionStatus)arg;

                if (status == Windows.Devices.WiFiDirect.WiFiDirectConnectionStatus.Connected)
                {
                    // Connection successful.
                    Debug.WriteLine($"Connected: {status}");
                }
                else
                {
                    // Disconnected.
                    Disconnect();
                }
            }
        }

        private void Disconnect()
        {
            if (wfdDevice != null)
            {
                wfdDevice.Dispose();
            }
        }
    }

}
