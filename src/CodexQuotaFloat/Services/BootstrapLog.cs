using System.Diagnostics;

namespace CodexQuotaFloat.Services;

public static class BootstrapLog
{
    private static readonly string Path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexQuotaFloat", "Logs", "bootstrap.log");
    public static string? LastFailure { get; private set; }

    public static void Write(string step, string? detail = null)
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            using var process = Process.GetCurrentProcess();
            System.IO.File.AppendAllText(Path, $"{DateTimeOffset.Now:O}; pid={process.Id}; tid={Environment.CurrentManagedThreadId}; session={process.SessionId}; exe={Environment.ProcessPath}; args={Environment.CommandLine}; step={step}; detail={detail ?? ""}{Environment.NewLine}");
        }
        catch (Exception ex) { LastFailure = ex.GetType().Name + ": " + ex.Message; }
    }
}
