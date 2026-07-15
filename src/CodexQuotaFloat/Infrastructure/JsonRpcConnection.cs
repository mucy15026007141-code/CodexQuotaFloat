using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.IO;

namespace CodexQuotaFloat.Infrastructure;

public sealed record JsonRpcDiagnostic(string Stage, string Method, int? RequestId, long ElapsedMs, string Detail);

public sealed class JsonRpcConnection : IAsyncDisposable
{
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly ConcurrentDictionary<int, string> _latePending = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly string _executable;
    private Process? _process;
    private int _id;
    public event Action<string, JsonElement>? Notification;
    public event Action<string, JsonElement>? LateResponse;
    public event Action<JsonRpcDiagnostic>? Diagnostic;

    public JsonRpcConnection(string executable) => _executable = executable;

    public async Task StartAsync()
    {
        var cmd = _executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase);
        var psi = cmd
            ? new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec")!, $"/d /s /c \"\"{_executable}\" app-server --listen stdio://\"")
            : new ProcessStartInfo(_executable, "app-server --listen stdio://");
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardInput = psi.RedirectStandardOutput = psi.RedirectStandardError = true;
        _process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start Codex App Server.");
        _ = Task.Run(ReadLoopAsync);
        _ = Task.Run(ReadErrorsAsync);
        await RequestAsync("initialize", new { clientInfo = new { name = "codex_quota_float", title = "Codex Quota Float", version = "1.3.0" } }, timeout: TimeSpan.FromSeconds(30));
        await NotifyAsync("initialized", new { });
    }

    public async Task<JsonElement> RequestAsync(string method, object parameters, CancellationToken cancellationToken = default, TimeSpan? timeout = null, bool acceptLateResponse = false)
    {
        if (_process is null) throw new InvalidOperationException("Not connected.");
        var id = Interlocked.Increment(ref _id);
        var started = Stopwatch.GetTimestamp();
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;
        try
        {
            await SendAsync(new { method, id, @params = parameters });
            Report("sent", method, id, started, "request");
            using var requestStop = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token, cancellationToken);
            requestStop.CancelAfter(timeout ?? TimeSpan.FromSeconds(30));
            using var registration = requestStop.Token.Register(() => tcs.TrySetCanceled(requestStop.Token));
            var result = await tcs.Task;
            Report("received", method, id, started, "result");
            return result;
        }
        catch (OperationCanceledException)
        {
            if (acceptLateResponse) _latePending[id] = method;
            Report("timeout", method, id, started, $"timeoutMs={(timeout ?? TimeSpan.FromSeconds(30)).TotalMilliseconds:F0}");
            throw;
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    public Task NotifyAsync(string method, object parameters) => SendAsync(new { method, @params = parameters });

    private async Task SendAsync(object value)
    {
        await _process!.StandardInput.WriteLineAsync(JsonSerializer.Serialize(value));
        await _process.StandardInput.FlushAsync();
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested && await _process!.StandardOutput.ReadLineAsync(_stop.Token) is { } line)
            {
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement.Clone();
                    if (root.TryGetProperty("id", out var id) && id.TryGetInt32(out var value))
                    {
                        if (_pending.TryGetValue(value, out var tcs))
                        {
                            if (root.TryGetProperty("error", out var error)) tcs.TrySetException(new InvalidOperationException(DescribeError(error)));
                            else if (root.TryGetProperty("result", out var result)) tcs.TrySetResult(result);
                            else tcs.TrySetException(new InvalidOperationException("App Server response has neither result nor error."));
                        }
                        else if (_latePending.TryRemove(value, out var lateMethod) && root.TryGetProperty("result", out var lateResult))
                        {
                            Report("late-response", lateMethod, value, Stopwatch.GetTimestamp(), "result");
                            LateResponse?.Invoke(lateMethod, lateResult);
                        }
                        else Report("unmatched-response", string.Empty, value, Stopwatch.GetTimestamp(), "no-pending-request");
                    }
                    else if (root.TryGetProperty("method", out var method))
                    {
                        var name = method.GetString() ?? string.Empty;
                        Report("notification", name, null, Stopwatch.GetTimestamp(), "received");
                        Notification?.Invoke(name, root.TryGetProperty("params", out var parameters) ? parameters : default);
                    }
                    else Report("unknown-envelope", string.Empty, null, Stopwatch.GetTimestamp(), DescribeEnvelope(root));
                }
                catch (JsonException)
                {
                    Report("invalid-json", string.Empty, null, Stopwatch.GetTimestamp(), "stdout-message-not-json");
                }
            }
        }
        catch (Exception ex)
        {
            Report("read-loop-ended", string.Empty, null, Stopwatch.GetTimestamp(), ex.GetType().Name);
        }
        finally
        {
            foreach (var item in _pending) item.Value.TrySetException(new IOException("App Server disconnected."));
        }
    }

    private async Task ReadErrorsAsync()
    {
        try
        {
            while (await _process!.StandardError.ReadLineAsync(_stop.Token) is { } line)
                Report("stderr", string.Empty, null, Stopwatch.GetTimestamp(), Redact(line));
        }
        catch { }
    }

    private void Report(string stage, string method, int? requestId, long started, string detail) => Diagnostic?.Invoke(new(stage, method, requestId, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, Redact(detail)));
    public bool HasAwaitingLateResponse(string method) => _latePending.Values.Any(value => string.Equals(value, method, StringComparison.Ordinal));
    private static string DescribeEnvelope(JsonElement root) => string.Join(',', root.EnumerateObject().Select(property => property.Name));
    private static string DescribeError(JsonElement error)
    {
        var code = error.TryGetProperty("code", out var codeValue) ? codeValue.ToString() : "unknown";
        var message = error.TryGetProperty("message", out var messageValue) ? messageValue.GetString() ?? "unknown" : "unknown";
        return $"App Server error code={code}; message={Redact(message)}";
    }
    private static string Redact(string value) => System.Text.RegularExpressions.Regex.Replace(value, @"(?i)(token|cookie|authorization|email|account[_ -]?id)\s*[:=]\s*[^\s,;]+", "$1=[redacted]");

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        if (_process is not null)
        {
            try
            {
                _process.StandardInput.Close();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try { await _process.WaitForExitAsync(timeout.Token); }
                catch (OperationCanceledException) { _process.Kill(true); }
            }
            catch { }
            _process.Dispose();
        }
        _stop.Dispose();
    }
}
