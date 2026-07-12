namespace CodexQuotaFloat.Models;

public sealed record RateLimitWindow(int RemainingPercent, long? ResetsAt, int WindowDurationMins)
{
    public DateTimeOffset? ResetLocal => ResetsAt is > 0 and < 32503680000 ? DateTimeOffset.FromUnixTimeSeconds(ResetsAt.Value).ToLocalTime() : null;
}
