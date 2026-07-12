using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using CodexQuotaFloat.Models;
using CodexQuotaFloat.Services;

namespace CodexQuotaFloat.ViewModels;

public sealed class SetupWizardViewModel : INotifyPropertyChanged
{
    public const string OfficialInstallCommand = "powershell -ExecutionPolicy ByPass -c \"irm https://chatgpt.com/codex/install.ps1 | iex\"";
    private readonly CodexEnvironmentService _environment;
    private SetupCheckResult _result = new(SetupStatus.Checking);
    private bool _checking;

    public SetupWizardViewModel(CodexEnvironmentService environment)
    {
        _environment = environment;
        CopyInstallCommandCommand = new RelayCommand(CopyInstallCommand);
        OpenPowerShellCommand = new RelayCommand(() => Process.Start(new ProcessStartInfo("powershell.exe") { UseShellExecute = true }));
        StartLoginCommand = new RelayCommand(() => Process.Start(new ProcessStartInfo("cmd.exe", "/k codex login") { UseShellExecute = true }));
        RetryCommand = new AsyncCommand(CheckAsync, () => !IsChecking);
        OpenLogsCommand = new RelayCommand(() => OpenLogsRequested?.Invoke());
        CopyDiagnosticsCommand = new RelayCommand(() => System.Windows.Clipboard.SetText(DiagnosticInfo.Create(Result)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? OpenLogsRequested;
    public event Action? SetupSucceeded;
    public SetupCheckResult Result { get => _result; private set { _result = value; RaiseAll(); } }
    public bool IsChecking { get => _checking; private set { _checking = value; Raise(nameof(IsChecking)); ((AsyncCommand)RetryCommand).NotifyCanExecuteChanged(); } }
    public string Title => Result.Status switch { SetupStatus.Checking => "正在检查 Codex CLI", SetupStatus.CodexNotFound => "需要安装 Codex CLI", SetupStatus.NotLoggedIn => "需要登录 Codex", SetupStatus.ApiKeyMode => "当前使用 API Key 模式", SetupStatus.VersionTooOld => "需要更新 Codex CLI", _ => "Codex CLI 连接状态" };
    public string Message => Result.Status switch
    {
        SetupStatus.Checking => "正在检查本机 Codex CLI、登录状态和额度接口，请稍候。",
        SetupStatus.CodexNotFound => "本工具通过你电脑上的 Codex CLI读取你自己的额度，不会读取开发者账号。",
        SetupStatus.NotLoggedIn => "请使用你自己的 ChatGPT账号登录。额度悬浮窗不会接触或保存你的密码。",
        SetupStatus.ApiKeyMode => "当前使用API Key模式。API Key按API用量计费，可能没有ChatGPT套餐的5小时和每周额度，因此悬浮窗无法显示相同的额度数据。",
        SetupStatus.VersionTooOld => "检测到的 Codex CLI 版本低于 0.144.1。请按官方方式更新后重新检测。",
        SetupStatus.IncompleteQuotaData => "额度接口未返回完整的5小时和每周数据，请确认使用 ChatGPT账号登录后重试。",
        _ => "找到 Codex CLI，但连接失败或额度接口暂不可用。可重新检测，或复制脱敏诊断信息查看日志。"
    };
    public bool ShowInstallActions => Result.Status is SetupStatus.CodexNotFound or SetupStatus.VersionTooOld;
    public bool ShowLoginAction => Result.Status is SetupStatus.NotLoggedIn or SetupStatus.ApiKeyMode;
    public bool ShowDiagnostics => Result.Status is SetupStatus.ConnectionFailed or SetupStatus.IncompleteQuotaData;
    public ICommand CopyInstallCommandCommand { get; }
    public ICommand OpenPowerShellCommand { get; }
    public ICommand StartLoginCommand { get; }
    public ICommand RetryCommand { get; }
    public ICommand OpenLogsCommand { get; }
    public ICommand CopyDiagnosticsCommand { get; }

    public async Task CheckAsync()
    {
        IsChecking = true;
        try { Result = await _environment.CheckAsync(); if (Result.IsReady) SetupSucceeded?.Invoke(); }
        finally { IsChecking = false; }
    }

    public void SetResult(SetupCheckResult result)
    {
        Result = result;
        if (result.IsReady) SetupSucceeded?.Invoke();
    }

    private static void CopyInstallCommand() => System.Windows.Clipboard.SetText(OfficialInstallCommand);
    private void RaiseAll()
    {
        foreach (var name in new[] { nameof(Result), nameof(Title), nameof(Message), nameof(ShowInstallActions), nameof(ShowLoginAction), nameof(ShowDiagnostics) }) Raise(name);
    }
    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
