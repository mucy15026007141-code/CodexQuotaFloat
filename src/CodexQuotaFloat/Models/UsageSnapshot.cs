namespace CodexQuotaFloat.Models;

public sealed record UsageSnapshot(RateLimitWindow? FiveHour, RateLimitWindow? Weekly, string? PlanType, DateTimeOffset RetrievedAt);
