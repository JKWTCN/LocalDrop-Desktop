using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using Windows.Storage;
using Windows.Storage.Pickers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LocalDrop
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class NavItemSetting : Page
    {
        public NavItemSetting()
        {
            this.InitializeComponent();
            DispatcherQueue.TryEnqueue(() =>
            {
                SavePathTextBox.Text = MySettings.ReadJsonToDictionary()["save_path"].ToString();
            });
        }
        private async void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {

            FolderPicker picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            var window = new Microsoft.UI.Xaml.Window();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            StorageFolder folder = await picker.PickSingleFolderAsync();
            if (folder == null)
            {
                return; // 用户取消了选择
            }
            var settings = MySettings.ReadJsonToDictionary();
            settings["save_path"] = folder.Path;
            MySettings.SaveDictionaryToJson(settings);
            DispatcherQueue.TryEnqueue(() =>
            {
                SavePathTextBox.Text = folder.Path;
            });
        }

        private void GitHubButton_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/JKWTCN/LocalDrop-Desktop");
        }

        private void BilibiliButton_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://space.bilibili.com/283390377");
        }

        private void OpenUrl(string url)
        {

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

        }
    }
}

