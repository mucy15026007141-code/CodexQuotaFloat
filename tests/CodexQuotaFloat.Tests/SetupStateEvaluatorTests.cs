using CodexQuotaFloat.Models;
using CodexQuotaFloat.Services;

namespace CodexQuotaFloat.Tests;

public sealed class SetupStateEvaluatorTests
{
    [Fact] public void ReportsMissingCli() => Assert.Equal(SetupStatus.CodexNotFound, SetupStateEvaluator.Evaluate(null, null, null, false).Status);
    [Fact] public void ReportsOldCli() => Assert.Equal(SetupStatus.VersionTooOld, SetupStateEvaluator.Evaluate("codex.cmd", "codex-cli 0.144.0", null, false).Status);
    [Fact] public void AcceptsMinimumSupportedVersion() => Assert.Equal(SetupStatus.Ready, SetupStateEvaluator.Evaluate("codex.cmd", "codex-cli 0.144.1", "chatgpt", true).Status);
    [Fact] public void ReportsNotLoggedIn() => Assert.Equal(SetupStatus.NotLoggedIn, SetupStateEvaluator.Evaluate("codex.cmd", "0.144.1", "notLoggedIn", false).Status);
    [Fact] public void ReportsApiKeyMode() => Assert.Equal(SetupStatus.ApiKeyMode, SetupStateEvaluator.Evaluate("codex.cmd", "0.144.1", "apiKey", false).Status);
    [Fact] public void ReportsConnectionFailure() => Assert.Equal(SetupStatus.ConnectionFailed, SetupStateEvaluator.Evaluate("codex.cmd", "0.144.1", null, false, "InvalidOperationException").Status);
    [Fact] public void ReportsIncompleteQuota() => Assert.Equal(SetupStatus.IncompleteQuotaData, SetupStateEvaluator.Evaluate("codex.cmd", "0.144.1", "chatgpt", false).Status);
    [Fact] public void WeeklyOnlyQuotaIsReady() => Assert.Equal(SetupStatus.Ready, SetupStateEvaluator.Evaluate("codex.cmd", "0.144.1", "chatgpt", true).Status);
    [Fact] public void DoesNotMarkSetupCompleteUnlessReady() => Assert.False(SetupStateEvaluator.Evaluate("codex.cmd", "0.144.1", "chatgpt", false).IsReady);
    [Fact] public void DiagnosticIsRedacted() { var text = DiagnosticInfo.Create(new(SetupStatus.ConnectionFailed, "codex.cmd", "0.144.1", "TimeoutException")); Assert.DoesNotContain("@", text); Assert.DoesNotContain("token", text, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("api key", text, StringComparison.OrdinalIgnoreCase); }
    [Fact] public void SingleInstanceNamesAreStable() { Assert.Equal("Local\\CodexQuotaFloat.SingleInstance", "Local\\CodexQuotaFloat.SingleInstance"); }
}
