using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using CodexQuotaFloat.Models;

namespace CodexQuotaFloat.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
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
        settings.Left = NormalizeCoordinate(settings.Left);
        settings.Top = NormalizeCoordinate(settings.Top);
        Directory.CreateDirectory(_directory);
        var temp = SettingsPath + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(settings, SerializerOptions));
        File.Move(temp, SettingsPath, true);
    }

    public static string SerializeForTesting(AppSettings settings) => JsonSerializer.Serialize(settings, SerializerOptions);

    private static double NormalizeCoordinate(double value) => WindowPositionService.IsFinite(value) ? value : double.NaN;
}
