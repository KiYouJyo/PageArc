namespace PageArc.Services;

public static class EbookOpenErrorFormatter
{
    public static string Format(Exception exception, string? language)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var message = exception.Message ?? string.Empty;
        var isZh = language?.StartsWith("zh", StringComparison.OrdinalIgnoreCase) == true;
        var isJa = language?.StartsWith("ja", StringComparison.OrdinalIgnoreCase) == true;

        if (exception is DrmProtectedEbookException
            || message.Contains("DRM", StringComparison.OrdinalIgnoreCase)
            || message.Contains("encrypted", StringComparison.OrdinalIgnoreCase))
        {
            if (isZh) return "这本电子书受 DRM 或加密保护，PageArc 无法打开。";
            if (isJa) return "この電子書籍は DRM または暗号化で保護されているため、PageArc では開けません。";
            return "This ebook is DRM-protected or encrypted and cannot be opened by PageArc.";
        }

        if (exception is FileNotFoundException
            || message.Contains("No such file or directory", StringComparison.OrdinalIgnoreCase)
            || message.Contains("source.epub", StringComparison.OrdinalIgnoreCase))
        {
            if (isZh) return "PageArc 无法准备这本电子书的本地阅读缓存。请重试；如果问题仍然存在，可重新导入该书。";
            if (isJa) return "PageArc はこの電子書籍のローカル読書キャッシュを準備できませんでした。再試行し、改善しない場合は再インポートしてください。";
            return "PageArc could not prepare the local reading cache for this ebook. Retry, or re-import the book if the problem persists.";
        }

        if (message.Contains("calibre", StringComparison.OrdinalIgnoreCase)
            || message.Contains("convert", StringComparison.OrdinalIgnoreCase)
            || message.Contains("normaliz", StringComparison.OrdinalIgnoreCase))
        {
            if (isZh) return "无法将这本电子书转换为可阅读的本地副本。请确认本机转换组件可用，并检查文件是否损坏。";
            if (isJa) return "この電子書籍を読み取り可能なローカルコピーに変換できませんでした。変換コンポーネントとファイルの状態を確認してください。";
            return "PageArc could not convert this ebook into a readable local copy. Check the local conversion provider and the ebook file.";
        }

        if (message.Contains("Traceback", StringComparison.OrdinalIgnoreCase))
        {
            if (isZh) return "打开电子书时发生内部转换错误。详细诊断信息已写入 PageArc 日志。";
            if (isJa) return "電子書籍を開く際に内部変換エラーが発生しました。詳細は PageArc の診断ログに記録されています。";
            return "An internal conversion error occurred while opening this ebook. Details were written to the PageArc diagnostic log.";
        }

        return message;
    }
}
