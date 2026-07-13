using CodexQuotaFloat.Models;

namespace CodexQuotaFloat.Services;

public static class DiagnosticInfo
{
    public static string Create(SetupCheckResult result) => string.Join(Environment.NewLine,
        "应用版本: 1.1.2",
        $"Windows: {Environment.OSVersion.VersionString}",
        $"Codex CLI版本: {result.CliVersion ?? "未检测到"}",
        $"Codex CLI路径: {result.CliPath ?? "未检测到"}",
        $"连接状态: {result.Status}",
        $"错误类型: {result.ErrorType ?? "无"}",
        $"时间: {DateTimeOffset.Now:O}");
}
