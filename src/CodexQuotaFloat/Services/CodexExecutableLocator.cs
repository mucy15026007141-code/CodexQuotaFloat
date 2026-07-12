using System.Diagnostics;

namespace CodexQuotaFloat.Services;

public sealed class CodexExecutableLocator
{
    public async Task<string?> FindAsync()
    {
        var psi = new ProcessStartInfo("where.exe", "codex") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
        using var process = Process.Start(psi); if (process is null) return null;
        var lines = (await process.StandardOutput.ReadToEndAsync()).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        await process.WaitForExitAsync();
        return lines.FirstOrDefault(path => path.Contains("AppData\\Roaming\\npm", StringComparison.OrdinalIgnoreCase) && (path.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))) ?? lines.FirstOrDefault();
    }
}
