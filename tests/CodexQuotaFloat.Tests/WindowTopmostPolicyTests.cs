using CodexQuotaFloat.Services;

namespace CodexQuotaFloat.Tests;

public sealed class WindowTopmostPolicyTests
{
    [Fact] public void EnabledUsesHwndTopmost() => Assert.Equal((nint)(-1), WindowTopmostPolicy.HwndTopmost);
    [Fact] public void DisabledUsesHwndNotTopmost() => Assert.Equal((nint)(-2), WindowTopmostPolicy.HwndNotTopmost);
    [Fact] public void ApplyFlagsIncludeNoActivate() => Assert.NotEqual(0u, WindowTopmostPolicy.ApplyFlags & WindowTopmostPolicy.SwpNoActivate);
    [Fact] public void ApplyFlagsIncludeNoMove() => Assert.NotEqual(0u, WindowTopmostPolicy.ApplyFlags & WindowTopmostPolicy.SwpNoMove);
    [Fact] public void ApplyFlagsIncludeNoSize() => Assert.NotEqual(0u, WindowTopmostPolicy.ApplyFlags & WindowTopmostPolicy.SwpNoSize);
    [Fact] public void ApplyFlagsIncludeNoOwnerZOrder() => Assert.NotEqual(0u, WindowTopmostPolicy.ApplyFlags & WindowTopmostPolicy.SwpNoOwnerZOrder);
    [Fact] public void ApplyFlagsDoNotIncludeNoZOrder() => Assert.False(WindowTopmostPolicy.HasNoZOrderFlag(WindowTopmostPolicy.ApplyFlags));
    [Fact] public void TopmostExtendedStyleIsDetected() => Assert.True(WindowTopmostPolicy.IsActuallyTopmost(WindowTopmostPolicy.WsExTopmost));
    [Fact] public void MissingTopmostExtendedStyleIsDetected() => Assert.False(WindowTopmostPolicy.IsActuallyTopmost(0));
    [Fact] public void UnrelatedExtendedStyleDoesNotCountAsTopmost() => Assert.False(WindowTopmostPolicy.IsActuallyTopmost(0x00000080));
    [Fact] public void TopmostStyleCanCoexistWithOtherStyles() => Assert.True(WindowTopmostPolicy.IsActuallyTopmost(WindowTopmostPolicy.WsExTopmost | 0x00000080));
    [Fact] public void RepairHasTwoOperations() => Assert.Equal(2, WindowTopmostPolicy.RepairSequence().Count);
    [Fact] public void RepairBeginsWithNotTopmost() => Assert.Equal(WindowTopmostPolicy.HwndNotTopmost, WindowTopmostPolicy.RepairSequence()[0]);
    [Fact] public void RepairEndsWithTopmost() => Assert.Equal(WindowTopmostPolicy.HwndTopmost, WindowTopmostPolicy.RepairSequence()[1]);
    [Fact] public void RepairDoesNotChangeApplyFlags() => Assert.False(WindowTopmostPolicy.HasNoZOrderFlag(WindowTopmostPolicy.ApplyFlags));
    [Fact] public void NoActivateFlagPreservesInputFocus() => Assert.NotEqual(0u, WindowTopmostPolicy.ApplyFlags & 0x0010);
    [Fact] public void NoMoveFlagPreventsPositionChanges() => Assert.NotEqual(0u, WindowTopmostPolicy.ApplyFlags & 0x0002);
    [Fact] public void NoSizeFlagPreventsSizeChanges() => Assert.NotEqual(0u, WindowTopmostPolicy.ApplyFlags & 0x0001);
    [Fact] public void TopmostStateUsesExtendedStyleIndex() => Assert.Equal((nint)(-20), WindowTopmostPolicy.GwlExStyle);
    [Fact] public void TopmostStyleConstantMatchesWin32() => Assert.Equal((nint)0x00000008, WindowTopmostPolicy.WsExTopmost);
    [Fact] public void DragCompletionCanUseTheSafeApplyFlags() => Assert.False(WindowTopmostPolicy.HasNoZOrderFlag(WindowTopmostPolicy.ApplyFlags));
    [Fact] public void BottomSnapCanUseTheSafeApplyFlags() => Assert.NotEqual(0u, WindowTopmostPolicy.ApplyFlags & WindowTopmostPolicy.SwpNoActivate);
    [Fact] public void AnimationCompletionCanUseTheSafeApplyFlags() => Assert.NotEqual(0u, WindowTopmostPolicy.ApplyFlags & WindowTopmostPolicy.SwpNoOwnerZOrder);
    [Fact] public void ResumeAndDisplayEventsCanUseTheSafeApplyFlags() => Assert.Equal(WindowTopmostPolicy.SwpNoMove | WindowTopmostPolicy.SwpNoSize | WindowTopmostPolicy.SwpNoActivate | WindowTopmostPolicy.SwpNoOwnerZOrder, WindowTopmostPolicy.ApplyFlags);
}
