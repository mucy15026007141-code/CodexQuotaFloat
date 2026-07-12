namespace CodexQuotaFloat.Models;

public enum SetupStatus
{
    Ready,
    CodexNotFound,
    VersionTooOld,
    NotLoggedIn,
    ApiKeyMode,
    ConnectionFailed,
    IncompleteQuotaData
}

public sealed record SetupCheckResult(
    SetupStatus Status,
    string? CliPath = null,
    string? CliVersion = null,
    string? ErrorType = null)
{
    public bool IsReady => Status == SetupStatus.Ready;
}
