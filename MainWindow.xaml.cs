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

        private bool is_human_close = false; // �Ƿ���Ϊ�ر�
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
            TrayIcon.Dispose(); // �����ͷ���Դ
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
            // �����ڹر��¼�
            this.Closed += (sender, args) =>
            {
                bool default_close = Convert.ToBoolean((MySettings.ReadJsonToDictionary()["default_close"].ToString()));
                if (default_close || is_human_close)
                {
                    // ֱ�ӹر�Ӧ��
                    TrayIcon.Dispose();
                    Application.Current.Exit();
                }
                else
                {
                    // ���ش��ڵ���������ͼ��
                    args.Handled = true;
                    this.Hide();
                    TrayIcon.Visibility = Visibility.Visible;
                    ContentFrame.Navigate(typeof(NavItemReceiver));
                    //��ʾ֪ͨ
                    TrayIcon.ShowNotification("Ӧ�ó�����������",
                        "����С����ϵͳ����",
                        NotificationIcon.Info);
                }
            };

            // ʹ�� Activated �¼�
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
