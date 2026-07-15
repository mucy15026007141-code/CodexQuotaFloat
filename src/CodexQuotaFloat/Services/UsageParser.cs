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
        var unknown = new List<int>();
        if (bucket.ValueKind == JsonValueKind.Object)
            foreach (var item in EnumerateObjects(bucket))
                if (TryWindow(item, out var window))
                {
                    if (window.WindowDurationMins == 300) five = window;
                    else if (window.WindowDurationMins == 10080) week = window;
                    else unknown.Add(window.WindowDurationMins);
                }

        var weeklyAvailable = week?.Availability == RateLimitAvailability.Available;
        var fiveResult = five ?? (weeklyAvailable ? RateLimitWindow.Unlimited(300) : RateLimitWindow.Unavailable(300));
        var weekResult = week ?? RateLimitWindow.Unavailable(10080);
        var supported = bucket.ValueKind == JsonValueKind.Object &&
            (fiveResult.Availability is RateLimitAvailability.Available or RateLimitAvailability.Unlimited || weekResult.Availability == RateLimitAvailability.Available);
        return new(fiveResult, weekResult, planType, retrievedAt ?? DateTimeOffset.Now, bucket.ValueKind == JsonValueKind.Object, supported, unknown.Distinct().ToArray(), FindBankedResetCount(rateResult));
    }

    private static IEnumerable<JsonElement> EnumerateObjects(JsonElement root)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object) continue;
            yield return property.Value;
            foreach (var child in EnumerateObjects(property.Value)) yield return child;
        }
    }

    private static bool TryWindow(JsonElement item, out RateLimitWindow window)
    {
        window = null!;
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("windowDurationMins", out var duration) || !duration.TryGetInt32(out var mins)) return false;
        if (!item.TryGetProperty("usedPercent", out var usedValue) || !usedValue.TryGetDouble(out var usedDouble))
        {
            window = new(0, null, mins, RateLimitAvailability.Unavailable);
            return true;
        }
        var reset = item.TryGetProperty("resetsAt", out var resetValue) && resetValue.TryGetInt64(out var resetAt) ? resetAt : (long?)null;
        window = new((int)Math.Round(Math.Clamp(100 - usedDouble, 0, 100)), reset, mins);
        return true;
    }

    // This field is deliberately optional: an unknown or malformed upstream value must never fail quota parsing.
    private static int? FindBankedResetCount(JsonElement root) => FindBankedResetCount(root, resetContext: false);

    private static int? FindBankedResetCount(JsonElement element, bool resetContext)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindBankedResetCount(item, resetContext);
                if (nested is not null) return nested;
            }
            return null;
        }
        if (element.ValueKind != JsonValueKind.Object) return null;

        foreach (var property in element.EnumerateObject())
        {
            var name = Normalize(property.Name);
            var nextContext = resetContext || name.Contains("banked") || name.Contains("resetcredit") || name.Contains("ratelimitreset") || name.Contains("redeem");
            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var count) && count >= 0 &&
                (name is "bankedresetcount" or "bankedresets" or "availableresets" or "resetcredits" or "ratelimitresets" or "redeemableresets" or "bankedusageresetcount" ||
                 nextContext && name is "availablecount" or "count"))
                return count;

            var nested = FindBankedResetCount(property.Value, nextContext);
            if (nested is not null) return nested;
        }
        return null;
    }

    private static string Normalize(string name) => string.Concat(name.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    private static IEnumerable<JsonProperty> EnumerateProperties(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object)
            foreach (var property in root.EnumerateObject())
            {
                yield return property;
                foreach (var nested in EnumerateProperties(property.Value)) yield return nested;
            }
        else if (root.ValueKind == JsonValueKind.Array)
            foreach (var item in root.EnumerateArray())
                foreach (var nested in EnumerateProperties(item)) yield return nested;
    }
}
