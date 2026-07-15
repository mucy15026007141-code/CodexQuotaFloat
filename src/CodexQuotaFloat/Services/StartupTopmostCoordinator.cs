namespace CodexQuotaFloat.Services;

public enum StartupTopmostStage { Uninitialized, HandleCreated, WindowShown, LayoutRestored, Ready }

public sealed class StartupTopmostCoordinator
{
    public StartupTopmostStage Stage { get; private set; } = StartupTopmostStage.Uninitialized;
    public bool StartupReady => Stage == StartupTopmostStage.Ready;
    public bool FinalApplyStarted { get; private set; }
    public bool RequestedAlwaysOnTop { get; private set; }
    public bool? ActualTopmost { get; private set; }

    public void SetRequestedAlwaysOnTop(bool enabled) => RequestedAlwaysOnTop = enabled;
    public void MarkHandleCreated() => AdvanceTo(StartupTopmostStage.HandleCreated);
    public void MarkWindowShown() => AdvanceTo(StartupTopmostStage.WindowShown);
    public void MarkLayoutRestored() => AdvanceTo(StartupTopmostStage.LayoutRestored);
    public bool TryBeginFinalApply() { if (FinalApplyStarted || Stage < StartupTopmostStage.LayoutRestored) return false; FinalApplyStarted = true; return true; }
    public void Complete(bool actualTopmost) { ActualTopmost = actualTopmost; Stage = StartupTopmostStage.Ready; }
    private void AdvanceTo(StartupTopmostStage stage) { if (stage > Stage) Stage = stage; }
}
