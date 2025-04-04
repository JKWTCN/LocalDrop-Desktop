using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;


namespace LocalDrop
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private static string GetCurrentDirectory()
        {
            return System.AppDomain.CurrentDomain.BaseDirectory;
        }

        public static string GetCurrentFile(string filename)
        {
            return GetCurrentDirectory() + filename;
        }

        public MainWindow()
        {
            this.InitializeComponent();
            ContentFrame.Navigate(typeof(NavItemReceiver));
            this.AppWindow.SetIcon(GetCurrentFile("Assets\\LocalDrop.ico"));

            //ContentFrame.Navigate(typeof(NavItemSend));
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var selectedItem = (NavigationViewItem)args.SelectedItem;
            if ((string)selectedItem.Tag == "NavItemReceiver") ContentFrame.Navigate(typeof(NavItemReceiver));
            else if ((string)selectedItem.Tag == "NavItemSend") ContentFrame.Navigate(typeof(NavItemSend));
            else if ((string)selectedItem.Tag == "NavItemSetting") ContentFrame.Navigate(typeof(NavItemSetting));
        }


    }
}
