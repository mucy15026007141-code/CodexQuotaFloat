using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using CodexQuotaFloat.Models;
using CodexQuotaFloat.Services;

namespace CodexQuotaFloat.ViewModels;

public sealed class FloatingViewModel : INotifyPropertyChanged
{
    private readonly UsageMonitorService _monitor;
    private UsageSnapshot? _snapshot;
    private ConnectionState _state = ConnectionState.Starting;
    private bool _isExpanded;
    private bool _isRefreshing;
    public event PropertyChangedEventHandler? PropertyChanged;

    public FloatingViewModel(UsageMonitorService monitor)
    {
        _monitor = monitor;
        _monitor.Updated += snapshot => OnUi(() => { _snapshot = snapshot; RaiseAll(); });
        _monitor.StateChanged += state => OnUi(() => { _state = state; RaiseAll(); });
        RefreshCommand = new AsyncCommand(RefreshAsync, () => !IsRefreshing);
        ToggleExpandedCommand = new RelayCommand(() => { IsExpanded = !IsExpanded; });
        _ = _monitor.StartAsync(); _ = TickAsync();
    }

    public ICommand RefreshCommand { get; }
    public ICommand ToggleExpandedCommand { get; }
    public bool IsExpanded { get => _isExpanded; private set { if (_isExpanded == value) return; _isExpanded = value; Raise(nameof(IsExpanded)); Raise(nameof(ExpandButtonText)); } }
    public bool IsRefreshing { get => _isRefreshing; private set { if (_isRefreshing == value) return; _isRefreshing = value; Raise(nameof(IsRefreshing)); Raise(nameof(RefreshButtonText)); ((AsyncCommand)RefreshCommand).NotifyCanExecuteChanged(); } }
    public int FivePercent => _snapshot?.FiveHour?.RemainingPercent ?? 0;
    public int WeekPercent => _snapshot?.Weekly?.RemainingPercent ?? 0;
    public string FiveCompactPercent => _snapshot?.FiveHour is { } window ? $"{window.RemainingPercent}%" : "--";
    public string WeekCompactPercent => _snapshot?.Weekly is { } window ? $"{window.RemainingPercent}%" : "--";
    public string CompactTitle => _state is ConnectionState.CodexNotFound or ConnectionState.NotLoggedIn or ConnectionState.UnsupportedAccount ? "Codex 需要配置" : "Codex 剩余额度";
    public string FiveCountdown => _snapshot?.FiveHour is { } window ? Until(window) : "暂不可用";
    public string WeekCountdown => _snapshot?.Weekly is { } window ? Until(window) : "暂不可用";
    public string FiveResetAt => FormatReset(_snapshot?.FiveHour);
    public string WeekResetAt => FormatReset(_snapshot?.Weekly);
    public string Plan => _snapshot?.PlanType ?? "暂不可用";
    public string Status => _state switch { ConnectionState.Connected => "已连接", ConnectionState.Refreshing => "正在刷新", ConnectionState.CodexNotFound => "未找到 Codex CLI", ConnectionState.NotLoggedIn => "Codex 尚未登录", ConnectionState.Stale => "数据可能已过期", ConnectionState.Faulted => "连接失败", _ => "正在连接" };
    public string UpdatedTime => _snapshot is null ? "未更新" : $"{_snapshot.RetrievedAt:HH:mm} 更新";
    public string LastSuccessfulUpdate => _snapshot is null ? "暂不可用" : _snapshot.RetrievedAt.ToString("HH:mm:ss");
    public string ExpandButtonText => IsExpanded ? "收起 ˄" : "展开 ˅";
    public string RefreshButtonText => IsRefreshing ? "刷新中…" : "刷新";

    private async Task RefreshAsync() { IsRefreshing = true; try { await _monitor.RefreshAsync(); } finally { IsRefreshing = false; } }
    private async Task TickAsync() { while (true) { await Task.Delay(1000); OnUi(RaiseAll); } }
    private static string Until(RateLimitWindow window) => window.ResetLocal is { } reset ? (reset - DateTimeOffset.Now) switch { var span when span.TotalSeconds <= 0 => "即将刷新", var span when span.TotalDays >= 1 => $"{(int)span.TotalDays}天{span.Hours}小时后刷新", var span => $"{span.Hours}小时{span.Minutes}分钟后刷新" } : "刷新时间不可用";
    private static string FormatReset(RateLimitWindow? window) => window?.ResetLocal is { } reset ? (reset.Date == DateTimeOffset.Now.Date ? $"今天 {reset:HH:mm}" : $"{reset:M月d日 HH:mm}") : "暂不可用";
    private void OnUi(Action action) { if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == true) action(); else System.Windows.Application.Current?.Dispatcher?.Invoke(action); }
    private void RaiseAll() { foreach (var property in new[] { nameof(FivePercent), nameof(WeekPercent), nameof(FiveCompactPercent), nameof(WeekCompactPercent), nameof(CompactTitle), nameof(FiveCountdown), nameof(WeekCountdown), nameof(FiveResetAt), nameof(WeekResetAt), nameof(Plan), nameof(Status), nameof(UpdatedTime), nameof(LastSuccessfulUpdate) }) Raise(property); }
    private void Raise(string property) => PropertyChanged?.Invoke(this, new(property));
}

public sealed class RelayCommand(Action execute) : ICommand { public event EventHandler? CanExecuteChanged { add { } remove { } } public bool CanExecute(object? _) => true; public void Execute(object? _) => execute(); }
public sealed class AsyncCommand(Func<Task> execute, Func<bool> canExecute) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? _) => canExecute();
    public async void Execute(object? _) => await execute();
    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
