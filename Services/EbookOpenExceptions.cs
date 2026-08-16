namespace PageArc.Services;

public sealed class DrmProtectedEbookException : InvalidDataException
{
    public DrmProtectedEbookException(string message) : base(message) { }
    public DrmProtectedEbookException(string message, Exception innerException) : base(message, innerException) { }
}
