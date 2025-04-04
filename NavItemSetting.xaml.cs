using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
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


        public static bool IsRunAsAdmin()
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        public void SetStartup(bool enable)
        {
            RegistryKey rk = Registry.CurrentUser.OpenSubKey(
                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (enable)
            {
                // 获取应用的完整路径
                string appPath = Process.GetCurrentProcess().MainModule.FileName;
                rk.SetValue("YourAppName", appPath);
            }
            else
            {
                rk.DeleteValue("YourAppName", false);
            }
        }

        //private void cb_start_open_quiet_Checked(object sender, RoutedEventArgs e)
        //{
        //    if (cb_start_open_quiet.FocusState != FocusState.Unfocused)
        //    {
        //        DispatcherQueue.TryEnqueue(() =>
        //        {
        //            var settings = MySettings.ReadJsonToDictionary();
        //            settings["start_open_quiet"] = true;
        //            MySettings.SaveDictionaryToJson(settings);
        //        });

        //    }
        //}
        private void MinimizeToTrayToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (MinimizeToTrayToggle.FocusState != FocusState.Unfocused)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    var settings = MySettings.ReadJsonToDictionary();
                    settings["default_close"] = !MinimizeToTrayToggle.IsChecked;
                    MySettings.SaveDictionaryToJson(settings);
                });

            }
        }
        private void cb_start_open_Checked(object sender, RoutedEventArgs e)
        {
            if (cb_start_open.FocusState != FocusState.Unfocused)
                if (IsRunAsAdmin())
                {
                    DispatcherQueue.TryEnqueue(() =>
                {
                    var settings = MySettings.ReadJsonToDictionary();
                    settings["start_open"] = true;
                    MySettings.SaveDictionaryToJson(settings);
                    //cb_start_open_quiet.IsEnabled = true;
                    SetStartup(true);
                });
                }
                else
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        DialogHelper.ShowMessageAsync("提示", "请以管理员身份重新启动", this.XamlRoot);
                        cb_start_open.IsChecked = false;
                    });

                }
        }



        //private void cb_start_open_quiet_Unchecked(object sender, RoutedEventArgs e)
        //{
        //    if (cb_start_open_quiet.FocusState != FocusState.Unfocused)
        //        DispatcherQueue.TryEnqueue(() =>
        //        {
        //            var settings = MySettings.ReadJsonToDictionary();
        //            settings["start_open_quiet"] = false;
        //            MySettings.SaveDictionaryToJson(settings);
        //        });
        //}
        private void MinimizeToTrayToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (MinimizeToTrayToggle.FocusState != FocusState.Unfocused)
                DispatcherQueue.TryEnqueue(() =>
                {
                    var settings = MySettings.ReadJsonToDictionary();
                    settings["default_close"] = !MinimizeToTrayToggle.IsChecked;
                    MySettings.SaveDictionaryToJson(settings);
                });

        }
        private void cb_start_open_Unchecked(object sender, RoutedEventArgs e)
        {
            if (cb_start_open.FocusState != FocusState.Unfocused)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    var settings = MySettings.ReadJsonToDictionary();
                    settings["start_open"] = false;
                    MySettings.SaveDictionaryToJson(settings);
                    //cb_start_open_quiet.IsEnabled = false;
                    SetStartup(false);
                });

            }
        }
        public NavItemSetting()
        {
            this.InitializeComponent();
            DispatcherQueue.TryEnqueue(() =>
            {
                SavePathTextBox.Text = MySettings.ReadJsonToDictionary()["save_path"].ToString();
                SocketTextBox.Text = MySettings.ReadJsonToDictionary()["port"].ToString();
                MinimizeToTrayToggle.IsChecked = !Convert.ToBoolean(MySettings.ReadJsonToDictionary()["default_close"].ToString());
                cb_start_open.IsChecked = Convert.ToBoolean(MySettings.ReadJsonToDictionary()["start_open"].ToString());
                if (IsRunAsAdmin())
                {
                    cb_start_open.IsEnabled = true;
                }
                else
                {
                    cb_start_open.IsEnabled = false;
                }
                //if ((bool)cb_start_open.IsChecked)
                //{
                //    cb_start_open_quiet.IsEnabled = true;
                //}
                //else
                //{
                //    cb_start_open_quiet.IsEnabled = false;

                //}
                //cb_start_open_quiet.IsChecked = Convert.ToBoolean(MySettings.ReadJsonToDictionary()["start_open_quiet"].ToString());
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
        private void SocketButton_Click(object sender, RoutedEventArgs e)
        {
            var settings = MySettings.ReadJsonToDictionary();
            settings["port"] = SocketTextBox.Text;
            MySettings.SaveDictionaryToJson(settings);
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

