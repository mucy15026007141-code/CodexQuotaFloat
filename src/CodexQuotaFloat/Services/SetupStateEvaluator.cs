using System.Text.RegularExpressions;
using CodexQuotaFloat.Models;

namespace CodexQuotaFloat.Services;

public static class SetupStateEvaluator
{
    public static readonly Version MinimumSupportedVersion = new(0, 144, 1);

    public static SetupCheckResult Evaluate(string? cliPath, string? versionText, string? loginMode, bool hasUsableQuotaData, string? errorType = null)
    {
        if (string.IsNullOrWhiteSpace(cliPath)) return new(SetupStatus.CodexNotFound);
        var version = ParseVersion(versionText);
        if (version is not null && version < MinimumSupportedVersion) return new(SetupStatus.VersionTooOld, cliPath, versionText);
        if (!string.IsNullOrWhiteSpace(errorType)) return new(SetupStatus.ConnectionFailed, cliPath, versionText, errorType);
        if (string.Equals(loginMode, "apiKey", StringComparison.OrdinalIgnoreCase)) return new(SetupStatus.ApiKeyMode, cliPath, versionText);
        if (string.Equals(loginMode, "notLoggedIn", StringComparison.OrdinalIgnoreCase)) return new(SetupStatus.NotLoggedIn, cliPath, versionText);
        return hasUsableQuotaData ? new(SetupStatus.Ready, cliPath, versionText) : new(SetupStatus.IncompleteQuotaData, cliPath, versionText);
    }

    public static Version? ParseVersion(string? text)
    {
        var match = Regex.Match(text ?? string.Empty, @"\b(\d+)\.(\d+)\.(\d+)\b");
        return match.Success ? new Version(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value)) : null;
    }
}
