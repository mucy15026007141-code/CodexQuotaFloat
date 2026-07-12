using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using CodexQuotaFloat.Models;
using CodexQuotaFloat.Services;
using CodexQuotaFloat.ViewModels;
using CodexQuotaFloat.Views;

namespace CodexQuotaFloat;

public partial class App : System.Windows.Application
{
    private const string MutexName = "Local\\CodexQuotaFloat.SingleInstance";
    private const string ShowEventName = "Local\\CodexQuotaFloat.ShowExisting";
    private const string ExitEventName = "Local\\CodexQuotaFloat.ExitExisting";
    private Mutex? _instanceMutex;
    private bool _ownsMutex;
    private EventWaitHandle? _showExistingEvent;
    private EventWaitHandle? _exitExistingEvent;
    private RegisteredWaitHandle? _showRegistration;
    private RegisteredWaitHandle? _exitRegistration;
    private Forms.NotifyIcon? _tray;
    private FloatingWindow? _window;
    private FloatingViewModel? _floatingViewModel;
    private SetupWizardWindow? _wizard;
    private UsageMonitorService? _monitor;
    private LogService? _log;
    private StartupService? _startup;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
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
            var eventName = SingleInstancePolicy.EventForArguments(args) == "shutdown" ? ExitEventName : ShowEventName;
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
        _showRegistration = ThreadPool.RegisterWaitForSingleObject(_showExistingEvent, (_, _) => Dispatcher.BeginInvoke(ShowWindow), null, Timeout.Infinite, false);
        _exitRegistration = ThreadPool.RegisterWaitForSingleObject(_exitExistingEvent, (_, _) => Dispatcher.BeginInvoke(() => _ = ExitAsync()), null, Timeout.Infinite, false);
    }

    private void InitializeTray()
    {
        _startup = new StartupService();
        _tray = new Forms.NotifyIcon { Icon = LoadTrayIcon(), Visible = true, Text = "Codex 额度悬浮窗" };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示/隐藏", null, (_, _) => Toggle());
        menu.Items.Add("立即刷新", null, async (_, _) => { if (_monitor is not null) await _monitor.RefreshAsync(); });
        var startupItem = new Forms.ToolStripMenuItem("开机启动") { Checked = _startup.IsEnabled(), CheckOnClick = true };
        startupItem.CheckedChanged += (_, _) => { try { _startup.SetEnabled(startupItem.Checked); startupItem.Checked = _startup.IsEnabled(); } catch { startupItem.Checked = _startup.IsEnabled(); } };
        menu.Items.Add(startupItem);
        menu.Items.Add("配置 Codex CLI", null, (_, _) => ShowSetupWizard());
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
            var settings = await new SettingsService().LoadAsync();
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
            MainWindow = _window;
            _window.Closing += (_, args) => { args.Cancel = true; _window.Hide(); };
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
        if (_window.IsVisible) _window.Hide(); else ShowWindow();
    }

    private void ShowWindow()
    {
        if (_window is null) return;
        _window.Show();
        _window.Activate();
        _window.Topmost = true;
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

    private static string DescribeArguments(string[] args) => args.Contains("--shutdown", StringComparer.OrdinalIgnoreCase) ? "shutdown" : args.Contains("--startup", StringComparer.OrdinalIgnoreCase) ? "startup" : "normal";
    private static System.Drawing.Icon LoadTrayIcon() => System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty) ?? System.Drawing.SystemIcons.Information;

    protected override void OnExit(ExitEventArgs e)
    {
        _showRegistration?.Unregister(null);
        _exitRegistration?.Unregister(null);
        _showExistingEvent?.Dispose();
        _exitExistingEvent?.Dispose();
        if (_ownsMutex) _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        _tray?.Dispose();
        _log?.Write("Application exited");
        base.OnExit(e);
    }
}
