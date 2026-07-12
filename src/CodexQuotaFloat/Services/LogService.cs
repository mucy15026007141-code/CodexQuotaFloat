using System.IO;
namespace CodexQuotaFloat.Services;

public sealed class LogService
{
    private readonly string _path;
    public LogService()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexQuotaFloat", "Logs");
        Directory.CreateDirectory(dir); _path = Path.Combine(dir, "app.log");
    }
    public void Write(string message)
    {
        try { if (File.Exists(_path) && new FileInfo(_path).Length > 1_000_000) File.Move(_path, _path + ".1", true); File.AppendAllText(_path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}"); } catch { }
    }
    public string DirectoryPath => Path.GetDirectoryName(_path)!;
}
