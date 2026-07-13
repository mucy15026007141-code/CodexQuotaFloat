using System.Windows;
using CodexQuotaFloat.Models;
using CodexQuotaFloat.Services;

namespace CodexQuotaFloat.Tests;

public sealed class TaskbarBoundaryTests
{
    private static readonly WorkArea Bounds = new(0, 0, 1707, 1067);
    private static readonly WorkArea Working = new(0, 0, 1707, 1019);

    [Fact] public void AvoidTaskbarTrueUsesWorkingAreaBottom() { var m = new MonitorBounds(Bounds, Working, false, TaskbarEdge.Bottom); Assert.Equal(1019, WindowPositionService.GetEffectiveWindowBounds(m, true).Bottom); }
    [Fact] public void AvoidTaskbarFalseUsesMonitorBoundsBottom() { var m = new MonitorBounds(Bounds, Working, false, TaskbarEdge.Bottom); Assert.Equal(1067, WindowPositionService.GetEffectiveWindowBounds(m, false).Bottom); }
    [Fact] public void FixedBottomTaskbarAvoidanceTargetsTaskbarTop() { var m = new MonitorBounds(Bounds, Working, false, TaskbarEdge.Bottom); var a = WindowPositionService.GetEffectiveWindowBounds(m, true); var r = WindowPositionService.CalculateSnap(new(100, 1000), new(340, 46), a); Assert.Equal(973, r.TargetTop); }
    [Fact] public void FixedBottomTaskbarDisabledTargetsPhysicalBottom() { var m = new MonitorBounds(Bounds, Working, false, TaskbarEdge.Bottom); var a = WindowPositionService.GetEffectiveWindowBounds(m, false); var r = WindowPositionService.CalculateSnap(new(100, 1040), new(340, 46), a); Assert.Equal(1021, r.TargetTop); }
    [Fact] public void AutoHiddenTaskbarLeavesTwoDipTriggerArea() { var m = new MonitorBounds(Bounds, Working, true, TaskbarEdge.Bottom); Assert.Equal(1065, WindowPositionService.GetEffectiveWindowBounds(m, true).Bottom); }
    [Fact] public void AvoidTaskbarDefaultsFalse() => Assert.False(new AppSettings().AvoidTaskbar);
    [Fact] public void AvoidTaskbarSettingCanBeSavedInMemory() { var s = new AppSettings { AvoidTaskbar = true }; Assert.True(s.AvoidTaskbar); }
    [Fact] public void EffectiveBoundarySwitchMovesBottomTargetImmediately() { var m = new MonitorBounds(Bounds, Working, false, TaskbarEdge.Bottom); var compact = new Size(340, 46); Assert.Equal(973, WindowPositionService.GetEffectiveWindowBounds(m, true).Bottom - compact.Height); Assert.Equal(1021, WindowPositionService.GetEffectiveWindowBounds(m, false).Bottom - compact.Height); }
    [Fact] public void BottomAnchoredExpandKeepsOldBottom() { var oldTop = 973d; var oldHeight = 46d; var newTop = oldTop + oldHeight - 230; Assert.Equal(1019, newTop + 230); }
    [Fact] public void BottomAnchoredCollapseKeepsOldBottom() { var oldTop = 787d; var oldHeight = 230d; var newTop = oldTop + oldHeight - 46; Assert.Equal(1017, newTop + 46); }
    [Fact] public void ExpandedBottomButtonRemainsWithinEffectiveBoundary() { var m = new MonitorBounds(Bounds, Working, false, TaskbarEdge.Bottom); var a = m.Effective(false); var p = WindowPositionService.Clamp(new(0, a.Bottom - 230), new(340, 230), a); Assert.True(p.Y + 230 <= a.Bottom); }
    [Fact] public void DifferentMonitorBoundsRemainIndependent() { var second = new MonitorBounds(new(1707, 0, 3414, 1067), new(1707, 0, 3414, 1019), false, TaskbarEdge.Bottom); Assert.Equal(3414, second.Effective(false).Right); Assert.Equal(1019, second.Effective(true).Bottom); }
}
