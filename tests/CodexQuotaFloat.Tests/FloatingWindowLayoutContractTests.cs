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
    [Fact] public void ExpandedHeightMatchesLayoutContract() => Assert.Equal(244d, CodexQuotaFloat.Views.FloatingWindow.ExpandedWindowHeight);
    [Fact] public void ChevronStyleHasSquareHitTarget() => Assert.Contains("<Setter Property=\"Width\" Value=\"34\"/>", Styles);
    [Fact] public void RefreshStyleHasMinimumWidth() => Assert.Contains("<Setter Property=\"MinWidth\" Value=\"60\"/>", Styles);

    private static string Xaml => ReadSource("src", "CodexQuotaFloat", "Views", "FloatingWindow.xaml");
    private static string Styles => ReadSource("src", "CodexQuotaFloat", "Resources", "Styles.xaml");
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
