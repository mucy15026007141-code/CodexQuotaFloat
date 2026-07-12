namespace CodexQuotaFloat.Models;

public sealed class AppSettings
{
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public bool IsExpanded { get; set; }
    public bool IsTopmost { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool EnableNotifications { get; set; } = true;
    public string LastDisplayState { get; set; } = "Compact";
}
