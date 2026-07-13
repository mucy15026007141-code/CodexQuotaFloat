namespace CodexQuotaFloat.Models;

public enum RateLimitAvailability { Available, Unlimited, Unavailable }

public sealed record RateLimitWindow(int RemainingPercent, long? ResetsAt, int WindowDurationMins, RateLimitAvailability Availability = RateLimitAvailability.Available)
{
    public DateTimeOffset? ResetLocal => ResetsAt is > 0 and < 32503680000 ? DateTimeOffset.FromUnixTimeSeconds(ResetsAt.Value).ToLocalTime() : null;
    public static RateLimitWindow Unavailable(int duration) => new(0, null, duration, RateLimitAvailability.Unavailable);
    public static RateLimitWindow Unlimited(int duration) => new(0, null, duration, RateLimitAvailability.Unlimited);
}
