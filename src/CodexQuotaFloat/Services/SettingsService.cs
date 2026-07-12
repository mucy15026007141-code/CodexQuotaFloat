using System.Text.Json;
using System.IO;
using CodexQuotaFloat.Models;

namespace CodexQuotaFloat.Services;

public sealed class SettingsService
{
    private readonly string _directory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexQuotaFloat");
    private string SettingsPath => System.IO.Path.Combine(_directory, "settings.json");
    public async Task<AppSettings> LoadAsync()
    {
        try { return JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(SettingsPath)) ?? new(); }
        catch (JsonException) { if (File.Exists(SettingsPath)) File.Move(SettingsPath, SettingsPath + ".corrupt-" + DateTime.UtcNow.Ticks, true); return new(); }
        catch (IOException) { return new(); }
    }
    public async Task SaveAsync(AppSettings settings)
    {
        Directory.CreateDirectory(_directory);
        var temp = SettingsPath + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, SettingsPath, true);
    }
}
