namespace PageArc.Services;

public static class DistributionChannel
{
#if PAGEARC_STORE
    public static bool IsStore => true;
    public static string Name => "Microsoft Store";
#else
    public static bool IsStore => false;
    public static string Name => "GitHub Releases";
#endif
}
