using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using CodexQuotaFloat.Services;

namespace CodexQuotaFloat.Tests;

public sealed class WindowDragPolicyTests
{
    [Fact] public void ExpandedTitleTextCanDrag() => Assert.False(WindowDragPolicy.IsInteractiveType(typeof(TextBlock)));
    [Fact] public void FiveHourLabelCanDrag() => Assert.False(WindowDragPolicy.IsInteractiveType(typeof(Label)));
    [Fact] public void FiveHourPercentageCanDrag() => Assert.False(WindowDragPolicy.IsInteractiveType(typeof(TextBlock)));
    [Fact] public void FiveHourProgressBarCanDrag() => Assert.False(WindowDragPolicy.IsInteractiveType(typeof(ProgressBar)));
    [Fact] public void FiveHourCountdownCanDrag() => Assert.False(WindowDragPolicy.IsInteractiveType(typeof(TextBlock)));
    [Fact] public void WeeklyLabelCanDrag() => Assert.False(WindowDragPolicy.IsInteractiveType(typeof(Label)));
    [Fact] public void WeeklyPercentageCanDrag() => Assert.False(WindowDragPolicy.IsInteractiveType(typeof(TextBlock)));
    [Fact] public void WeeklyProgressBarCanDrag() => Assert.False(WindowDragPolicy.IsInteractiveType(typeof(ProgressBar)));
    [Fact] public void WeeklyCountdownCanDrag() => Assert.False(WindowDragPolicy.IsInteractiveType(typeof(TextBlock)));
    [Fact] public void BackgroundGridCanDrag() => Assert.False(WindowDragPolicy.IsInteractiveType(typeof(Grid)));
    [Fact] public void RefreshButtonDoesNotDrag() => Assert.True(WindowDragPolicy.IsInteractiveType(typeof(Button)));
    [Fact] public void CollapseButtonDoesNotDrag() => Assert.True(WindowDragPolicy.IsInteractiveType(typeof(ButtonBase)));
    [Fact] public void CompactExpandButtonDoesNotDrag() => Assert.True(WindowDragPolicy.IsInteractiveType(typeof(Button)));
    [Fact] public void MenuItemsDoNotDrag() => Assert.True(WindowDragPolicy.IsInteractiveType(typeof(MenuItem)));
    [Fact] public void TextInputsDoNotDrag() => Assert.True(WindowDragPolicy.IsInteractiveType(typeof(TextBoxBase)) && WindowDragPolicy.IsInteractiveType(typeof(PasswordBox)));
}
