using H.NotifyIcon;
using H.NotifyIcon.Core;
using H.NotifyIcon.EfficiencyMode;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Windows.Input;


namespace LocalDrop
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {

        private bool is_human_close = false; // 是否人为关闭
        private static string GetCurrentDirectory()
        {
            return System.AppDomain.CurrentDomain.BaseDirectory;
        }

        public static string GetCurrentFile(string filename)
        {
            return GetCurrentDirectory() + filename;
        }


        public ICommand ShowWindowCommand => new RelayCommand(() =>
        {
            this.Show();
            this.Activate();
            TrayIcon.Visibility = Visibility.Collapsed;
        });
        public ICommand CloseWindowCommand => new RelayCommand(() =>
        {
            is_human_close = true;
            TrayIcon.Dispose(); // 必须释放资源
            Application.Current.Exit();
        });

        public MainWindow()
        {
            this.InitializeComponent();
            ContentFrame.Navigate(typeof(NavItemReceiver));
            this.AppWindow.SetIcon(GetCurrentFile("Assets\\LocalDrop.ico"));

            TrayIcon.ContextFlyout = TrayMenu;
            EfficiencyModeUtilities.SetEfficiencyMode(false);
            //this.TrayIcon.Visibility = Visibility.Visible;
            // 处理窗口关闭事件
            this.Closed += (sender, args) =>
            {
                bool default_close = Convert.ToBoolean((MySettings.ReadJsonToDictionary()["default_close"].ToString()));
                if (default_close || is_human_close)
                {
                    // 直接关闭应用
                    TrayIcon.Dispose();
                    Application.Current.Exit();
                }
                else
                {
                    // 隐藏窗口但保持托盘图标
                    args.Handled = true;
                    this.Hide();
                    TrayIcon.Visibility = Visibility.Visible;
                    ContentFrame.Navigate(typeof(NavItemReceiver));
                    //显示通知
                    TrayIcon.ShowNotification("应用程序仍在运行",
                        "已最小化到系统托盘",
                        NotificationIcon.Info);
                }
            };

            // 使用 Activated 事件
            this.Activated += OnWindowActivated;

        }
        public void EnsureTrayIconVisible()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (!this.Visible && TrayIcon != null)
                {
                    TrayIcon.Visibility = Visibility.Visible;
                }
            });
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var selectedItem = (NavigationViewItem)args.SelectedItem;
            if ((string)selectedItem.Tag == "NavItemReceiver") ContentFrame.Navigate(typeof(NavItemReceiver));
            else if ((string)selectedItem.Tag == "NavItemSend") ContentFrame.Navigate(typeof(NavItemSend));
            else if ((string)selectedItem.Tag == "NavItemSetting") ContentFrame.Navigate(typeof(NavItemSetting));
        }
        private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState != WindowActivationState.Deactivated)
            {
                TrayIcon.Visibility = Visibility.Visible;
            }
        }


    }
}
