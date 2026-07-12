using System.Text.Json;
using CodexQuotaFloat.Models;

namespace CodexQuotaFloat.Services;

public static class UsageParser
{
    public static UsageSnapshot Parse(JsonElement rateResult, string? planType = null, DateTimeOffset? retrievedAt = null)
    {
        JsonElement bucket = default;
        if (rateResult.TryGetProperty("rateLimitsByLimitId", out var byId) && byId.ValueKind == JsonValueKind.Object && byId.TryGetProperty("codex", out var codex)) bucket = codex;
        else if (rateResult.TryGetProperty("rateLimits", out var legacy) && legacy.ValueKind == JsonValueKind.Object && legacy.TryGetProperty("limitId", out var id) && id.GetString() == "codex") bucket = legacy;
        RateLimitWindow? five = null, week = null;
        if (bucket.ValueKind == JsonValueKind.Object)
            foreach (var property in bucket.EnumerateObject())
                if (TryWindow(property.Value, out var window))
                {
                    if (window.WindowDurationMins == 300) five = window;
                    if (window.WindowDurationMins == 10080) week = window;
                }
        return new(five, week, planType, retrievedAt ?? DateTimeOffset.Now);
    }

    private static bool TryWindow(JsonElement item, out RateLimitWindow window)
    {
        window = null!;
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("windowDurationMins", out var duration) || !duration.TryGetInt32(out var mins)) return false;
        var used = item.TryGetProperty("usedPercent", out var usedValue) && usedValue.TryGetDouble(out var usedDouble) ? usedDouble : 100;
        var reset = item.TryGetProperty("resetsAt", out var resetValue) && resetValue.TryGetInt64(out var resetAt) ? resetAt : (long?)null;
        window = new((int)Math.Round(Math.Clamp(100 - used, 0, 100)), reset, mins);
        return true;
    }
}
