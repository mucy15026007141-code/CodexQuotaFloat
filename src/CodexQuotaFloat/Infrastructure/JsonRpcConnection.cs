using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.IO;

namespace CodexQuotaFloat.Infrastructure;

public sealed class JsonRpcConnection : IAsyncDisposable
{
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly string _executable;
    private Process? _process; private int _id;
    public event Action<string, JsonElement>? Notification;
    public JsonRpcConnection(string executable) => _executable = executable;
    public async Task StartAsync()
    {
        var cmd = _executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase);
        var psi = cmd ? new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec")!, $"/d /s /c \"\"{_executable}\" app-server --listen stdio://\"") : new ProcessStartInfo(_executable, "app-server --listen stdio://");
        psi.UseShellExecute = false; psi.CreateNoWindow = true; psi.RedirectStandardInput = psi.RedirectStandardOutput = psi.RedirectStandardError = true;
        _process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start Codex App Server.");
        _ = Task.Run(ReadLoopAsync); _ = Task.Run(ReadErrorsAsync);
        await RequestAsync("initialize", new { clientInfo = new { name = "codex_quota_float", title = "Codex Quota Float", version = "1.1.2" } });
        await NotifyAsync("initialized", new { });
    }
    public async Task<JsonElement> RequestAsync(string method, object parameters, CancellationToken cancellationToken = default)
    {
        if (_process is null) throw new InvalidOperationException("Not connected.");
        var id = Interlocked.Increment(ref _id); var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously); _pending[id] = tcs;
        await SendAsync(new { method, id, @params = parameters });
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token, cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(15));
        await using var _ = timeout.Token.Register(() => tcs.TrySetCanceled(timeout.Token));
        return await tcs.Task;
    }
    public Task NotifyAsync(string method, object parameters) => SendAsync(new { method, @params = parameters });
    private async Task SendAsync(object value) { await _process!.StandardInput.WriteLineAsync(JsonSerializer.Serialize(value)); await _process.StandardInput.FlushAsync(); }
    private async Task ReadLoopAsync()
    {
        try { while (!_stop.IsCancellationRequested && await _process!.StandardOutput.ReadLineAsync(_stop.Token) is { } line) { try { using var doc = JsonDocument.Parse(line); var root = doc.RootElement.Clone(); if (root.TryGetProperty("id", out var id) && id.TryGetInt32(out var value) && _pending.TryRemove(value, out var tcs)) { if (root.TryGetProperty("error", out _)) tcs.TrySetException(new InvalidOperationException("App Server request failed.")); else tcs.TrySetResult(root.GetProperty("result")); } else if (root.TryGetProperty("method", out var method)) Notification?.Invoke(method.GetString()!, root.TryGetProperty("params", out var p) ? p : default); } catch (JsonException) { } } } catch { } finally { foreach (var item in _pending) item.Value.TrySetException(new IOException("App Server disconnected.")); }
    }
    private async Task ReadErrorsAsync() { try { while (await _process!.StandardError.ReadLineAsync(_stop.Token) is not null) { } } catch { } }
    public async ValueTask DisposeAsync() { _stop.Cancel(); if (_process is not null) { try { _process.StandardInput.Close(); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)); try { await _process.WaitForExitAsync(timeout.Token); } catch (OperationCanceledException) { _process.Kill(true); } } catch { } _process.Dispose(); } _stop.Dispose(); }
}
