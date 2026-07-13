using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CodexQuotaFloat.ViewModels;

namespace CodexQuotaFloat.Views;

public partial class FloatingWindow : Window
{
    private const double CompactHeight = 46;
    private const double ExpandedHeight = 230;
    private int _transitionVersion;
    public const double CompactWindowHeight = CompactHeight;
    public const double ExpandedWindowHeight = ExpandedHeight;

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
        Height = expanded ? ExpandedHeight : CompactHeight;
    }

    private void TransitionPanels(bool expand)
    {
        var version = ++_transitionVersion;
        BeginAnimation(HeightProperty, null);
        Height = ActualHeight;

        if (expand)
        {
            KeepExpandedWindowVisible();
            CompactPanel.Visibility = Visibility.Collapsed;
            ExpandedPanel.Visibility = Visibility.Visible;
            AnimateHeight(ExpandedHeight, version, () => Height = ExpandedHeight);
        }
        else
        {
            // Keep the detail panel visible until the reverse animation completes.
            AnimateHeight(CompactHeight, version, () =>
            {
                if (version != _transitionVersion) return;
                ExpandedPanel.Visibility = Visibility.Collapsed;
                CompactPanel.Visibility = Visibility.Visible;
                Height = CompactHeight;
            });
        }
    }

    private void AnimateHeight(double target, int version, Action completed)
    {
        var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(190)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } };
        animation.Completed += (_, _) => { if (version == _transitionVersion) completed(); };
        BeginAnimation(HeightProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void KeepExpandedWindowVisible()
    {
        var workArea = SystemParameters.WorkArea;
        if (Top + ExpandedHeight > workArea.Bottom) Top = Math.Max(workArea.Top, workArea.Bottom - ExpandedHeight);
    }

    private void HeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && !IsButtonSource(e.OriginalSource as DependencyObject))
        {
            DragMove();
            (DataContext as FloatingViewModel)?.NotifyWindowDragCompleted();
        }
    }

    private static bool IsButtonSource(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase) return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }
}
