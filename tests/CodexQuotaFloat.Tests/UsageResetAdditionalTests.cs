using System.Text.Json;
using CodexQuotaFloat.Services;

namespace CodexQuotaFloat.Tests;

public sealed class UsageResetAdditionalTests
{
    [Fact] public void ExactZeroFromAppServerIsPreserved() => Assert.Equal(0, Parse("""{"rateLimitResetCredits":{"availableCount":0}}"""));
    [Fact] public void NegativeCountIsUnknown() => Assert.Null(Parse("""{"rateLimitResetCredits":{"availableCount":-1}}"""));
    [Fact] public void DecimalCountIsUnknown() => Assert.Null(Parse("""{"rateLimitResetCredits":{"availableCount":1.5}}"""));
    [Fact] public void StringCountIsUnknown() => Assert.Null(Parse("""{"rateLimitResetCredits":{"availableCount":"1"}}"""));
    [Fact] public void UnrelatedAvailableCountIsIgnored() => Assert.Null(Parse("""{"other":{"availableCount":1}}"""));
    [Fact] public void ResetCountInsideArrayIsAccepted() => Assert.Equal(5, Parse("""{"events":[{"rateLimitResetCredits":{"availableCount":5}}]}"""));
    [Fact] public void FirstExactCountWins() => Assert.Equal(2, Parse("""{"rateLimitResetCredits":{"availableCount":2},"later":{"resetCredits":{"availableCount":7}}}"""));
    [Fact] public void CreditBalanceStringIsNotTreatedAsCount() => Assert.Null(Parse("""{"rateLimits":{"credits":{"balance":"1"}}}"""));

    private static int? Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return UsageParser.Parse(document.RootElement).BankedResetCount;
    }
}
