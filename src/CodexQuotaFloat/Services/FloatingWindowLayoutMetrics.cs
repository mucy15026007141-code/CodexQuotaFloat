namespace CodexQuotaFloat.Services;

public static class FloatingWindowLayoutMetrics
{
    public const double Width = 340;
    public const double CompactHeight = 46;
    public const double MinimumExpandedHeight = 262;
    public const double ActionButtonHeight = 34;
    public const double MinimumActionBarBottomInset = 12;

    public static double ResolveExpandedHeight(double desiredHeight) =>
        Math.Max(MinimumExpandedHeight, Math.Ceiling(desiredHeight));

    public static double ActionBarBottomInset(double windowHeight, double actionBarBottom) =>
        windowHeight - actionBarBottom;
}
