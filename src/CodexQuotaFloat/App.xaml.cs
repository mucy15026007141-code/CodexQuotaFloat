using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Net.NetworkInformation;
using Forms = System.Windows.Forms;
using CodexQuotaFloat.Models;
using CodexQuotaFloat.Services;
using CodexQuotaFloat.ViewModels;
using CodexQuotaFloat.Views;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

namespace CodexQuotaFloat;

public partial class App : System.Windows.Application
{
    private const string MutexName = "Local\\CodexQuotaFloat.SingleInstance";
    private const string ShowEventName = "Local\\CodexQuotaFloat.ShowExisting";
    private const string ExitEventName = "Local\\CodexQuotaFloat.ExitExisting";
    private const string ResetEventName = "Local\\CodexQuotaFloat.ResetExisting";
    private Mutex? _instanceMutex;
    private bool _ownsMutex;
    private EventWaitHandle? _showExistingEvent;
    private EventWaitHandle? _exitExistingEvent;
    private EventWaitHandle? _resetExistingEvent;
    private RegisteredWaitHandle? _showRegistration;
    private RegisteredWaitHandle? _exitRegistration;
    private RegisteredWaitHandle? _resetRegistration;
    private Forms.NotifyIcon? _tray;
    private FloatingWindow? _window;
    private FloatingViewModel? _floatingViewModel;
    private SetupWizardWindow? _wizard;
    private UsageMonitorService? _monitor;
    private LogService? _log;
    private readonly LogService _bootstrapLog = new();
    private readonly long _constructedAt = Stopwatch.GetTimestamp();
    private StartupService? _startup;
    private bool _exiting;
    private bool _resetOnStartup;
    private bool _isResettingWindow;
    private bool _mutexReleased;
    private readonly SettingsService _settingsService = new();
    private AppSettings _settings = new();
    private DispatcherTimer? _positionSaveTimer;
    private Forms.ToolStripMenuItem? _topmostItem;
    private Forms.ToolStripMenuItem? _avoidTaskbarItem;
    private Forms.ToolStripMenuItem? _toggleItem;
    private Forms.ToolStripMenuItem? _reconnectItem;
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hWnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hWnd, out NativeRect rect);
    [StructLayout(LayoutKind.Sequential)] private readonly struct NativeRect { public readonly int Left, Top, Right, Bottom; }
    private WindowTopmostService? _topmostService;
    private StartupTopmostCoordinator? _startupTopmost;

    public App()
    {
        BootstrapLog.Write("APP_CREATE_BEGIN");
        _bootstrapLog.Write($"Startup lifecycle: timestamp={DateTimeOffset.Now:O}; tid={Environment.CurrentManagedThreadId}; event=AppConstructed");
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        BootstrapLog.Write("ONSTARTUP_ENTER");
        LogStartup("OnStartup");
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _resetOnStartup = e.Args.Contains("--reset-window", StringComparer.OrdinalIgnoreCase);
        base.OnStartup(e);
        if (!TryAcquireInstance(e.Args)) { if (e.Args.Contains("--shutdown", StringComparer.OrdinalIgnoreCase)) WaitForPrimaryExit(); BootstrapLog.Write("SECONDARY_PATH_EXIT"); Shutdown(); return; }

        _log = _bootstrapLog;
        InstallGlobalExceptionLogging();
        _log.Write("Application starting");
        var process = Process.GetCurrentProcess();
        LogStartup($"ProcessIdentity; pid={process.Id}; started={process.StartTime:O}; mainInstance={_ownsMutex}");
        _log.Write("Mutex acquired");
        InstanceRegistry.WriteCurrent();
        _log.Write("Startup arguments: " + DescribeArguments(e.Args));

        if (StartupFlow.InitialPresentation(e.Args.Contains("--shutdown", StringComparer.OrdinalIgnoreCase), setupCompleted: false) == StartupPresentation.Exit)
        {
            _log.Write("Shutdown requested without an existing instance");
            Environment.ExitCode = 1;
            Shutdown();
            return;
        }

        try
        {
            InitializeSignals();
            InitializeTray();
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
            _ = BeginStartupAsync();
        }
        catch (Exception ex)
        {
            LogException("Unhandled startup exception", ex);
            EnsureVisibleFallback();
        }
    }

    private bool TryAcquireInstance(string[] args)
    {
        BootstrapLog.Write("MUTEX_CREATE_BEGIN", MutexName);
        _instanceMutex = new Mutex(initiallyOwned: false, MutexName);
        try { _ownsMutex = _instanceMutex.WaitOne(0); BootstrapLog.Write(_ownsMutex ? "MUTEX_OWNED_TRUE" : "MUTEX_OWNED_FALSE"); }
        catch (AbandonedMutexException) { _ownsMutex = true; BootstrapLog.Write("MUTEX_ABANDONED_RECOVERED"); }
        if (SingleInstancePolicy.ShouldContinueStartup(_ownsMutex)) { BootstrapLog.Write("PRIMARY_PATH_ENTER"); LogStartup("SingleInstance; main=true"); return true; }

        BootstrapLog.Write("SECONDARY_PATH_ENTER");
        try
        {
            var eventName = SingleInstancePolicy.EventForArguments(args) switch { "shutdown" => ExitEventName, "reset" => ResetEventName, _ => ShowEventName };
            using var existingEvent = EventWaitHandle.OpenExisting(eventName);
            existingEvent.Set();
            BootstrapLog.Write("SECONDARY_SIGNAL_SUCCESS", eventName);
            LogStartup($"SecondInstanceRequest; request={eventName}");
        }
        catch (Exception ex) { BootstrapLog.Write("SECONDARY_SIGNAL_FAILED", ex.GetType().Name + ": " + ex.Message); }
        _instanceMutex.Dispose();
        _instanceMutex = null;
        Shutdown();
        return false;
    }

    private void InitializeSignals()
    {
        BootstrapLog.Write("SECONDARY_SIGNAL_SERVER_START_BEGIN");
        _showExistingEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _exitExistingEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ExitEventName);
        _resetExistingEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ResetEventName);
        _showRegistration = ThreadPool.RegisterWaitForSingleObject(_showExistingEvent, (_, _) => Dispatcher.BeginInvoke(ShowWindow), null, Timeout.Infinite, false);
        _exitRegistration = ThreadPool.RegisterWaitForSingleObject(_exitExistingEvent, (_, _) => Dispatcher.BeginInvoke(() => _ = ExitAsync()), null, Timeout.Infinite, false);
        _resetRegistration = ThreadPool.RegisterWaitForSingleObject(_resetExistingEvent, (_, _) => Dispatcher.BeginInvoke(ResetWindowPosition), null, Timeout.Infinite, false);
        BootstrapLog.Write("SECONDARY_SIGNAL_SERVER_START_END");
    }

    private void InitializeTray()
    {
        _startup = new StartupService();
        _tray = new Forms.NotifyIcon { Icon = LoadTrayIcon(), Visible = true, Text = "Codex 额度悬浮窗" };
        var menu = new Forms.ContextMenuStrip();
        _toggleItem = new Forms.ToolStripMenuItem("显示", null, (_, _) => Toggle());
        menu.Items.Add(_toggleItem);
        menu.Items.Add("立即刷新", null, (_, _) => _floatingViewModel?.RefreshCommand.Execute(null));
        _reconnectItem = new Forms.ToolStripMenuItem("重新连接 Codex", null, async (_, _) => await ReconnectAsync("tray"));
        menu.Items.Add(_reconnectItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        _topmostItem = new Forms.ToolStripMenuItem("始终置顶") { CheckOnClick = true };
        _topmostItem.Click += (_, _) => _floatingViewModel?.ToggleAlwaysOnTopCommand.Execute(null);
        menu.Items.Add(_topmostItem);
        _avoidTaskbarItem = new Forms.ToolStripMenuItem("避让任务栏") { CheckOnClick = true };
        _avoidTaskbarItem.Click += (_, _) => _floatingViewModel?.ToggleAvoidTaskbarCommand.Execute(null);
        menu.Items.Add(_avoidTaskbarItem);
        menu.Items.Add(new Forms.ToolStripMenuItem("重置窗口位置", null, (_, _) => _floatingViewModel?.ResetWindowPositionCommand.Execute(null)));
        menu.Items.Add("配置 Codex CLI", null, (_, _) => ShowSetupWizard());
        var startupItem = new Forms.ToolStripMenuItem("开机启动") { Checked = _startup.IsEnabled(), CheckOnClick = true };
        startupItem.CheckedChanged += (_, _) => { try { _startup.SetEnabled(startupItem.Checked); startupItem.Checked = _startup.IsEnabled(); } catch { startupItem.Checked = _startup.IsEnabled(); } };
        menu.Items.Add(startupItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("打开日志目录", null, (_, _) => OpenLogDirectory());
        menu.Items.Add("退出", null, (_, _) => _floatingViewModel?.ExitCommand.Execute(null));
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowWindow();
        _log!.Write("Tray initialized");
    }

    private async Task BeginStartupAsync()
    {
        try
        {
            var settings = await _settingsService.LoadAsync();
            _settings = settings;
            LogStartup($"SettingsLoaded; requestedAlwaysOnTop={_settings.IsTopmost}; avoidTaskbar={_settings.AvoidTaskbar}");
            UpdateTopmostMenu();
            UpdateAvoidTaskbarMenu();
            if (StartupFlow.InitialPresentation(shutdownRequested: false, setupCompleted: settings.SetupCompleted) == StartupPresentation.FloatingWindow)
            {
                CreateAndShowFloating(startMonitor: true);
                _log!.Write("Startup completed");
                return;
            }

            ShowSetupWizard();
            _log!.Write("Setup check started");
            var result = await new CodexEnvironmentService().CheckAsync();
            _log!.Write("Setup check result: " + result.Status);
            if (_wizard?.DataContext is SetupWizardViewModel viewModel)
                viewModel.SetResult(result);
        }
        catch (Exception ex)
        {
            LogException("Unhandled startup exception", ex);
            EnsureVisibleFallback();
        }
        finally
        {
            if (_wizard is null && _window is null) EnsureVisibleFallback();
        }
    }

    private void ShowSetupWizard()
    {
        if (_wizard is { IsVisible: true }) { _wizard.Activate(); return; }
        var viewModel = new SetupWizardViewModel(new CodexEnvironmentService());
        viewModel.OpenLogsRequested += OpenLogDirectory;
        viewModel.SetupSucceeded += () => _ = PersistSuccessfulSetupAsync(viewModel.Result);
        _wizard = new SetupWizardWindow(viewModel);
        _wizard.Closed += (_, _) =>
        {
            var ready = viewModel.Result.IsReady;
            _wizard = null;
            CreateAndShowFloating(startMonitor: ready);
            _log?.Write("Setup wizard closed");
        };
        _wizard.Show();
        _log!.Write("Setup wizard created");
        _log.Write("Setup wizard shown");
    }

    private async Task PersistSuccessfulSetupAsync(SetupCheckResult result)
    {
        try
        {
            var service = new SettingsService();
            var settings = await service.LoadAsync();
            settings.SetupCompleted = true;
            settings.LastDetectedCodexVersion = result.CliVersion;
            settings.LastSetupCheckTime = DateTimeOffset.Now;
            await service.SaveAsync(settings);
        }
        catch (Exception ex) { LogException("Setup settings save failed", ex); }
    }

    private void CreateAndShowFloating(bool startMonitor)
    {
        if (_window is null)
        {
            _monitor = new UsageMonitorService(_log!);
            _floatingViewModel = new FloatingViewModel(_monitor, startMonitor);
            _floatingViewModel.ConfigureWindowCommands(new FloatingWindowCommandActions
            {
                ToggleAlwaysOnTop = () => SetAlwaysOnTop(!_settings.IsTopmost, persist: true),
                ToggleAvoidTaskbar = () => SetAvoidTaskbar(!_settings.AvoidTaskbar),
                ResetWindowPosition = ResetWindowPosition,
                Exit = () => _ = ExitAsync()
            });
            _floatingViewModel.SetWindowOptions(_settings.IsTopmost, _settings.AvoidTaskbar);
            LogStartup("MainWindowConstructing");
            BootstrapLog.Write("MAIN_WINDOW_CREATE_BEGIN");
            _window = new FloatingWindow { DataContext = _floatingViewModel };
            _window.ExpandedLayoutMeasured += message => _log?.Write(message);
            _window.MeasureExpandedLayout();
            BootstrapLog.Write("MAIN_WINDOW_CREATE_END");
            _topmostService = new WindowTopmostService(_log!);
            _startupTopmost = new StartupTopmostCoordinator();
            _startupTopmost.SetRequestedAlwaysOnTop(_settings.IsTopmost);
            LogStartup("MainWindowConstructed");
            LogStartup("PositionRestoreStart");
            if (WindowPositionService.IsUsableCoordinate(_settings.Left)) _window.Left = _settings.Left;
            if (WindowPositionService.IsUsableCoordinate(_settings.Top)) _window.Top = _settings.Top;
            LogStartup("PositionRestoreEnd");
            LogStartup("ExpandedRestoreStart");
            _floatingViewModel.RestoreExpanded(_settings.IsExpanded);
            LogStartup("ExpandedRestoreEnd");
            MainWindow = _window;
            _window.Closing += (_, args) => { args.Cancel = true; _window.Hide(); };
            _window.SourceInitialized += (_, _) => { _startupTopmost?.MarkHandleCreated(); LogStartup($"SourceInitialized; hwnd={new System.Windows.Interop.WindowInteropHelper(_window).Handle}"); };
            _window.Loaded += (_, _) => LogStartup("Loaded");
            _window.ContentRendered += (_, _) => LogStartup("ContentRendered");
            _window.LocationChanged += (_, _) => SchedulePositionSave();
            _window.StateChanged += (_, _) => LogStartup("StateChanged");
            _window.IsVisibleChanged += (_, _) => { LogStartup($"IsVisibleChanged; visible={_window.IsVisible}"); if (_window.IsVisible) _startupTopmost?.MarkWindowShown(); };
            _window.Activated += (_, _) => LogStartup("Activated");
            _window.Deactivated += (_, _) => { LogStartup("Deactivated"); if (_startupTopmost?.StartupReady == true) _topmostService?.RepairIfNeeded(_window, _settings.IsTopmost, "Deactivated"); };
            _floatingViewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(FloatingViewModel.IsExpanded))
                {
                    _settings.IsExpanded = _floatingViewModel.IsExpanded;
                    if (!_isResettingWindow) ReanchorWindowForTransition(_floatingViewModel.IsExpanded);
                    ApplyTopmost("ExpandedStateChanged");
                    SchedulePositionSave();
                }
                else if (args.PropertyName == nameof(FloatingViewModel.WindowDragCompleted))
                {
                    SnapWindowToWorkArea();
                    ApplyTopmost("DragCompleted");
                    SchedulePositionSave();
                }
                else if (args.PropertyName == nameof(FloatingViewModel.WindowGeometryChanged))
                {
                    EnsureWindowInWorkArea();
                    ApplyTopmost("GeometryChanged");
                    SchedulePositionSave();
                }
            };
            _log!.Write("Floating window created");
            if (startMonitor) _log.Write("Usage monitor started");
            else _floatingViewModel.ShowConfigurationRequired();
        }
        else if (startMonitor)
        {
            _floatingViewModel?.StartMonitoring();
            _log?.Write("Usage monitor started");
        }
        ShowWindow();
        if (_resetOnStartup) { _resetOnStartup = false; Dispatcher.BeginInvoke(ResetWindowPosition); }
        _log!.Write("Floating window shown");
    }

    private void EnsureVisibleFallback()
    {
        if (!StartupFlow.NeedsVisibleFallback(_wizard is { IsVisible: true }, _window is { IsVisible: true }, _tray is not null)) return;
        _log?.Write("Startup visibility check failed; showing fallback floating window");
        CreateAndShowFloating(startMonitor: false);
    }

    private void Toggle()
    {
        if (_window is null) { CreateAndShowFloating(startMonitor: false); return; }
        if (_window.IsVisible) { _window.Hide(); UpdateToggleMenu(); } else ShowWindow();
    }

    private void ShowWindow()
    {
        if (_window is null) return;
        _window.Show();
        LogStartup("FirstShow");
        _startupTopmost?.MarkWindowShown();
        LogStartup("AvoidTaskbarApplyStart");
        EnsureWindowInWorkArea();
        LogStartup("AvoidTaskbarApplyEnd");
        _ = CompleteStartupAsync();
        UpdateToggleMenu();
    }

    private void SetAlwaysOnTop(bool enabled, bool persist)
    {
        _settings.IsTopmost = enabled;
        _startupTopmost?.SetRequestedAlwaysOnTop(enabled);
        if (_window is not null) ApplyTopmost("AlwaysOnTopChanged");
        _log?.Write($"Always-on-top {(enabled ? "enabled" : "disabled")}");
        if (persist) _ = _settingsService.SaveAsync(_settings);
        UpdateTopmostMenu();
    }

    private void SetAvoidTaskbar(bool enabled)
    {
        _settings.AvoidTaskbar = enabled;
        UpdateAvoidTaskbarMenu();
        MoveWindowToEffectiveBottom();
        ApplyTopmost("AvoidTaskbarChanged");
        _ = SaveWindowPositionAsync();
        _log?.Write($"Avoid-taskbar {(enabled ? "enabled" : "disabled")}");
    }

    private void ApplyTopmost(string reason)
    {
        if (_window is null) return;
        if (_startupTopmost?.StartupReady != true) { LogStartup($"TopmostDeferred; reason={reason}; stage={_startupTopmost?.Stage}"); return; }
        _topmostService?.ApplyAsync(_window, _settings.IsTopmost, reason);
    }

    private async Task CompleteStartupAsync()
    {
        if (_window is null || _topmostService is null || _startupTopmost is null) return;
        _startupTopmost.MarkLayoutRestored();
        if (!_startupTopmost.TryBeginFinalApply()) return;
        await Dispatcher.InvokeAsync(() => LogStartup("FirstDispatcherIdle"), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        LogStartup("FinalTopmostApplyStart");
        var success = await _topmostService.ApplyAfterStartupReadyAsync(_window, _startupTopmost.RequestedAlwaysOnTop, "StartupReady");
        _startupTopmost.Complete(_topmostService.Verify(_window, "StartupReady:final"));
        if (!success && _startupTopmost.RequestedAlwaysOnTop) _log?.Write("STARTUP_TOPMOST_VERIFICATION_FAILED");
        if (!success && _startupTopmost.RequestedAlwaysOnTop) _log?.Write("STARTUP_TOPMOST_REPAIR_FAILED");
        LogStartup($"StartupInitializationCompleted; ready={_startupTopmost.StartupReady}; requested={_startupTopmost.RequestedAlwaysOnTop}; actual={_startupTopmost.ActualTopmost}");
    }

    private void UpdateTopmostMenu() { if (_topmostItem is not null) _topmostItem.Checked = _settings.IsTopmost; _floatingViewModel?.SetWindowOptions(_settings.IsTopmost, _settings.AvoidTaskbar); }
    private void UpdateAvoidTaskbarMenu() { if (_avoidTaskbarItem is not null) _avoidTaskbarItem.Checked = _settings.AvoidTaskbar; _floatingViewModel?.SetWindowOptions(_settings.IsTopmost, _settings.AvoidTaskbar); }
    private void UpdateToggleMenu() { if (_toggleItem is not null) _toggleItem.Text = _window?.IsVisible == true ? "隐藏" : "显示"; }
    private double DpiScaleX
    {
        get
        {
            if (_window is null || !_window.IsInitialized) return 1;
            var handle = new System.Windows.Interop.WindowInteropHelper(_window).Handle;
            var dpi = handle == nint.Zero ? 96u : GetDpiForWindow(handle);
            return dpi == 0 ? 1 : dpi / 96d;
        }
    }
    private double DpiScaleY => DpiScaleX;
    private Forms.Screen CurrentScreen()
    {
        var scale = WindowCoordinateScale();
        var scaleX = scale.X; var scaleY = scale.Y;
        return Forms.Screen.FromPoint(new System.Drawing.Point((int)Math.Round((_window?.Left ?? 0) * scaleX), (int)Math.Round((_window?.Top ?? 0) * scaleY)));
    }
    private MonitorBounds GetMonitorBounds(Forms.Screen screen)
    {
        var bounds = screen.WorkingArea;
        var scale = WindowCoordinateScale();
        var full = screen.Bounds;
        var taskbar = TaskbarService.GetState();
        return new(
            new(full.Left / scale.X, full.Top / scale.Y, full.Right / scale.X, full.Bottom / scale.Y),
            new(bounds.Left / scale.X, bounds.Top / scale.Y, bounds.Right / scale.X, bounds.Bottom / scale.Y),
            taskbar.AutoHide,
            taskbar.Edge);
    }
    private WorkArea WorkAreaForScreen(Forms.Screen screen) => WindowPositionService.GetEffectiveWindowBounds(GetMonitorBounds(screen), _settings.AvoidTaskbar);
    private (double X, double Y) WindowCoordinateScale()
    {
        try
        {
            if (_window is not null && _window.IsLoaded)
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(_window).Handle;
                if (handle != nint.Zero && GetWindowRect(handle, out var rect))
                {
                    var width = CurrentWindowSize().Width;
                    var height = CurrentWindowSize().Height;
                    if (width > 0 && height > 0) return (Math.Max(0.5, (rect.Right - rect.Left) / width), Math.Max(0.5, (rect.Bottom - rect.Top) / height));
                }
            }
        }
        catch { }
        return (1, 1);
    }
    private WorkArea CurrentWorkArea() => WorkAreaForScreen(CurrentScreen());
    private WpfSize CurrentWindowSize(bool? expanded = null)
    {
        var isExpanded = expanded ?? _floatingViewModel?.IsExpanded == true;
        return WindowPositionService.GetEffectiveSize(_window?.ActualWidth ?? double.NaN, _window?.Width ?? double.NaN, _window?.ActualHeight ?? double.NaN, _window?.Height ?? double.NaN, isExpanded);
    }
    private void EnsureWindowInWorkArea()
    {
        if (_window is null) return;
        var area = CurrentWorkArea();
        var size = CurrentWindowSize();
        var point = WindowPositionService.Restore(new WpfPoint(_window.Left, _window.Top), size, area);
        var corrected = point != new WpfPoint(_window.Left, _window.Top);
        _window.Left = point.X; _window.Top = point.Y; _settings.LastMonitorDeviceName = CurrentScreen().DeviceName;
        if (corrected)
        {
            _settings.Left = point.X; _settings.Top = point.Y;
            _log?.Write("Window moved back into work area");
            SchedulePositionSave();
        }
    }
    private void SnapWindowToWorkArea()
    {
        if (_window is null) return;
        var screen = CurrentScreen();
        var monitor = GetMonitorBounds(screen);
        var area = WindowPositionService.GetEffectiveWindowBounds(monitor, _settings.AvoidTaskbar);
        var size = CurrentWindowSize();
        var before = new WpfPoint(_window.Left, _window.Top);
        var result = WindowPositionService.CalculateSnap(before, size, area);
        _log?.Write($"Bottom placement: top={before.Y:F2}; height={_window.Height:F2}; actualHeight={_window.ActualHeight:F2}; expanded={_floatingViewModel?.IsExpanded == true}; workTopDip={area.Top:F2}; workBottomDip={area.Bottom:F2}; workHeightDip={area.Height:F2}; screenBoundsPx={screen.Bounds.Left},{screen.Bounds.Top},{screen.Bounds.Right},{screen.Bounds.Bottom}; screenWorkingAreaPx={screen.WorkingArea.Left},{screen.WorkingArea.Top},{screen.WorkingArea.Right},{screen.WorkingArea.Bottom}; dpiScale={DpiScaleX:F3}/{DpiScaleY:F3}; avoidTaskbar={_settings.AvoidTaskbar}; taskbarAutoHide={monitor.TaskbarAutoHide}; maxTop={result.MaxTop:F2}; targetTop={result.TargetTop:F2}; snappedBottom={result.SnappedToBottom}; finalTop={result.Position.Y:F2}");
        _window.Left = result.Position.X; _window.Top = result.Position.Y; ApplyTopmost("SnapCompleted");
    }
    private void SchedulePositionSave()
    {
        if (_window is null || !_window.IsVisible) return;
        _positionSaveTimer ??= new DispatcherTimer();
        _positionSaveTimer.Interval = TimeSpan.FromMilliseconds(500);
        _positionSaveTimer.Stop(); _positionSaveTimer.Tick -= PositionSaveTimerTick; _positionSaveTimer.Tick += PositionSaveTimerTick; _positionSaveTimer.Start();
    }
    private async void PositionSaveTimerTick(object? sender, EventArgs e) { _positionSaveTimer?.Stop(); await SaveWindowPositionAsync(); }
    private async Task SaveWindowPositionAsync()
    {
        if (_window is null || !WindowPositionService.IsUsableCoordinate(_window.Left) || !WindowPositionService.IsUsableCoordinate(_window.Top)) return;
        var legal = WindowPositionService.Clamp(new WpfPoint(_window.Left, _window.Top), CurrentWindowSize(), CurrentWorkArea());
        _window.Left = legal.X; _window.Top = legal.Y;
        _settings.Left = _window.Left; _settings.Top = _window.Top; _settings.IsExpanded = _floatingViewModel?.IsExpanded == true;
        await _settingsService.SaveAsync(_settings);
    }
    private void MoveWindowToEffectiveBottom()
    {
        if (_window is null) return;
        var area = CurrentWorkArea();
        var size = CurrentWindowSize();
        var point = WindowPositionService.Clamp(new WpfPoint(_window.Left, area.Bottom - size.Height), size, area);
        _window.Left = point.X; _window.Top = point.Y;
    }
    private void ReanchorWindowForTransition(bool expanding)
    {
        if (_window is null) return;
        var expandedHeight = _window.ExpandedTargetHeight;
        var oldSize = new WpfSize(340, expanding ? FloatingWindow.CompactWindowHeight : expandedHeight);
        var newSize = new WpfSize(340, expanding ? expandedHeight : FloatingWindow.CompactWindowHeight);
        var oldBottom = _window.Top + oldSize.Height;
        var point = WindowPositionService.Clamp(new WpfPoint(_window.Left, oldBottom - newSize.Height), newSize, CurrentWorkArea());
        _window.Left = point.X; _window.Top = point.Y;
    }
    private void ResetWindowPosition()
    {
        if (_window is null) { CreateAndShowFloating(_floatingViewModel is not null); }
        if (_window is null) return;
        _isResettingWindow = true;
        _floatingViewModel?.ResetToCompact();
        _window.StopAnimationsAndSetCompact();
        _isResettingWindow = false;
        _window.WindowState = WindowState.Normal;
        ShowWindow();
        var primary = WorkAreaForScreen(Forms.Screen.PrimaryScreen ?? CurrentScreen());
        var compact = new WpfSize(_window.Width > 0 && WindowPositionService.IsFinite(_window.Width) ? _window.Width : 340, FloatingWindow.CompactWindowHeight);
        var point = WindowPositionService.Clamp(WindowPositionService.DefaultPosition(compact, primary), compact, primary);
        _window.Left = point.X; _window.Top = point.Y;
        ApplyTopmost("ResetWindow"); _ = SaveWindowPositionAsync(); _log?.Write("Window position reset");
    }
    private async Task ReconnectAsync(string reason)
    {
        if (_monitor is null || _exiting) return;
        _reconnectItem?.Enabled = false; _reconnectItem?.Text = "正在重新连接…";
        try { await _monitor.ReconnectAsync(reason); ApplyTopmost("Reconnect:" + reason); }
        finally { _reconnectItem?.Text = "重新连接 Codex"; _reconnectItem?.Enabled = true; }
    }
    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() => { EnsureWindowInWorkArea(); ApplyTopmost("DisplaySettingsChanged"); SchedulePositionSave(); _log?.Write("Display settings changed"); });
    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend) { _log?.Write("System suspend detected"); _monitor?.MarkOffline(); }
        else if (e.Mode == PowerModes.Resume) { _log?.Write("System resume detected"); _ = Dispatcher.InvokeAsync(async () => { await Task.Delay(3000); await ReconnectAsync("resume"); ApplyTopmost("PowerResume"); _log?.Write("Reconnect succeeded after resume"); }); }
    }
    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (!e.IsAvailable) { _log?.Write("Network unavailable"); _monitor?.MarkOffline(); return; }
        _log?.Write("Network available"); _ = Dispatcher.InvokeAsync(async () => { await Task.Delay(2500); await ReconnectAsync("network"); });
    }

    private void OpenLogDirectory()
    {
        if (_log is not null) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", _log.DirectoryPath) { UseShellExecute = true });
    }

    private async Task ExitAsync()
    {
        if (_exiting) return;
        _exiting = true;
        BootstrapLog.Write("SHUTDOWN_BEGIN");
        _topmostService?.SetExiting();
        BootstrapLog.Write("BACKGROUND_CANCEL_BEGIN");
        if (_monitor is not null) await _monitor.DisposeAsync();
        BootstrapLog.Write("BACKGROUND_CANCEL_END");
        BootstrapLog.Write("SETTINGS_SAVE_BEGIN");
        await SaveWindowPositionAsync();
        BootstrapLog.Write("SETTINGS_SAVE_END");
        BootstrapLog.Write("TRAY_DISPOSE_BEGIN");
        if (_tray is not null) { _tray.Visible = false; _tray.Dispose(); _tray = null; }
        BootstrapLog.Write("TRAY_DISPOSE_END");
        InstanceRegistry.TryDeleteCurrent();
        ReleaseMutex();
        BootstrapLog.Write("APPLICATION_SHUTDOWN");
        Shutdown();
    }

    private void WaitForPrimaryExit()
    {
        var instance = InstanceRegistry.ReadLive();
        if (instance is null) { Environment.ExitCode = 1; BootstrapLog.Write("SHUTDOWN_NO_LIVE_INSTANCE"); return; }
        BootstrapLog.Write("SHUTDOWN_SIGNAL_RECEIVED", $"pid={instance.ProcessId}");
        var complete = InstanceRegistry.WaitForExit(instance, TimeSpan.FromSeconds(5));
        Environment.ExitCode = complete ? 0 : 2;
        BootstrapLog.Write(complete ? "SHUTDOWN_CONFIRMED_EXIT" : "SHUTDOWN_WAIT_TIMEOUT", $"pid={instance.ProcessId}");
    }

    private void ReleaseMutex()
    {
        if (!_ownsMutex || _mutexReleased || _instanceMutex is null) return;
        BootstrapLog.Write("MUTEX_RELEASE_BEGIN");
        try { _instanceMutex.ReleaseMutex(); }
        finally { _instanceMutex.Dispose(); _instanceMutex = null; _mutexReleased = true; BootstrapLog.Write("MUTEX_RELEASE_END"); }
    }

    private void LogStartup(string message) => _bootstrapLog.Write($"Startup lifecycle: timestamp={DateTimeOffset.Now:O}; elapsedMs={Stopwatch.GetElapsedTime(_constructedAt).TotalMilliseconds:F0}; tid={Environment.CurrentManagedThreadId}; {message}");

    private void InstallGlobalExceptionLogging()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            LogException("DispatcherUnhandledException", args.Exception);
            args.Handled = true;
            Dispatcher.BeginInvoke(EnsureVisibleFallback);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogException("AppDomainUnhandledException", args.ExceptionObject as Exception ?? new InvalidOperationException("Unknown unhandled exception"));
        TaskScheduler.UnobservedTaskException += (_, args) => { LogException("UnobservedTaskException", args.Exception); args.SetObserved(); };
    }

    private void LogException(string prefix, Exception exception)
    {
        _log?.Write($"{prefix}: {exception.GetType().Name}; {Redact(exception.Message)}; {Redact(exception.StackTrace ?? string.Empty)}");
    }

    private static string Redact(string value)
    {
        var withoutEmails = Regex.Replace(value, @"[\w.+-]+@[\w.-]+", "[redacted-email]");
        return Regex.Replace(withoutEmails, @"(?i)(token|api[_ -]?key|cookie)\s*[:=]\s*[^\s,;]+", "$1=[redacted]");
    }

    private static string DescribeArguments(string[] args) => args.Contains("--shutdown", StringComparer.OrdinalIgnoreCase) ? "shutdown" : args.Contains("--reset-window", StringComparer.OrdinalIgnoreCase) ? "reset-window" : args.Contains("--startup", StringComparer.OrdinalIgnoreCase) ? "startup" : "normal";
    private static System.Drawing.Icon LoadTrayIcon() => System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty) ?? System.Drawing.SystemIcons.Information;

    protected override void OnExit(ExitEventArgs e)
    {
        BootstrapLog.Write("PROCESS_EXIT_BEGIN");
        _showRegistration?.Unregister(null);
        _exitRegistration?.Unregister(null);
        _showExistingEvent?.Dispose();
        _exitExistingEvent?.Dispose();
        _resetExistingEvent?.Dispose();
        ReleaseMutex();
        _tray?.Dispose();
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        _log?.Write("Application exited");
        base.OnExit(e);
    }
}
