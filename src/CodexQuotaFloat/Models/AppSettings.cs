namespace CodexQuotaFloat.Models;

public sealed class AppSettings
{
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public bool IsExpanded { get; set; }
    public bool IsTopmost { get; set; } = true;
    public bool AvoidTaskbar { get; set; }
    public string? LastMonitorDeviceName { get; set; }
    public DateTimeOffset? LastSuccessfulRefresh { get; set; }
    public bool AlwaysOnTop { get => IsTopmost; set => IsTopmost = value; }
    public bool StartWithWindows { get; set; }
    public bool EnableNotifications { get; set; } = true;
    public string LastDisplayState { get; set; } = "Compact";
    public bool SetupCompleted { get; set; }
    public string? LastDetectedCodexVersion { get; set; }
    public DateTimeOffset? LastSetupCheckTime { get; set; }
}
