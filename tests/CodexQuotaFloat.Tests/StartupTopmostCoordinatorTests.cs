using CodexQuotaFloat.Services;

namespace CodexQuotaFloat.Tests;

public sealed class StartupTopmostCoordinatorTests
{
    [Fact] public void StartsUninitialized() => Assert.Equal(StartupTopmostStage.Uninitialized, new StartupTopmostCoordinator().Stage);
    [Fact] public void FinalApplyCannotBeginBeforeShowAndLayout() => Assert.False(new StartupTopmostCoordinator().TryBeginFinalApply());
    [Fact] public void FinalApplyRequiresLayoutRestored() { var c = ReadyForLayout(); Assert.True(c.TryBeginFinalApply()); }
    [Fact] public void FinalApplyRunsOnlyOnce() { var c = ReadyForLayout(); Assert.True(c.TryBeginFinalApply()); Assert.False(c.TryBeginFinalApply()); }
    [Fact] public void HandleCreatedPrecedesWindowShown() { var c = new StartupTopmostCoordinator(); c.MarkHandleCreated(); c.MarkWindowShown(); Assert.Equal(StartupTopmostStage.WindowShown, c.Stage); }
    [Fact] public void LayoutRestoredPrecedesReady() { var c = ReadyForLayout(); Assert.False(c.StartupReady); }
    [Fact] public void CompletionMarksStartupReady() { var c = ReadyForLayout(); c.Complete(true); Assert.True(c.StartupReady); }
    [Fact] public void CompletionRecordsActualTopmost() { var c = ReadyForLayout(); c.Complete(false); Assert.False(c.ActualTopmost); }
    [Fact] public void RequestedTopmostIsIndependentOfActualState() { var c = ReadyForLayout(); c.SetRequestedAlwaysOnTop(true); c.Complete(false); Assert.True(c.RequestedAlwaysOnTop); Assert.False(c.ActualTopmost); }
    [Fact] public void DisabledStartupKeepsRequestedStateFalse() { var c = ReadyForLayout(); c.SetRequestedAlwaysOnTop(false); c.Complete(false); Assert.False(c.RequestedAlwaysOnTop); }
    [Fact] public void RepeatedLifecycleMarksDoNotRegressStage() { var c = ReadyForLayout(); c.MarkHandleCreated(); Assert.Equal(StartupTopmostStage.LayoutRestored, c.Stage); }
    [Fact] public void StartupReadyAllowsPostStartupLifecycleRepairs() { var c = ReadyForLayout(); c.Complete(true); Assert.True(c.StartupReady); }

    private static StartupTopmostCoordinator ReadyForLayout()
    {
        var coordinator = new StartupTopmostCoordinator();
        coordinator.MarkHandleCreated(); coordinator.MarkWindowShown(); coordinator.MarkLayoutRestored();
        return coordinator;
    }
}
