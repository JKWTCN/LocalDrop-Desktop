using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;

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

