using System.Text.Json;
using CodexQuotaFloat.Infrastructure;
using CodexQuotaFloat.Models;

namespace CodexQuotaFloat.Services;

public sealed class UsageMonitorService : IAsyncDisposable
{
    private readonly CodexExecutableLocator _locator = new(); private readonly LogService _log;
    private JsonRpcConnection? _connection; private CancellationTokenSource? _stop; private string? _plan;
    private Task? _loopTask; private readonly SemaphoreSlim _operationGate = new(1, 1); private bool _shuttingDown;
    public bool IsRefreshing { get; private set; }
    public event Action<UsageSnapshot>? Updated; public event Action<ConnectionState>? StateChanged;
    public UsageMonitorService(LogService log) => _log = log;
    public Task StartAsync()
    {
        if (_loopTask is not null) return Task.CompletedTask;
        _stop = new(); _loopTask = Task.Run(() => LoopAsync(_stop.Token)); return Task.CompletedTask;
    }
    public async Task ReconnectAsync(string reason)
    {
        await _operationGate.WaitAsync();
        try
        {
            if (_shuttingDown) return;
            _log.Write("Reconnect started: " + reason);
            _stop?.Cancel();
            if (_connection is not null) await _connection.DisposeAsync();
            if (_loopTask is not null) { try { await _loopTask; } catch { } }
            _connection = null; _stop?.Dispose(); _stop = new();
            _loopTask = Task.Run(() => LoopAsync(_stop.Token));
            _log.Write("Reconnect succeeded");
        }
        catch (Exception ex) { _log.Write("Reconnect failed: " + ex.GetType().Name); StateChanged?.Invoke(ConnectionState.Faulted); }
        finally { _operationGate.Release(); }
    }
    public void MarkOffline() { StateChanged?.Invoke(ConnectionState.Offline); }
    public async Task RefreshAsync()
    {
        if (!await _operationGate.WaitAsync(0)) return;
        try
        {
            if (_connection is null || _connection.HasAwaitingLateResponse("account/rateLimits/read")) return;
            IsRefreshing = true; StateChanged?.Invoke(ConnectionState.Refreshing);
            try { var limits = await _connection.RequestAsync("account/rateLimits/read", new { }, timeout: TimeSpan.FromSeconds(15), acceptLateResponse: true); PublishRateLimits(limits, isPartial: false); StateChanged?.Invoke(ConnectionState.Connected); _log.Write("Quota refresh succeeded."); }
            catch (Exception ex) { _log.Write("Quota refresh failed: " + ex.GetType().Name + "; " + ex.Message); StateChanged?.Invoke(ConnectionState.Stale); }
            finally { IsRefreshing = false; }
        }
        finally { _operationGate.Release(); }
    }
    private async Task LoopAsync(CancellationToken token)
    {
        var backoff = new[] { 2, 5, 10, 30, 60 }; var attempt = 0;
        while (!token.IsCancellationRequested)
            try
            {
                StateChanged?.Invoke(ConnectionState.Connecting); var exe = await _locator.FindAsync(); if (exe is null) { StateChanged?.Invoke(ConnectionState.CodexNotFound); return; }
                _connection = new JsonRpcConnection(exe); _connection.Notification += OnNotification; _connection.LateResponse += OnLateResponse; _connection.Diagnostic += OnDiagnostic; await _connection.StartAsync();
                var account = await _connection.RequestAsync("account/read", new { refreshToken = false }, token); AppServerResponseSchemaRecorder.Record(_log.DirectoryPath, "account/read", account); _plan = GetPlan(account); await RefreshAsync(); attempt = 0;
                while (!token.IsCancellationRequested) { await Task.Delay(TimeSpan.FromSeconds(60), token); await RefreshAsync(); }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.Write("Connection failure: " + ex.GetType().Name); StateChanged?.Invoke(ConnectionState.Faulted); await Task.Delay(TimeSpan.FromSeconds(backoff[Math.Min(attempt++, backoff.Length - 1)]), token); }
            finally { if (_connection is not null) await _connection.DisposeAsync(); _connection = null; }
    }
    private UsageSnapshot? _lastSnapshot;
    private void OnNotification(string method, JsonElement parameters)
    {
        AppServerResponseSchemaRecorder.Record(_log.DirectoryPath, "notification/" + method, parameters);
        if (method == "account/rateLimits/updated") { try { Publish(UsageParser.Parse(parameters, _plan), true); } catch { _ = RefreshAsync(); } }
    }
    private void OnLateResponse(string method, JsonElement response)
    {
        if (method != "account/rateLimits/read") return;
        try { PublishRateLimits(response, isPartial: false); StateChanged?.Invoke(ConnectionState.Connected); _log.Write("Delayed quota response applied."); }
        catch (Exception ex) { _log.Write("Delayed quota response failed: " + ex.GetType().Name); }
    }
    private void PublishRateLimits(JsonElement limits, bool isPartial)
    {
        AppServerResponseSchemaRecorder.Record(_log.DirectoryPath, "account/rateLimits/read", limits);
        Publish(UsageParser.Parse(limits, _plan), isPartial);
    }
    private void OnDiagnostic(JsonRpcDiagnostic diagnostic) => _log.Write($"App-server RPC: stage={diagnostic.Stage}; method={diagnostic.Method}; id={diagnostic.RequestId?.ToString() ?? "-"}; elapsedMs={diagnostic.ElapsedMs}; detail={diagnostic.Detail}");
    private void Publish(UsageSnapshot snapshot, bool isPartial)
    {
        if (isPartial && _lastSnapshot is { } previous)
        {
            if (snapshot.Weekly.Availability == RateLimitAvailability.Unavailable && previous.Weekly.Availability == RateLimitAvailability.Available)
                snapshot = snapshot with { Weekly = previous.Weekly };
            if (snapshot.FiveHour.Availability == RateLimitAvailability.Unavailable && previous.FiveHour.Availability == RateLimitAvailability.Available)
                snapshot = snapshot with { FiveHour = previous.FiveHour };
            if (snapshot.BankedResetCount is null && previous.BankedResetCount is not null)
                snapshot = snapshot with { BankedResetCount = previous.BankedResetCount };
        }
        _lastSnapshot = snapshot;
        LogStructure(snapshot);
        Updated?.Invoke(snapshot);
    }
    private void LogStructure(UsageSnapshot snapshot)
    {
        if (!snapshot.HasCodexBucket) { _log.Write("Rate-limit structure unsupported"); return; }
        _log.Write("Codex rate-limit bucket detected");
        _log.Write(snapshot.FiveHour.Availability == RateLimitAvailability.Available ? "Five-hour window available" : snapshot.FiveHour.Availability == RateLimitAvailability.Unlimited ? "Five-hour window absent; treating as unlimited" : "Five-hour window unavailable");
        _log.Write(snapshot.Weekly.Availability == RateLimitAvailability.Available ? "Weekly window available" : "Weekly window unavailable");
        _log.Write(snapshot.BankedResetCount is { } count ? $"Rate-limit reset credits available: {count}" : "Rate-limit reset credits unavailable");
        foreach (var minutes in snapshot.UnknownWindowDurations) _log.Write($"Unknown rate-limit window: {minutes}");
        if (!snapshot.IsStructureSupported) _log.Write("Rate-limit structure unsupported");
    }
    private static string? GetPlan(JsonElement account) => account.TryGetProperty("account", out var a) && a.TryGetProperty("planType", out var p) ? p.GetString() : null;
    public async ValueTask DisposeAsync() { _shuttingDown = true; _stop?.Cancel(); if (_connection is not null) await _connection.DisposeAsync(); if (_loopTask is not null) { try { await _loopTask; } catch { } } _stop?.Dispose(); _operationGate.Dispose(); }
}
