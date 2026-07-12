using System.Text.Json;
using CodexQuotaFloat.Services;
namespace CodexQuotaFloat.Tests;
public sealed class UsageParserTests
{
    [Fact] public void PrefersCodexAndClassifiesWindows() { using var doc = JsonDocument.Parse("""{"rateLimitsByLimitId":{"codex":{"primary":{"windowDurationMins":300,"usedPercent":28,"resetsAt":1},"secondary":{"windowDurationMins":10080,"usedPercent":52,"resetsAt":2}},"codex_other":{"primary":{"windowDurationMins":300,"usedPercent":99}}}}"""); var actual = UsageParser.Parse(doc.RootElement); Assert.Equal(72, actual.FiveHour!.RemainingPercent); Assert.Equal(48, actual.Weekly!.RemainingPercent); }
    [Theory] [InlineData(-5,100)] [InlineData(110,0)] public void ClampsRemaining(double used, int expected) { var json = "{\"rateLimits\":{\"limitId\":\"codex\",\"primary\":{\"windowDurationMins\":300,\"usedPercent\":" + used + "}}}"; using var doc = JsonDocument.Parse(json); Assert.Equal(expected, UsageParser.Parse(doc.RootElement).FiveHour!.RemainingPercent); }
    [Fact] public void MissingWeeklyStaysUnavailable() { using var doc = JsonDocument.Parse("""{"rateLimits":{"limitId":"codex","primary":{"windowDurationMins":300,"usedPercent":10}}}"""); Assert.Null(UsageParser.Parse(doc.RootElement).Weekly); }
}
