using System.Windows;
using CodexQuotaFloat.Services;

namespace CodexQuotaFloat.Tests;

public sealed class BottomSnapRegressionTests
{
    [Fact] public void CompactBottomTargetUsesBottomMinus46() { var a = new WorkArea(0, 0, 1920, 1040); var r = WindowPositionService.CalculateSnap(new(100, 990), new(340, 46), a); Assert.Equal(994, r.TargetTop); }
    [Fact] public void ExpandedBottomTargetUsesBottomMinus230() { var a = new WorkArea(0, 0, 1920, 1040); var r = WindowPositionService.CalculateSnap(new(100, 800), new(340, 230), a); Assert.Equal(810, r.TargetTop); }
    [Fact] public void WorkAreaBottomIsNotReducedAgainForTaskbar() { var a = new WorkArea(0, 0, 1920, 994); var r = WindowPositionService.CalculateSnap(new(100, 950), new(340, 46), a); Assert.Equal(948, r.TargetTop); }
    [Fact] public void SnapThresholdDoesNotChangeBottomTarget() { var a = new WorkArea(0, 0, 1920, 1040); var r = WindowPositionService.CalculateSnap(new(100, 990), new(340, 46), a); Assert.Equal(a.Bottom - 46, r.TargetTop); }
    [Fact] public void DefaultMarginDoesNotEnterBottomTarget() { var a = new WorkArea(0, 0, 1920, 1040); var r = WindowPositionService.CalculateSnap(new(100, 990), new(340, 46), a); Assert.NotEqual(a.Bottom - 46 - WindowPositionService.RestoreMargin, r.TargetTop); }
    [Fact] public void LegalBottomPositionIsNotMovedUpAgain() { var a = new WorkArea(0, 0, 1920, 1040); var p = new Point(100, 994); Assert.Equal(p, WindowPositionService.Clamp(p, new(340, 46), a)); }
    [Fact] public void CompactBottomErrorIsZeroDip() { var a = new WorkArea(0, 0, 1920, 1040); var r = WindowPositionService.CalculateSnap(new(100, 990), new(340, 46), a); Assert.InRange(Math.Abs(a.Bottom - (r.Position.Y + 46)), 0, 4); }
    [Fact] public void Dpi125BottomErrorIsWithinFourDip() { var a = new WorkArea(0, 0, 1536, 832); var r = WindowPositionService.CalculateSnap(new(100, 790), new(272, 36.8), a); Assert.InRange(Math.Abs(a.Bottom - (r.Position.Y + 36.8)), 0, 4); }
    [Fact] public void Dpi150BottomErrorIsWithinFourDip() { var a = new WorkArea(0, 0, 1280, 693.333333); var r = WindowPositionService.CalculateSnap(new(100, 650), new(226.666667, 30.666667), a); Assert.InRange(Math.Abs(a.Bottom - (r.Position.Y + 30.666667)), 0, 4); }
    [Fact] public void BottomResultUsesSingleClampPass() { var a = new WorkArea(0, 0, 1920, 1040); var r = WindowPositionService.CalculateSnap(new(100, 994), new(340, 46), a); Assert.Equal(994, r.Position.Y); Assert.Equal(r.MaxTop, r.TargetTop); }
    [Fact] public void OvershotBottomUsesExactBottomTarget() { var a = new WorkArea(0, 0, 1920, 1040); var r = WindowPositionService.CalculateSnap(new(100, 1043), new(340, 46), a); Assert.True(r.SnappedToBottom); Assert.Equal(a.Bottom - 46, r.TargetTop); Assert.Equal(r.TargetTop, r.Position.Y); }
}
