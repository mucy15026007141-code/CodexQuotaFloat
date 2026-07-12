using System.Diagnostics;
using System.Text.Json;
using CodexQuotaFloat.Infrastructure;
using CodexQuotaFloat.Models;

namespace CodexQuotaFloat.Services;

public sealed class CodexEnvironmentService
{
    private readonly CodexExecutableLocator _locator = new();

    public async Task<SetupCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var path = await _locator.FindAsync();
        if (path is null) return SetupStateEvaluator.Evaluate(null, null, null, false);
        var version = await GetVersionAsync(path, cancellationToken);
        if (SetupStateEvaluator.ParseVersion(version) is { } parsed && parsed < SetupStateEvaluator.MinimumSupportedVersion)
            return SetupStateEvaluator.Evaluate(path, version, null, false);

        try
        {
            await using var connection = new JsonRpcConnection(path);
            await connection.StartAsync();
            var account = await connection.RequestAsync("account/read", new { refreshToken = false }, cancellationToken);
            var mode = GetLoginMode(account);
            if (mode is not null) return SetupStateEvaluator.Evaluate(path, version, mode, false);
            var limits = await connection.RequestAsync("account/rateLimits/read", new { }, cancellationToken);
            var snapshot = UsageParser.Parse(limits);
            return SetupStateEvaluator.Evaluate(path, version, "chatgpt", snapshot.FiveHour is not null && snapshot.Weekly is not null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return SetupStateEvaluator.Evaluate(path, version, null, false, ex.GetType().Name); }
    }

    private static string? GetLoginMode(JsonElement account)
    {
        var serialized = account.GetRawText();
        if (serialized.Contains("apiKey", StringComparison.OrdinalIgnoreCase) || serialized.Contains("api_key", StringComparison.OrdinalIgnoreCase)) return "apiKey";
        if (serialized.Contains("notLoggedIn", StringComparison.OrdinalIgnoreCase) || serialized.Contains("loggedIn\":false", StringComparison.OrdinalIgnoreCase)) return "notLoggedIn";
        return null;
    }

    private static async Task<string?> GetVersionAsync(string executable, CancellationToken token)
    {
        var command = executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase);
        var info = command
            ? new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec")!, $"/d /s /c \"\"{executable}\" --version\"")
            : new ProcessStartInfo(executable, "--version");
        info.UseShellExecute = false; info.CreateNoWindow = true; info.RedirectStandardOutput = true;
        using var process = Process.Start(info);
        if (process is null) return null;
        var output = await process.StandardOutput.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        return output.Trim();
    }
}
