namespace PageArc.Services;

public sealed class DrmProtectedEbookException : IOException
{
    public DrmProtectedEbookException(string message) : base(message) { }
    public DrmProtectedEbookException(string message, Exception innerException) : base(message, innerException) { }
}
