using System.IO;

namespace CodexQuotaFloat.Tests;

public sealed class FloatingWindowLayoutContractTests
{
    [Fact] public void ContextMenuBindsPlacementTargetDataContext() => Assert.Contains("PlacementTarget.DataContext", Xaml);
    [Fact] public void ContextMenuUsesRefreshCommand() => Assert.Contains("Command=\"{Binding RefreshCommand}\"", Xaml);
    [Fact] public void ContextMenuUsesSharedTopmostCommand() => Assert.Contains("Command=\"{Binding ToggleAlwaysOnTopCommand}\"", Xaml);
    [Fact] public void ContextMenuUsesSharedAvoidTaskbarCommand() => Assert.Contains("Command=\"{Binding ToggleAvoidTaskbarCommand}\"", Xaml);
    [Fact] public void ContextMenuUsesSharedResetCommand() => Assert.Contains("Command=\"{Binding ResetWindowPositionCommand}\"", Xaml);
    [Fact] public void ContextMenuUsesSharedExitCommand() => Assert.Contains("Command=\"{Binding ExitCommand}\"", Xaml);
    [Fact] public void ExpandedLayoutHasIndependentActionRow() => Assert.Contains("<Grid.RowDefinitions><RowDefinition Height=\"Auto\"/><RowDefinition Height=\"Auto\"/><RowDefinition Height=\"Auto\"/></Grid.RowDefinitions>", Xaml);
    [Fact] public void ExpandedActionsUseChevronStyle() => Assert.Equal(2, Count("Style=\"{StaticResource ChevronActionButton}\""));
    [Fact] public void LayoutDoesNotUseCanvas() => Assert.DoesNotContain("<Canvas", Xaml);
    [Fact] public void LayoutDoesNotUseNegativeMargins() => Assert.DoesNotContain("Margin=\"-", Xaml);
    [Fact] public void ExpandedHeightMatchesLayoutContract() => Assert.Equal(262d, CodexQuotaFloat.Views.FloatingWindow.ExpandedWindowHeight);
    [Fact] public void ChevronStyleHasSquareHitTarget() => Assert.Contains("<Setter Property=\"Width\" Value=\"34\"/>", Styles);
    [Fact] public void RefreshStyleHasMinimumWidth() => Assert.Contains("<Setter Property=\"MinWidth\" Value=\"60\"/>", Styles);

    [Fact]
    public void ExpandedDesiredHeightDoesNotExceedFinalHeight()
    {
        var desiredHeight = 261.95;
        Assert.True(desiredHeight <= CodexQuotaFloat.Views.FloatingWindow.ResolveExpandedHeight(desiredHeight));
    }

    [Fact] public void ExpandedActionBarIsBottomAligned() => Assert.Contains("x:Name=\"ExpandedActionBar\" Grid.Row=\"2\" HorizontalAlignment=\"Right\" VerticalAlignment=\"Bottom\"", Xaml);

    [Fact]
    public void ExpandedActionBarBottomInsetIsAtLeastTwelveDip()
    {
        const double measuredActionBarBottom = 1 + 18 + 36.94 + 156.01 + 34;
        var inset = CodexQuotaFloat.Services.FloatingWindowLayoutMetrics.ActionBarBottomInset(262, measuredActionBarBottom);
        Assert.InRange(inset, 12, 16.1);
    }

    [Fact]
    public void TwoThirtyFourDipButtonsFitActionRow()
    {
        Assert.Equal(34, CodexQuotaFloat.Services.FloatingWindowLayoutMetrics.ActionButtonHeight);
        Assert.Contains("<Setter Property=\"Height\" Value=\"34\"/>", Styles);
        Assert.Contains("x:Name=\"ExpandedActionBar\"", Xaml);
    }

    [Fact] public void ExpandAnimationUsesResolvedTarget() => Assert.Contains("AnimateHeight(expandedHeight, version", CodeBehind);
    [Fact] public void BottomAnchorUsesDynamicExpandedTarget() => Assert.Contains("var expandedHeight = _window.ExpandedTargetHeight;", AppCode);

    [Fact]
    public void LayoutRemainsVisibleAtOneHundredFiftyPercentDpi()
    {
        const double scale = 1.5;
        var scaledWindow = CodexQuotaFloat.Views.FloatingWindow.ResolveExpandedHeight(261.95) * scale;
        var scaledContent = 261.95 * scale;
        Assert.True(scaledContent <= scaledWindow);
        Assert.Equal(51, CodexQuotaFloat.Services.FloatingWindowLayoutMetrics.ActionButtonHeight * scale);
    }

    [Fact]
    public void ResetCreditsRowPrecedesVisibleActionBar()
    {
        var reset = Xaml.IndexOf("x:Name=\"ResetCreditsSection\"", StringComparison.Ordinal);
        var actionBar = Xaml.IndexOf("x:Name=\"ExpandedActionBar\"", StringComparison.Ordinal);
        Assert.True(reset >= 0 && actionBar > reset);
    }

    private static string Xaml => ReadSource("src", "CodexQuotaFloat", "Views", "FloatingWindow.xaml");
    private static string Styles => ReadSource("src", "CodexQuotaFloat", "Resources", "Styles.xaml");
    private static string CodeBehind => ReadSource("src", "CodexQuotaFloat", "Views", "FloatingWindow.xaml.cs");
    private static string AppCode => ReadSource("src", "CodexQuotaFloat", "App.xaml.cs");
    private static int Count(string value) => Xaml.Split(value, StringSplitOptions.None).Length - 1;
    private static string ReadSource(params string[] parts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException("Source file was not found from the test output directory.");
    }
}
