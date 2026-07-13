using System.Windows;
using CodexQuotaFloat.Models;
using CodexQuotaFloat.Services;

namespace CodexQuotaFloat.Tests;

public sealed class WindowStabilityTests
{
    private static readonly WorkArea Area = new(0, 0, 1920, 1040);
    private static readonly Size Compact = new(340, 46);

    [Fact] public void AlwaysOnTopDefaultsToEnabled() => Assert.True(new AppSettings().IsTopmost);
    [Fact] public void AlwaysOnTopAliasRoundTrips() { var s = new AppSettings { AlwaysOnTop = false }; Assert.False(s.IsTopmost); Assert.False(s.AlwaysOnTop); }
    [Fact] public void InvalidCoordinateIsDetected() => Assert.False(WindowPositionService.IsFinite(double.NaN));
    [Fact] public void InfiniteCoordinateIsDetected() => Assert.False(WindowPositionService.IsFinite(double.PositiveInfinity));
    [Fact] public void ValidCoordinateIsAccepted() => Assert.True(WindowPositionService.IsFinite(12.5));
    [Fact] public void InvalidPositionUsesPrimaryWorkArea() => Assert.Equal(new Point(1560, 20), WindowPositionService.Restore(new Point(double.NaN, 0), Compact, Area));
    [Fact] public void CompletelyOffscreenPositionUsesPrimaryWorkArea() => Assert.Equal(new Point(1560, 20), WindowPositionService.Restore(new Point(5000, 5000), Compact, Area));
    [Fact] public void PartiallyVisiblePositionIsPreserved() => Assert.Equal(new Point(1580, 10), WindowPositionService.Restore(new Point(1880, 10), Compact, Area));
    [Fact] public void LeftEdgeSnapsInsideWorkArea() => Assert.Equal(20, WindowPositionService.Snap(new Point(5, 200), Compact, Area).X);
    [Fact] public void RightEdgeSnapsInsideWorkArea() => Assert.Equal(1560, WindowPositionService.Snap(new Point(1585, 200), Compact, Area).X);
    [Fact] public void TopEdgeSnapsInsideWorkArea() => Assert.Equal(20, WindowPositionService.Snap(new Point(200, 5), Compact, Area).Y);
    [Fact] public void BottomEdgeSnapsInsideWorkArea() => Assert.Equal(974, WindowPositionService.Snap(new Point(200, 1000), Compact, Area).Y);
    [Fact] public void ClampPreventsRightOverflow() => Assert.Equal(1580, WindowPositionService.Clamp(new Point(2000, 0), Compact, Area).X);
    [Fact] public void ClampPreventsBottomOverflow() => Assert.Equal(994, WindowPositionService.Clamp(new Point(0, 2000), Compact, Area).Y);
    [Fact] public void DefaultPositionLeavesTopMargin() => Assert.Equal(20, WindowPositionService.DefaultPosition(Compact, Area).Y);
    [Fact] public void DefaultPositionLeavesRightMargin() => Assert.Equal(1560, WindowPositionService.DefaultPosition(Compact, Area).X);
    [Fact] public void ExpandedWindowFitsVerticallyAfterClamp() => Assert.Equal(810, WindowPositionService.Clamp(new Point(0, 1000), new Size(340, 230), Area).Y);
    [Fact] public void ExpandedWindowFitsHorizontallyAfterClamp() => Assert.Equal(1580, WindowPositionService.Clamp(new Point(2000, 0), new Size(340, 230), Area).X);
    [Fact] public void OfflineStateIsDistinct() => Assert.NotEqual(ConnectionState.Offline, ConnectionState.Stale);
    [Fact] public void ConnectingStateIsDistinct() => Assert.NotEqual(ConnectionState.Connecting, ConnectionState.Faulted);
    [Fact] public void NotLoggedInStateIsDistinct() => Assert.NotEqual(ConnectionState.NotLoggedIn, ConnectionState.CodexNotFound);
    [Fact] public void FaultedStateIsDistinct() => Assert.Contains(ConnectionState.Faulted, Enum.GetValues<ConnectionState>());
    [Fact] public void StaleStateIsAvailable() => Assert.Contains(ConnectionState.Stale, Enum.GetValues<ConnectionState>());
    [Fact] public void SettingsKeepLegacyPositionFields() { var json = SettingsService.SerializeForTesting(new AppSettings { Left = 12, Top = 34 }); Assert.Contains("Left", json); Assert.Contains("Top", json); }
    [Fact] public void SettingsPersistMonitorName() { var json = SettingsService.SerializeForTesting(new AppSettings { LastMonitorDeviceName = "DISPLAY1" }); Assert.Contains("DISPLAY1", json); }
    [Fact] public void UnlimitedWindowDoesNotLookUnavailable() => Assert.Equal(RateLimitAvailability.Unlimited, RateLimitWindow.Unlimited(300).Availability);
    [Fact] public void UnavailableWindowDoesNotLookUnlimited() => Assert.Equal(RateLimitAvailability.Unavailable, RateLimitWindow.Unavailable(300).Availability);
}
