using System.Threading;
using CodexQuotaFloat.Models;
using CodexQuotaFloat.Services;

namespace CodexQuotaFloat.Tests;

public sealed class StartupFlowTests
{
    [Fact] public void FirstInstanceContinuesAfterMutexAcquired() => Assert.True(SingleInstancePolicy.ShouldContinueStartup(true));
    [Fact] public void SecondInstanceCannotContinueFullStartup() => Assert.False(SingleInstancePolicy.ShouldContinueStartup(false));
    [Fact] public void ShutdownUsesExitNotificationRatherThanShowNotification() => Assert.Equal("shutdown", SingleInstancePolicy.EventForArguments(["--shutdown"]));
    [Fact] public void FreshConfigurationShowsWizard() => Assert.Equal(StartupPresentation.SetupWizard, StartupFlow.InitialPresentation(false, false));
    [Fact] public void CompletedSetupShowsFloatingWindow() => Assert.Equal(StartupPresentation.FloatingWindow, StartupFlow.InitialPresentation(false, true));
    [Fact] public void ShutdownNeverStartsNormalUi() => Assert.Equal(StartupPresentation.Exit, StartupFlow.InitialPresentation(true, true));
    [Fact] public void LaterSetupShowsConfigurationFloatingWindowWithoutMonitor() => Assert.False(StartupFlow.StartMonitorAfterWizard(new(SetupStatus.NotLoggedIn)));
    [Fact] public void ReadySetupStartsMonitorAfterWizard() => Assert.True(StartupFlow.StartMonitorAfterWizard(new(SetupStatus.Ready)));
    [Fact] public void VisibleWindowOrTrayPreventsFallback() { Assert.False(StartupFlow.NeedsVisibleFallback(true, false, false)); Assert.False(StartupFlow.NeedsVisibleFallback(false, false, true)); }
    [Fact] public void NoWindowAndNoTrayNeedsFallback() => Assert.True(StartupFlow.NeedsVisibleFallback(false, false, false));
    [Fact] public void FreshSettingsWithNanWindowPositionCanBeSerialized() => Assert.Contains("NaN", SettingsService.SerializeForTesting(new AppSettings()));
}
