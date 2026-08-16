using PageArc.Models;
using Windows.UI.StartScreen;

namespace PageArc.Services;

public sealed class JumpListService
{
    private const int MaxRecentBooks = 8;

    public async Task RecordRecentBookAsync(BookEntry book)
    {
        ArgumentNullException.ThrowIfNull(book);
        if (string.IsNullOrWhiteSpace(book.Id) || string.IsNullOrWhiteSpace(book.Title)) return;

        try
        {
            if (!JumpList.IsSupported()) return;
            var jumpList = await JumpList.LoadCurrentAsync();
            jumpList.SystemGroupKind = JumpListSystemGroupKind.None;
            var arguments = AppActivationRequestParser.CreateBookUri(book.Id).AbsoluteUri;

            for (var index = jumpList.Items.Count - 1; index >= 0; index--)
            {
                if (string.Equals(jumpList.Items[index].Arguments, arguments, StringComparison.OrdinalIgnoreCase))
                    jumpList.Items.RemoveAt(index);
            }

            var item = JumpListItem.CreateWithArguments(arguments, book.Title);
            item.GroupName = "PageArc";
            jumpList.Items.Insert(0, item);
            while (jumpList.Items.Count > MaxRecentBooks)
                jumpList.Items.RemoveAt(jumpList.Items.Count - 1);
            await jumpList.SaveAsync();
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Windows jump list update was unavailable; reading continues normally.", ex);
        }
    }
}
