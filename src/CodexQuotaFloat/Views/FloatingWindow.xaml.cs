using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CodexQuotaFloat.ViewModels;
using CodexQuotaFloat.Services;

namespace CodexQuotaFloat.Views;

public partial class FloatingWindow : Window
{
    private int _transitionVersion;
    public const double CompactWindowHeight = FloatingWindowLayoutMetrics.CompactHeight;
    public const double ExpandedWindowHeight = FloatingWindowLayoutMetrics.MinimumExpandedHeight;
    public double ExpandedTargetHeight { get; private set; } = ExpandedWindowHeight;
    public event Action<string>? ExpandedLayoutMeasured;

    public FloatingWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is FloatingViewModel oldModel) oldModel.PropertyChanged -= ViewModelPropertyChanged;
        if (e.NewValue is FloatingViewModel newModel)
        {
            newModel.PropertyChanged += ViewModelPropertyChanged;
            SetInitialPanel(newModel.IsExpanded);
        }
    }

    private void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FloatingViewModel.IsExpanded) && sender is FloatingViewModel model)
            Dispatcher.Invoke(() => TransitionPanels(model.IsExpanded));
    }

    private void SetInitialPanel(bool expanded)
    {
        CompactPanel.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
        ExpandedPanel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        Height = expanded ? MeasureExpandedHeight() : CompactWindowHeight;
    }

    private void TransitionPanels(bool expand)
    {
        var version = ++_transitionVersion;
        BeginAnimation(HeightProperty, null);
        Height = ActualHeight;

        if (expand)
        {
            (DataContext as FloatingViewModel)?.NotifyWindowGeometryChanged();
            CompactPanel.Visibility = Visibility.Collapsed;
            ExpandedPanel.Visibility = Visibility.Visible;
            var expandedHeight = MeasureExpandedHeight();
            AnimateHeight(expandedHeight, version, () =>
            {
                Height = expandedHeight;
                UpdateLayout();
                RecordExpandedLayout("arranged");
                (DataContext as FloatingViewModel)?.NotifyWindowGeometryChanged();
            });
        }
        else
        {
            // Keep the detail panel visible until the reverse animation completes.
            AnimateHeight(CompactWindowHeight, version, () =>
            {
                if (version != _transitionVersion) return;
                ExpandedPanel.Visibility = Visibility.Collapsed;
                CompactPanel.Visibility = Visibility.Visible;
                Height = CompactWindowHeight;
                (DataContext as FloatingViewModel)?.NotifyWindowGeometryChanged();
            });
        }
    }

    public static double ResolveExpandedHeight(double desiredHeight) =>
        FloatingWindowLayoutMetrics.ResolveExpandedHeight(desiredHeight);

    public double MeasureExpandedLayout()
    {
        var visibility = ExpandedPanel.Visibility;
        ExpandedPanel.Visibility = Visibility.Visible;
        ExpandedPanel.Measure(new System.Windows.Size(Width, double.PositiveInfinity));
        ExpandedTargetHeight = ResolveExpandedHeight(ExpandedPanel.DesiredSize.Height);
        ExpandedPanel.Arrange(new Rect(0, 0, Width, ExpandedTargetHeight));
        RecordExpandedLayout("diagnostic");
        ExpandedPanel.Visibility = visibility;
        return ExpandedTargetHeight;
    }

    private double MeasureExpandedHeight()
    {
        ExpandedPanel.Measure(new System.Windows.Size(Width, double.PositiveInfinity));
        ExpandedTargetHeight = ResolveExpandedHeight(ExpandedPanel.DesiredSize.Height);
        RecordExpandedLayout("measured");
        return ExpandedTargetHeight;
    }

    private void RecordExpandedLayout(string phase)
    {
        var rows = ExpandedRoot.RowDefinitions;
        var padding = ExpandedPanel.Padding;
        var actionBarBottom = ExpandedActionBar.TranslatePoint(new System.Windows.Point(0, ExpandedActionBar.ActualHeight), ExpandedPanel).Y;
        var actionBarBottomInset = ExpandedPanel.ActualHeight - actionBarBottom;
        var dpi = VisualTreeHelper.GetDpi(this);
        var margins = ExpandedHeader.Margin.Top + ExpandedHeader.Margin.Bottom
            + ExpandedQuotaContent.Margin.Top + ExpandedQuotaContent.Margin.Bottom
            + FiveHourSection.Margin.Top + FiveHourSection.Margin.Bottom
            + WeeklySection.Margin.Top + WeeklySection.Margin.Bottom
            + ResetCreditsSection.Margin.Top + ResetCreditsSection.Margin.Bottom;
        ExpandedLayoutMeasured?.Invoke(
            $"ExpandedLayout phase={phase} desired={ExpandedPanel.DesiredSize.Height:F2} actualWindow={ActualHeight:F2} " +
            $"panelActual={ExpandedPanel.ActualHeight:F2} rootDesired={ExpandedRoot.DesiredSize.Height:F2} " +
            $"rows=[{rows[0].ActualHeight:F2},{rows[1].ActualHeight:F2},{rows[2].ActualHeight:F2}] " +
            $"header={ExpandedHeader.ActualHeight:F2} fiveHour={FiveHourSection.ActualHeight:F2} " +
            $"weekly={WeeklySection.ActualHeight:F2} reset={ResetCreditsSection.ActualHeight:F2} " +
            $"actionBar={ExpandedActionBar.ActualHeight:F2} actionBarBottomInset={actionBarBottomInset:F2} " +
            $"padding={padding.Top + padding.Bottom:F2} dpiScale={dpi.DpiScaleY:F2} " +
            $"margins={margins:F2} minimum={ExpandedWindowHeight:F2} target={ExpandedTargetHeight:F2}");
    }

    private void AnimateHeight(double target, int version, Action completed)
    {
        var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(190)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } };
        animation.Completed += (_, _) => { if (version == _transitionVersion) completed(); };
        BeginAnimation(HeightProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    public void StopAnimationsAndSetCompact()
    {
        ++_transitionVersion;
        BeginAnimation(HeightProperty, null);
        ExpandedPanel.Visibility = Visibility.Collapsed;
        CompactPanel.Visibility = Visibility.Visible;
        Height = CompactWindowHeight;
        UpdateLayout();
    }

    private void HeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || WindowState != WindowState.Normal || IsInteractiveElement(e.OriginalSource as DependencyObject)) return;
        e.Handled = true;
        DragMove();
        (DataContext as FloatingViewModel)?.NotifyWindowDragCompleted();
    }

    private static bool IsInteractiveElement(DependencyObject? source)
    {
        while (source is not null)
        {
            if (WindowDragPolicy.IsInteractiveType(source.GetType())) return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }
}
