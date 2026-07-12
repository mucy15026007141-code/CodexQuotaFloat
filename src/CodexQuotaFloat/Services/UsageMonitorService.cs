using System.Text.Json;
using CodexQuotaFloat.Infrastructure;
using CodexQuotaFloat.Models;

namespace CodexQuotaFloat.Services;

public sealed class UsageMonitorService : IAsyncDisposable
{
    private readonly CodexExecutableLocator _locator = new(); private readonly LogService _log;
    private JsonRpcConnection? _connection; private CancellationTokenSource? _stop; private string? _plan;
    public event Action<UsageSnapshot>? Updated; public event Action<ConnectionState>? StateChanged;
    public UsageMonitorService(LogService log) => _log = log;
    public Task StartAsync() { _stop = new(); _ = Task.Run(() => LoopAsync(_stop.Token)); return Task.CompletedTask; }
    public async Task RefreshAsync()
    {
        if (_connection is null) return;
        StateChanged?.Invoke(ConnectionState.Refreshing);
        try { var limits = await _connection.RequestAsync("account/rateLimits/read", new { }); Updated?.Invoke(UsageParser.Parse(limits, _plan)); StateChanged?.Invoke(ConnectionState.Connected); _log.Write("Quota refresh succeeded."); } catch (Exception ex) { _log.Write("Quota refresh failed: " + ex.GetType().Name); StateChanged?.Invoke(ConnectionState.Stale); }
    }
    private async Task LoopAsync(CancellationToken token)
    {
        var backoff = new[] { 2, 5, 10, 30, 60 }; var attempt = 0;
        while (!token.IsCancellationRequested)
            try
            {
                StateChanged?.Invoke(ConnectionState.Connecting); var exe = await _locator.FindAsync(); if (exe is null) { StateChanged?.Invoke(ConnectionState.CodexNotFound); return; }
                _connection = new JsonRpcConnection(exe); _connection.Notification += OnNotification; await _connection.StartAsync();
                var account = await _connection.RequestAsync("account/read", new { refreshToken = false }, token); _plan = GetPlan(account); await RefreshAsync(); attempt = 0;
                while (!token.IsCancellationRequested) { await Task.Delay(TimeSpan.FromSeconds(60), token); await RefreshAsync(); }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.Write("Connection failure: " + ex.GetType().Name); StateChanged?.Invoke(ConnectionState.Faulted); await Task.Delay(TimeSpan.FromSeconds(backoff[Math.Min(attempt++, backoff.Length - 1)]), token); }
            finally { if (_connection is not null) await _connection.DisposeAsync(); _connection = null; }
    }
    private void OnNotification(string method, JsonElement parameters) { if (method == "account/rateLimits/updated") { try { Updated?.Invoke(UsageParser.Parse(parameters, _plan)); } catch { _ = RefreshAsync(); } } }
    private static string? GetPlan(JsonElement account) => account.TryGetProperty("account", out var a) && a.TryGetProperty("planType", out var p) ? p.GetString() : null;
    public async ValueTask DisposeAsync() { _stop?.Cancel(); if (_connection is not null) await _connection.DisposeAsync(); _stop?.Dispose(); }
}
