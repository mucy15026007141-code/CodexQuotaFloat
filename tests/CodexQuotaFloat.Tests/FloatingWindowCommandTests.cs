using CodexQuotaFloat.Services;
using CodexQuotaFloat.ViewModels;

namespace CodexQuotaFloat.Tests;

public sealed class FloatingWindowCommandTests
{
    [Fact] public void TopmostCommandUsesSharedAction()
    {
        var calls = 0; var model = Create(() => calls++, () => { }, () => { }, () => { });
        model.ToggleAlwaysOnTopCommand.Execute(null);
        Assert.Equal(1, calls);
    }

    [Fact] public void AvoidTaskbarCommandUsesSharedAction()
    {
        var calls = 0; var model = Create(() => { }, () => calls++, () => { }, () => { });
        model.ToggleAvoidTaskbarCommand.Execute(null);
        Assert.Equal(1, calls);
    }

    [Fact] public void ResetCommandUsesSharedAction()
    {
        var calls = 0; var model = Create(() => { }, () => { }, () => calls++, () => { });
        model.ResetWindowPositionCommand.Execute(null);
        Assert.Equal(1, calls);
    }

    [Fact] public void ExitCommandUsesSharedAction()
    {
        var calls = 0; var model = Create(() => { }, () => { }, () => { }, () => calls++);
        model.ExitCommand.Execute(null);
        Assert.Equal(1, calls);
    }

    [Fact] public void WindowOptionStateSynchronizesForEitherMenu()
    {
        var model = Create(() => { }, () => { }, () => { }, () => { });
        model.SetWindowOptions(isAlwaysOnTop: true, isAvoidTaskbar: false);
        Assert.True(model.IsAlwaysOnTop);
        Assert.False(model.IsAvoidTaskbar);
    }

    private static FloatingViewModel Create(Action topmost, Action avoidTaskbar, Action reset, Action exit)
    {
        var model = new FloatingViewModel(new UsageMonitorService(new LogService()), startMonitor: false);
        model.ConfigureWindowCommands(new FloatingWindowCommandActions { ToggleAlwaysOnTop = topmost, ToggleAvoidTaskbar = avoidTaskbar, ResetWindowPosition = reset, Exit = exit });
        return model;
    }
}
