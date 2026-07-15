using System.Text.Json;
using System.Windows;
using CodexQuotaFloat.Models;
using CodexQuotaFloat.Services;

namespace CodexQuotaFloat.Tests;

public sealed class WindowBoundaryRegressionTests
{
    private static readonly WorkArea Area = new(0, 0, 1920, 1040);

    [Fact] public void RightSnapUsesRightMinusWidth() => Assert.Equal(1580, WindowPositionService.Snap(new(1900, 400), new(340, 46), Area).X);
    [Fact] public void BottomSnapUsesBottomMinusHeight() => Assert.Equal(994, WindowPositionService.Snap(new(400, 1020), new(340, 46), Area).Y);
    [Fact] public void HorizontalEdgesUseSymmetricClamp() { Assert.Equal(0, WindowPositionService.Clamp(new(-100, 0), new(340, 46), Area).X); Assert.Equal(1580, WindowPositionService.Clamp(new(2000, 0), new(340, 46), Area).X); }
    [Fact] public void VerticalEdgesUseSymmetricClamp() { Assert.Equal(0, WindowPositionService.Clamp(new(0, -100), new(340, 46), Area).Y); Assert.Equal(994, WindowPositionService.Clamp(new(0, 2000), new(340, 46), Area).Y); }
    [Fact] public void ExpandedClampUsesExpandedHeight() => Assert.Equal(810, WindowPositionService.Clamp(new(0, 1000), new(340, 230), Area).Y);
    [Fact] public void ExpandedClampUsesFullWidth() => Assert.Equal(1580, WindowPositionService.Clamp(new(2000, 0), new(340, 230), Area).X);
    [Fact] public void ExpandedBottomRightPointKeepsWindowVisible() { var p = WindowPositionService.Clamp(new(1900, 1000), new(340, 230), Area); Assert.True(p.X + 340 <= Area.Right && p.Y + 230 <= Area.Bottom); }
    [Fact] public void CompactBottomRightPointKeepsWindowVisible() { var p = WindowPositionService.Clamp(new(1900, 1000), new(340, 46), Area); Assert.True(p.X + 340 <= Area.Right && p.Y + 46 <= Area.Bottom); }
    [Fact] public void NaNActualWidthFallsBack() => Assert.Equal(340, WindowPositionService.GetEffectiveSize(double.NaN, 0, 100, 100, false).Width);
    [Fact] public void NaNActualHeightFallsBackToExpandedHeight() => Assert.Equal(244, WindowPositionService.GetEffectiveSize(340, 340, double.NaN, 0, true).Height);
    [Fact] public void ZeroDimensionsFallBack() { var size = WindowPositionService.GetEffectiveSize(0, 0, 0, 0, false); Assert.Equal(340, size.Width); Assert.Equal(46, size.Height); }
    [Fact] public void ResetUsesCompactSize() { var size = WindowPositionService.GetEffectiveSize(double.NaN, double.NaN, double.NaN, double.NaN, false); Assert.Equal(new Size(340, 46), size); }
    [Fact] public void DefaultPositionIsInsideWorkArea() { var p = WindowPositionService.DefaultPosition(new(340, 46), Area); Assert.True(p.X >= Area.Left && p.Y >= Area.Top); }
    [Fact] public void DefaultPositionLeavesRequestedMargin() { var p = WindowPositionService.DefaultPosition(new(340, 46), Area); Assert.Equal(20, Area.Right - p.X - 340); }
    [Fact] public void ScreenOutPositionRestores() => Assert.Equal(new Point(1560, 20), WindowPositionService.Restore(new(99999999, 99999999), new(340, 46), Area));
    [Fact] public void ExtremeNegativePositionRestores() => Assert.Equal(new Point(1560, 20), WindowPositionService.Restore(new(-99999999, -99999999), new(340, 46), Area));
    [Fact] public void InvalidPositionIsMovedToDefault() => Assert.Equal(new Point(1560, 20), WindowPositionService.Restore(new(double.NaN, double.PositiveInfinity), new(340, 46), Area));
    [Fact] public void ResetArgumentUsesResetEvent() => Assert.Equal("reset", SingleInstancePolicy.EventForArguments(["--reset-window"]));
    [Fact] public void ResetArgumentDoesNotUseShowEvent() => Assert.NotEqual("show", SingleInstancePolicy.EventForArguments(["--reset-window"]));
    [Fact] public void NormalArgumentUsesShowEvent() => Assert.Equal("show", SingleInstancePolicy.EventForArguments([]));
    [Fact] public void AlwaysOnTopTrueIsDefault() => Assert.True(new AppSettings().AlwaysOnTop);
    [Fact] public void AlwaysOnTopFalseIsHonored() => Assert.False(new AppSettings { AlwaysOnTop = false }.IsTopmost);
    [Fact] public void ClampNeverProducesNaN() { var p = WindowPositionService.Clamp(new(0, 0), new(340, 46), Area); Assert.False(double.IsNaN(p.X) || double.IsNaN(p.Y)); }
    [Fact] public void ClampKeepsTitleBarVisibleWhenWindowExceedsWorkArea() { var p = WindowPositionService.Clamp(new(500, 500), new(3000, 2000), Area); Assert.Equal(Area.Left, p.X); Assert.Equal(Area.Top, p.Y); }
    [Fact] public void OldLeftSettingDeserializes() { var settings = JsonSerializer.Deserialize<AppSettings>("{\"Left\":12,\"Top\":34}"); Assert.Equal(12, settings!.Left); Assert.Equal(34, settings.Top); }
    [Fact] public void InfinityIsNotUsableCoordinate() => Assert.False(WindowPositionService.IsUsableCoordinate(double.NegativeInfinity));
    [Fact] public void HugeFiniteValueIsNotUsableCoordinate() => Assert.False(WindowPositionService.IsUsableCoordinate(10000001));
    [Fact] public void CompactSnapThresholdIsBounded() => Assert.InRange(WindowPositionService.EdgeSnapDistance, 12, 20);
    [Fact] public void HugeWindowUsesFiniteFallbackSize() { var size = WindowPositionService.GetEffectiveSize(99999, 99999, 99999, 99999, true); Assert.Equal(new Size(340, 244), size); }
}
