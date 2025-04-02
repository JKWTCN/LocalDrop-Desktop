using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace LocalDrop
{
    public static class DialogHelper
    {

        public static async Task ShowMessageAsync(string title, string content, XamlRoot xamlRoot)
        {
            ContentDialog dialog = new ContentDialog();
            dialog.Title = title;
            dialog.Content = content;
            dialog.CloseButtonText = "OK";
            dialog.XamlRoot = xamlRoot;
            await dialog.ShowAsync();
        }
    }
}
