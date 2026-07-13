using System.ComponentModel;
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
    private StartupService? _startup;
    private bool _exiting;
    private bool _resetOnStartup;
    private readonly SettingsService _settingsService = new();
    private AppSettings _settings = new();
    private DispatcherTimer? _positionSaveTimer;
    private Forms.ToolStripMenuItem? _topmostItem;
    private Forms.ToolStripMenuItem? _toggleItem;
    private Forms.ToolStripMenuItem? _reconnectItem;
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hWnd);
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(NativePoint point, uint flags);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);
    [StructLayout(LayoutKind.Sequential)] private readonly struct NativePoint(int x, int y) { public readonly int X = x; public readonly int Y = y; }
    private static readonly nint HwndTopmost = new(-1), HwndNotTopmost = new(-2);
    private const uint SwpNoActivate = 0x0010, SwpNoMove = 0x0002, SwpNoSize = 0x0001;

    protected override void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _resetOnStartup = e.Args.Contains("--reset-window", StringComparer.OrdinalIgnoreCase);
        if (!TryAcquireInstance(e.Args)) return;
        base.OnStartup(e);

        _log = new LogService();
        InstallGlobalExceptionLogging();
        _log.Write("Application starting");
        _log.Write("Mutex acquired");
        _log.Write("Startup arguments: " + DescribeArguments(e.Args));

        if (StartupFlow.InitialPresentation(e.Args.Contains("--shutdown", StringComparer.OrdinalIgnoreCase), setupCompleted: false) == StartupPresentation.Exit)
        {
            _log.Write("Shutdown requested without an existing instance");
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
        _instanceMutex = new Mutex(initiallyOwned: false, MutexName);
        try { _ownsMutex = _instanceMutex.WaitOne(0); }
        catch (AbandonedMutexException) { _ownsMutex = true; }
        if (SingleInstancePolicy.ShouldContinueStartup(_ownsMutex)) return true;

        try
        {
            var eventName = SingleInstancePolicy.EventForArguments(args) switch { "shutdown" => ExitEventName, "reset" => ResetEventName, _ => ShowEventName };
            using var existingEvent = EventWaitHandle.OpenExisting(eventName);
            existingEvent.Set();
        }
        catch { }
        _instanceMutex.Dispose();
        _instanceMutex = null;
        Shutdown();
        return false;
    }

    private void InitializeSignals()
    {
        _showExistingEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _exitExistingEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ExitEventName);
        _resetExistingEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ResetEventName);
        _showRegistration = ThreadPool.RegisterWaitForSingleObject(_showExistingEvent, (_, _) => Dispatcher.BeginInvoke(ShowWindow), null, Timeout.Infinite, false);
        _exitRegistration = ThreadPool.RegisterWaitForSingleObject(_exitExistingEvent, (_, _) => Dispatcher.BeginInvoke(() => _ = ExitAsync()), null, Timeout.Infinite, false);
        _resetRegistration = ThreadPool.RegisterWaitForSingleObject(_resetExistingEvent, (_, _) => Dispatcher.BeginInvoke(ResetWindowPosition), null, Timeout.Infinite, false);
    }

    private void InitializeTray()
    {
        _startup = new StartupService();
        _tray = new Forms.NotifyIcon { Icon = LoadTrayIcon(), Visible = true, Text = "Codex 额度悬浮窗" };
        var menu = new Forms.ContextMenuStrip();
        _toggleItem = new Forms.ToolStripMenuItem("显示", null, (_, _) => Toggle());
        menu.Items.Add(_toggleItem);
        menu.Items.Add("立即刷新", null, async (_, _) => { if (_monitor is not null) await _monitor.RefreshAsync(); });
        _reconnectItem = new Forms.ToolStripMenuItem("重新连接 Codex", null, async (_, _) => await ReconnectAsync("tray"));
        menu.Items.Add(_reconnectItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        _topmostItem = new Forms.ToolStripMenuItem("始终置顶") { CheckOnClick = true };
        _topmostItem.Click += (_, _) => SetAlwaysOnTop(_topmostItem.Checked, true);
        menu.Items.Add(_topmostItem);
        menu.Items.Add(new Forms.ToolStripMenuItem("重置窗口位置", null, (_, _) => ResetWindowPosition()));
        menu.Items.Add("配置 Codex CLI", null, (_, _) => ShowSetupWizard());
        var startupItem = new Forms.ToolStripMenuItem("开机启动") { Checked = _startup.IsEnabled(), CheckOnClick = true };
        startupItem.CheckedChanged += (_, _) => { try { _startup.SetEnabled(startupItem.Checked); startupItem.Checked = _startup.IsEnabled(); } catch { startupItem.Checked = _startup.IsEnabled(); } };
        menu.Items.Add(startupItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("打开日志目录", null, (_, _) => OpenLogDirectory());
        menu.Items.Add("退出", null, async (_, _) => await ExitAsync());
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
            UpdateTopmostMenu();
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
            _window = new FloatingWindow { DataContext = _floatingViewModel };
            if (WindowPositionService.IsUsableCoordinate(_settings.Left)) _window.Left = _settings.Left;
            if (WindowPositionService.IsUsableCoordinate(_settings.Top)) _window.Top = _settings.Top;
            _floatingViewModel.RestoreExpanded(_settings.IsExpanded);
            MainWindow = _window;
            _window.Closing += (_, args) => { args.Cancel = true; _window.Hide(); };
            _window.SourceInitialized += (_, _) => ApplyTopmost();
            _window.LocationChanged += (_, _) => SchedulePositionSave();
            _floatingViewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(FloatingViewModel.IsExpanded))
                {
                    _settings.IsExpanded = _floatingViewModel.IsExpanded;
                    Dispatcher.BeginInvoke(EnsureWindowInWorkArea);
                    SchedulePositionSave();
                }
                else if (args.PropertyName == nameof(FloatingViewModel.WindowDragCompleted))
                {
                    SnapWindowToWorkArea();
                    SchedulePositionSave();
                }
                else if (args.PropertyName == nameof(FloatingViewModel.WindowGeometryChanged))
                {
                    EnsureWindowInWorkArea();
                    ApplyTopmost();
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
        EnsureWindowInWorkArea();
        ApplyTopmost();
        UpdateToggleMenu();
    }

    private void SetAlwaysOnTop(bool enabled, bool persist)
    {
        _settings.IsTopmost = enabled;
        if (_window is not null) { _window.Topmost = enabled; ApplyTopmost(); }
        _log?.Write($"Always-on-top {(enabled ? "enabled" : "disabled")}");
        if (persist) _ = _settingsService.SaveAsync(_settings);
        UpdateTopmostMenu();
    }

    private void ApplyTopmost()
    {
        if (_window is null || !_window.IsLoaded) return;
        _window.Topmost = _settings.IsTopmost;
        var handle = new System.Windows.Interop.WindowInteropHelper(_window).Handle;
        if (handle != nint.Zero) SetWindowPos(handle, _settings.IsTopmost ? HwndTopmost : HwndNotTopmost, 0, 0, 0, 0, SwpNoActivate | SwpNoMove | SwpNoSize);
    }

    private void UpdateTopmostMenu() { if (_topmostItem is not null) _topmostItem.Checked = _settings.IsTopmost; }
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
        var scaleX = DpiScaleX; var scaleY = DpiScaleY;
        return Forms.Screen.FromPoint(new System.Drawing.Point((int)Math.Round((_window?.Left ?? 0) * scaleX), (int)Math.Round((_window?.Top ?? 0) * scaleY)));
    }
    private WorkArea WorkAreaForScreen(Forms.Screen screen)
    {
        var bounds = screen.WorkingArea;
        var scale = ScreenDpiScale(screen);
        return new(bounds.Left / scale.X, bounds.Top / scale.Y, bounds.Right / scale.X, bounds.Bottom / scale.Y);
    }
    private static (double X, double Y) ScreenDpiScale(Forms.Screen screen)
    {
        try
        {
            var monitor = MonitorFromPoint(new NativePoint(screen.Bounds.Left + 1, screen.Bounds.Top + 1), 2);
            if (monitor != nint.Zero && GetDpiForMonitor(monitor, 0, out var dpiX, out var dpiY) == 0)
                return (Math.Max(1, dpiX / 96d), Math.Max(1, dpiY / 96d));
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
        var size = CurrentWindowSize();
        var point = WindowPositionService.Snap(new WpfPoint(_window.Left, _window.Top), size, CurrentWorkArea());
        _window.Left = point.X; _window.Top = point.Y; ApplyTopmost();
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
    private void ResetWindowPosition()
    {
        if (_window is null) { CreateAndShowFloating(_floatingViewModel is not null); }
        if (_window is null) return;
        _window.StopAnimationsAndSetCompact();
        _floatingViewModel?.ResetToCompact();
        _window.WindowState = WindowState.Normal;
        ShowWindow();
        var primary = WorkAreaForScreen(Forms.Screen.PrimaryScreen ?? CurrentScreen());
        var compact = new WpfSize(_window.Width > 0 && WindowPositionService.IsFinite(_window.Width) ? _window.Width : 340, FloatingWindow.CompactWindowHeight);
        var point = WindowPositionService.Clamp(WindowPositionService.DefaultPosition(compact, primary), compact, primary);
        _window.Left = point.X; _window.Top = point.Y;
        ApplyTopmost(); _ = SaveWindowPositionAsync(); _log?.Write("Window position reset");
    }
    private async Task ReconnectAsync(string reason)
    {
        if (_monitor is null || _exiting) return;
        _reconnectItem?.Enabled = false; _reconnectItem?.Text = "正在重新连接…";
        try { await _monitor.ReconnectAsync(reason); ApplyTopmost(); }
        finally { _reconnectItem?.Text = "重新连接 Codex"; _reconnectItem?.Enabled = true; }
    }
    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() => { EnsureWindowInWorkArea(); ApplyTopmost(); SchedulePositionSave(); _log?.Write("Display settings changed"); });
    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend) { _log?.Write("System suspend detected"); _monitor?.MarkOffline(); }
        else if (e.Mode == PowerModes.Resume) { _log?.Write("System resume detected"); _ = Dispatcher.InvokeAsync(async () => { await Task.Delay(3000); await ReconnectAsync("resume"); _log?.Write("Reconnect succeeded after resume"); }); }
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
        if (_tray is not null) _tray.Visible = false;
        if (_monitor is not null) await _monitor.DisposeAsync();
        await SaveWindowPositionAsync();
        Shutdown();
    }

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
        _showRegistration?.Unregister(null);
        _exitRegistration?.Unregister(null);
        _showExistingEvent?.Dispose();
        _exitExistingEvent?.Dispose();
        _resetExistingEvent?.Dispose();
        if (_ownsMutex) _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        _tray?.Dispose();
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        _log?.Write("Application exited");
        base.OnExit(e);
    }
}
