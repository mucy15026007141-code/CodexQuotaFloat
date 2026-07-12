using System.Threading;
using System.Windows;
using Forms = System.Windows.Forms;
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
    private UsageMonitorService? _monitor;
    private LogService? _log;
    private StartupService? _startup;

    protected override async void OnStartup(StartupEventArgs e)
    {
        _instanceMutex = new Mutex(initiallyOwned: false, MutexName);
        try { _ownsMutex = _instanceMutex.WaitOne(0); }
        catch (AbandonedMutexException) { _ownsMutex = true; }
        if (!_ownsMutex)
        {
            try
            {
                var eventName = e.Args.Contains("--shutdown", StringComparer.OrdinalIgnoreCase) ? ExitEventName : ShowEventName;
                using var existingEvent = EventWaitHandle.OpenExisting(eventName);
                existingEvent.Set();
            }
            catch { }
            Shutdown(); return;
        }

        base.OnStartup(e);
        _showExistingEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _exitExistingEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ExitEventName);
        _showRegistration = ThreadPool.RegisterWaitForSingleObject(_showExistingEvent, (_, _) => Dispatcher.Invoke(ShowWindow), null, Timeout.Infinite, false);
        _exitRegistration = ThreadPool.RegisterWaitForSingleObject(_exitExistingEvent, (_, _) => Dispatcher.Invoke(() => _ = ExitAsync()), null, Timeout.Infinite, false);
        _log = new LogService(); _log.Write("Application started."); _startup = new StartupService(); _monitor = new UsageMonitorService(_log);
        _window = new FloatingWindow { DataContext = new FloatingViewModel(_monitor) };
        _window.Closing += (_, args) => { args.Cancel = true; _window.Hide(); };
        _tray = new Forms.NotifyIcon { Icon = LoadTrayIcon(), Visible = true, Text = "Codex 额度悬浮窗" };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示/隐藏", null, (_, _) => Toggle());
        menu.Items.Add("立即刷新", null, async (_, _) => await _monitor.RefreshAsync());
        var startupItem = new Forms.ToolStripMenuItem("开机启动") { Checked = _startup.IsEnabled(), CheckOnClick = true };
        startupItem.CheckedChanged += (_, _) => { try { _startup.SetEnabled(startupItem.Checked); startupItem.Checked = _startup.IsEnabled(); } catch { startupItem.Checked = _startup.IsEnabled(); } };
        menu.Items.Add(startupItem);
        menu.Items.Add("配置 Codex CLI", null, async (_, _) => await OpenSetupWizardAsync());
        menu.Items.Add("打开日志目录", null, (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", _log.DirectoryPath) { UseShellExecute = true }));
        menu.Items.Add("退出", null, async (_, _) => await ExitAsync());
        _tray.ContextMenuStrip = menu; _tray.DoubleClick += (_, _) => ShowWindow();

        var settingsService = new SettingsService();
        var settings = await settingsService.LoadAsync();
        if (!settings.SetupCompleted)
        {
            var result = await new CodexEnvironmentService().CheckAsync();
            settings.LastDetectedCodexVersion = result.CliVersion;
            settings.LastSetupCheckTime = DateTimeOffset.Now;
            if (result.IsReady)
            {
                settings.SetupCompleted = true;
                await settingsService.SaveAsync(settings);
            }
            else
            {
                var viewModel = new SetupWizardViewModel(new CodexEnvironmentService());
                viewModel.SetResult(result);
                viewModel.OpenLogsRequested += () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", _log.DirectoryPath) { UseShellExecute = true });
                viewModel.SetupSucceeded += async () =>
                {
                    settings.SetupCompleted = true;
                    settings.LastDetectedCodexVersion = viewModel.Result.CliVersion;
                    settings.LastSetupCheckTime = DateTimeOffset.Now;
                    await settingsService.SaveAsync(settings);
                };
                new SetupWizardWindow(viewModel).ShowDialog();
            }
        }
        _window.Show();
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        return System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty) ?? System.Drawing.SystemIcons.Information;
    }
    private void Toggle() { if (_window!.IsVisible) _window.Hide(); else ShowWindow(); }
    private void ShowWindow() { _window!.Show(); _window.Activate(); _window.Topmost = true; }
    private async Task OpenSetupWizardAsync()
    {
        if (_log is null) return;
        var settingsService = new SettingsService();
        var settings = await settingsService.LoadAsync();
        var viewModel = new SetupWizardViewModel(new CodexEnvironmentService());
        viewModel.OpenLogsRequested += () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", _log.DirectoryPath) { UseShellExecute = true });
        viewModel.SetupSucceeded += async () =>
        {
            settings.SetupCompleted = true;
            settings.LastDetectedCodexVersion = viewModel.Result.CliVersion;
            settings.LastSetupCheckTime = DateTimeOffset.Now;
            await settingsService.SaveAsync(settings);
            await _monitor!.RefreshAsync();
        };
        new SetupWizardWindow(viewModel).ShowDialog();
    }
    private async Task ExitAsync() { _tray!.Visible = false; await _monitor!.DisposeAsync(); Shutdown(); }
    protected override void OnExit(ExitEventArgs e)
    {
        _showRegistration?.Unregister(null); _exitRegistration?.Unregister(null); _showExistingEvent?.Dispose(); _exitExistingEvent?.Dispose(); if (_ownsMutex) _instanceMutex?.ReleaseMutex(); _instanceMutex?.Dispose(); _tray?.Dispose(); _log?.Write("Application exited."); base.OnExit(e);
    }
}
