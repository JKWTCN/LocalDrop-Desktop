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
                Title = "ɾ��ȷ��",
                Content = $"ȷ��Ҫɾ�� {selectedFile.fileName} ��",
                PrimaryButtonText = "ɾ��",
                CloseButtonText = "ȡ��"
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
                        Debug.WriteLine($"����ļ�{selectedFile.Name},��С{fileSize},��ַ{selectedFile.Path}");
                    }
                }
                else if (button == SendText)
                {
                    //todo ���������ı�
                    var dialog = new ContentDialog
                    {
                        Title = "�ı�",
                        Content = new TextBox { AcceptsReturn = true },
                        PrimaryButtonText = "����",
                        CloseButtonText = "ȡ��",
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
                        Debug.WriteLine($"����ı���{the_text}");
                    }
                }
                else if (button == SendCut)
                {
                    try
                    {
                        DataPackageView dataPackageView = Clipboard.GetContent();
                        bool contentAdded = false;

                        // ������а��ļ�
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
                                    Debug.WriteLine($"��Ӽ��а��ļ���{file.Name}, ��ַ��{file.Path}");
                                    contentAdded = true;
                                }
                            }
                        }

                        // ������а��ı�
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
                                Debug.WriteLine($"��Ӽ��а��ı���{textContent}");
                                contentAdded = true;
                            }
                        }

                        // ��������ʾ
                        if (!contentAdded)
                        {
                            await ShowMessageDialog("���а���δ�ҵ��ļ����ı�����");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"���а�����쳣: {ex.Message}");
                        await ShowMessageDialog($"����ʧ��: {ex.Message}");
                    }
                }
            }
        }
        private async Task ShowMessageDialog(string message)
        {
            ContentDialog dialog = new ContentDialog()
            {
                Title = "��ʾ",
                Content = message,
                CloseButtonText = "ȷ��",
                XamlRoot = this.Content.XamlRoot // ȷ���Ի�����ȷ��ʾ
            };

            await dialog.ShowAsync();
        }


        private void BeginScanner()
        {
            if (_deviceWatcher == null)
            {
                Debug.WriteLine("��ʼ�㲥");
                _publisher.Start();
                if (_publisher.Status != WiFiDirectAdvertisementPublisherStatus.Started)
                {
                    Debug.WriteLine("�㲥ʧ��");
                }

                //AssociationEndpoint �������ս�㡣 ������������ԡ�ƽ����Ժ��ֻ���
                //DeviceInterface �豸�ӿڡ�
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
                Debug.WriteLine("ֹͣɨ��");
            }

        }

        private void NearbyDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NearbyDevice.SelectedItem is DeviceInfo selectedDevice)
            {
                System.Diagnostics.Debug.WriteLine($"ѡ���豸��{selectedDevice.DeviceName} (ID: {selectedDevice.DeviceId})");
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
                Debug.WriteLine($"�ɹ�����豸��{name} ({id})");

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
