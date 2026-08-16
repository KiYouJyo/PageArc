using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PageArc.Services;

namespace PageArc.Pages;

public sealed partial class ImportFoldersPage : Page
{
    public ImportFoldersPage() { InitializeComponent(); }

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = await PickerService.PickFolderAsync();
        if (string.IsNullOrWhiteSpace(folder)) return;
        var border = new Border { Style = (Style)Application.Current.Resources["PageArcCardStyle"] };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = Path.GetFileName(folder), FontWeight = FontWeights.SemiBold });
        stack.Children.Add(new TextBlock { Text = folder, Opacity = 0.62 });
        border.Child = stack;
        FoldersList.Children.Insert(Math.Max(0, FoldersList.Children.Count - 1), border);
    }
}
