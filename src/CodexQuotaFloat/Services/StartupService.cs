using Microsoft.Win32;

namespace CodexQuotaFloat.Services;

public sealed class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CodexQuotaFloat";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string value && value.Contains(Environment.ProcessPath ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true) ?? throw new InvalidOperationException("Unable to open the current-user startup registry key.");
        if (enabled) key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" --startup", RegistryValueKind.String);
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
