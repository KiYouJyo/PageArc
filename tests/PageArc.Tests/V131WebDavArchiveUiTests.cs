using PageArc.Models;
using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class V131WebDavArchiveUiTests
{
    private const string UrbanPlanToolboxSourceSha = "249bbf99088e5edc92b9a6f9b7635ca777cf847e";

    [Fact]
    public void RestoreAndManageDialogs_CopyUrbanPlanToolboxArchivePickerContract()
    {
        var root = FindRepoRoot();
        var code = File.ReadAllText(Path.Combine(root, "Pages", "SettingsPage.xaml.cs"));

        Assert.Contains(UrbanPlanToolboxSourceSha, code, StringComparison.Ordinal);
        Assert.Contains("SelectionMode = ListViewSelectionMode.Single", code, StringComparison.Ordinal);
        Assert.Contains("MinWidth = 520", code, StringComparison.Ordinal);
        Assert.Contains("MaxHeight = 360", code, StringComparison.Ordinal);
        Assert.Contains("选择要恢复的云存档", code, StringComparison.Ordinal);
        Assert.Contains("WebDAV 云存档", code, StringComparison.Ordinal);
        Assert.Contains("PrimaryButtonText = LocalText(\"恢复\"", code, StringComparison.Ordinal);
        Assert.Contains("SecondaryButtonText = LocalText(\"删除\"", code, StringComparison.Ordinal);
        Assert.Contains("CloseButtonText = LocalText(\"关闭\"", code, StringComparison.Ordinal);
        Assert.Contains("DefaultButton = ContentDialogButton.Close", code, StringComparison.Ordinal);
        Assert.Contains("IsPrimaryButtonEnabled = false", code, StringComparison.Ordinal);
        Assert.Contains("IsSecondaryButtonEnabled = false", code, StringComparison.Ordinal);
        Assert.Contains("timestamp}   {version}   {FormatBytes(item.Size)}\\n{item.FileName}", code, StringComparison.Ordinal);
    }

    [Fact]
    public void WebDavSync_ShowsProgressAndChecksCloudBeforeUploading()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Pages", "SettingsPage.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "Pages", "SettingsPage.xaml.cs"));

        Assert.Contains("x:Name=\"WebDavSyncProgress\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ListArchivesAsync(settings, password)", code, StringComparison.Ordinal);
        Assert.Contains("ComputePackageContentHashAsync(localPath)", code, StringComparison.Ordinal);
        Assert.Contains("ComputePackageContentHashAsync(remotePath)", code, StringComparison.Ordinal);
        Assert.Contains("本地与云端没有差异，未上传新存档", code, StringComparison.Ordinal);

        var syncStart = code.IndexOf("private async void SyncWebDav_Click", StringComparison.Ordinal);
        var listIndex = code.IndexOf("ListArchivesAsync(settings, password)", syncStart, StringComparison.Ordinal);
        var firstUploadIndex = code.IndexOf("UploadArchiveAsync(", syncStart, StringComparison.Ordinal);
        Assert.True(syncStart >= 0 && listIndex > syncStart && firstUploadIndex > listIndex);
    }

    [Fact]
    public void ArchiveFilename_ContainsTimestampAndVersion()
    {
        var timestamp = new DateTimeOffset(2026, 9, 4, 1, 2, 3, TimeSpan.Zero);
        var fileName = WebDavArchiveItem.CreateFileName(timestamp, "1.3.1");

        Assert.Equal("PageArc-20260904T010203Z-v1.3.1.pagearcbackup", fileName);
        Assert.True(WebDavArchiveItem.TryParseFileName(fileName, out var parsedTime, out var version));
        Assert.Equal(timestamp, parsedTime);
        Assert.Equal("1.3.1", version);
    }

    [Fact]
    public void ArchiveListingParser_UsesUrbanPlanToolboxMetadataShapeAndSortOrder()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <d:multistatus xmlns:d="DAV:">
              <d:response>
                <d:href>/dav/PageArc/PageArc-20260903T120000Z-v1.3.pagearcbackup</d:href>
                <d:propstat><d:prop>
                  <d:displayname>PageArc-20260903T120000Z-v1.3.pagearcbackup</d:displayname>
                  <d:getcontentlength>4096</d:getcontentlength>
                  <d:getlastmodified>Thu, 03 Sep 2026 12:01:00 GMT</d:getlastmodified>
                </d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat>
              </d:response>
              <d:response>
                <d:href>/dav/PageArc/PageArc-20260904T080000Z-v1.3.1.pagearcbackup</d:href>
                <d:propstat><d:prop>
                  <d:displayname>PageArc-20260904T080000Z-v1.3.1.pagearcbackup</d:displayname>
                  <d:getcontentlength>8192</d:getcontentlength>
                  <d:getlastmodified>Fri, 04 Sep 2026 08:01:00 GMT</d:getlastmodified>
                </d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat>
              </d:response>
            </d:multistatus>
            """;

        var items = WebDavArchiveListingParser.Parse(xml);

        Assert.Equal(2, items.Count);
        Assert.Equal("1.3.1", items[0].AppVersion);
        Assert.Equal(8192, items[0].Size);
        Assert.Equal("PageArc-20260904T080000Z-v1.3.1.pagearcbackup", items[0].FileName);
        Assert.Equal("1.3", items[1].AppVersion);
    }

    [Fact]
    public async Task PackageContentHash_IgnoresExportTimestampButDetectsBookByteChanges()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pagearc-v131-diff-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var bookPath = Path.Combine(root, "book.epub");
            await File.WriteAllBytesAsync(bookPath, [1, 2, 3, 4]);

            var reading = new ReadingDataService(Path.Combine(root, "reading.json"));
            reading.Load();
            var book = new BookEntry
            {
                Id = "book-1",
                FilePath = bookPath,
                Format = "EPUB",
                Title = "Book",
                FileFingerprint = "stable-fingerprint"
            };
            var service = new ReadingBackupService();

            var first = Path.Combine(root, "first.pagearcbackup");
            var second = Path.Combine(root, "second.pagearcbackup");
            var changed = Path.Combine(root, "changed.pagearcbackup");

            await service.ExportPackageAsync(first, reading, [book]);
            await Task.Delay(20);
            await service.ExportPackageAsync(second, reading, [book]);

            var firstHash = await ReadingBackupService.ComputePackageContentHashAsync(first);
            var secondHash = await ReadingBackupService.ComputePackageContentHashAsync(second);
            Assert.Equal(firstHash, secondHash);

            await File.WriteAllBytesAsync(bookPath, [1, 2, 3, 9]);
            await service.ExportPackageAsync(changed, reading, [book]);
            var changedHash = await ReadingBackupService.ComputePackageContentHashAsync(changed);
            Assert.NotEqual(firstHash, changedHash);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static string FindRepoRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "PageArc.csproj"))) return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException("PageArc repository root not found.");
    }
}
