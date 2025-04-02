using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
        public string DeviceName { get; set; }
        public string DeviceId { get; set; }
    }

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class NavItemSend : Page
    {
        ObservableCollection<DeviceInfo> deviceInfoes = new ObservableCollection<DeviceInfo>();
        ObservableCollection<FileInfo> fileInfoes = new ObservableCollection<FileInfo>();

        WiFiDirectAdvertisementPublisher _publisher = new WiFiDirectAdvertisementPublisher();
        DeviceWatcher _deviceWatcher = null;
        bool _fWatcherStarted = false;
        public NavItemSend()
        {
            this.InitializeComponent();
            NearbyDevice.ItemsSource = deviceInfoes;
            BeginScanner();
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
                CloseButtonText = "取消"
            };
            deleteDialog.XamlRoot = this.Content.XamlRoot;
            var result = await deleteDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                fileInfoes.Remove(selectedFile);
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
                            fileName = "TEXT",
                            fileSize = the_text.Length,
                            fileType = FileType.TEXT,
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
                                    fileName = "TEXT",
                                    fileSize = textContent.Length,
                                    fileType = FileType.TEXT,
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
                _publisher.Start();
                if (_publisher.Status != WiFiDirectAdvertisementPublisherStatus.Started)
                {
                    Debug.WriteLine("广播失败");
                }

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
                _publisher.Stop();
                StopWatcher();
                Debug.WriteLine("停止扫描");
            }

        }

        private void NearbyDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NearbyDevice.SelectedItem is DeviceInfo selectedDevice)
            {
                System.Diagnostics.Debug.WriteLine($"选中设备：{selectedDevice.DeviceName} (ID: {selectedDevice.DeviceId})");
                _ = Connect(selectedDevice.DeviceId);
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
        private async void OnDeviceAdded(DeviceWatcher deviceWatcher, DeviceInformation deviceInfo)
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

        private async void OnDeviceRemoved(
            DeviceWatcher deviceWatcher,
            DeviceInformationUpdate deviceInfoUpdate
        )
        {


        }

        private async void OnDeviceUpdated(
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

        Windows.Devices.WiFiDirect.WiFiDirectDevice wfdDevice;

        private async System.Threading.Tasks.Task<String> Connect(string deviceId)
        {
            string result = "";

            try
            {
                // No device ID specified.
                if (String.IsNullOrEmpty(deviceId)) { return "Please specify a Wi-Fi Direct device ID."; }

                // Connect to the selected Wi-Fi Direct device.
                wfdDevice = await Windows.Devices.WiFiDirect.WiFiDirectDevice.FromIdAsync(deviceId);

                if (wfdDevice == null)
                {
                    result = "Connection to " + deviceId + " failed.";
                }

                // Register for connection status change notification.
                wfdDevice.ConnectionStatusChanged += new TypedEventHandler<Windows.Devices.WiFiDirect.WiFiDirectDevice, object>(OnConnectionChanged);

                // Get the EndpointPair information.
                var EndpointPairCollection = wfdDevice.GetConnectionEndpointPairs();

                if (EndpointPairCollection.Count > 0)
                {
                    var endpointPair = EndpointPairCollection[0];
                    result = "Local IP address " + endpointPair.LocalHostName.ToString() +
                        " connected to remote IP address " + endpointPair.RemoteHostName.ToString();
                    if (fileInfoes.Count != 0)
                    {
                        FileSender fileSender = new FileSender();
                        foreach (var fileInfo in fileInfoes)
                        {
                            fileSender.SendFile(fileInfo, endpointPair.RemoteHostName.ToString(), 27431);
                        }
                    }
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

        private void OnConnectionChanged(object sender, object arg)
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

        private void Disconnect()
        {
            if (wfdDevice != null)
            {
                wfdDevice.Dispose();
            }
        }
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
}
