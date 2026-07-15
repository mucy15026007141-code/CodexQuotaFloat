using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace CodexQuotaFloat.Services;

public static class WindowTopmostPolicy
{
    public const uint SwpNoSize = 0x0001;
    public const uint SwpNoMove = 0x0002;
    public const uint SwpNoActivate = 0x0010;
    public const uint SwpNoOwnerZOrder = 0x0200;
    public const uint ApplyFlags = SwpNoSize | SwpNoMove | SwpNoActivate | SwpNoOwnerZOrder;
    public const nint HwndTopmost = -1;
    public const nint HwndNotTopmost = -2;
    public const nint GwlExStyle = -20;
    public const nint WsExTopmost = 0x00000008;

    public static bool HasNoZOrderFlag(uint flags) => (flags & 0x0004) != 0;
    public static bool IsActuallyTopmost(nint exStyle) => (exStyle & WsExTopmost) != 0;
    public static IReadOnlyList<nint> RepairSequence() => [HwndNotTopmost, HwndTopmost];
}

public sealed class WindowTopmostService
{
    private const uint GwHwndPrev = 3;
    private readonly LogService _log;
    private DispatcherTimer? _deactivatedDebounce;
    private RepairRequest? _pendingRepair;
    private bool _isExiting;

    public WindowTopmostService(LogService log) => _log = log;

    public void SetExiting() => _isExiting = true;

    public void ApplyAsync(Window window, bool enabled, string reason)
    {
        if (_isExiting) return;
        _ = window.Dispatcher.BeginInvoke(() => Apply(window, enabled, reason), DispatcherPriority.ApplicationIdle);
    }

    public async Task<bool> ApplyAfterStartupReadyAsync(Window window, bool enabled, string reason)
    {
        if (_isExiting) return false;
        var first = await window.Dispatcher.InvokeAsync(() => Apply(window, enabled, reason), DispatcherPriority.ApplicationIdle);
        await Task.Delay(75);
        var verified = await window.Dispatcher.InvokeAsync(() => Verify(window, reason + ":delayed"), DispatcherPriority.ApplicationIdle);
        return first && verified == enabled;
    }

    public void RepairIfNeeded(Window window, bool enabled, string reason)
    {
        if (_isExiting || !enabled || !window.IsVisible) return;
        _deactivatedDebounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _deactivatedDebounce.Stop();
        _deactivatedDebounce.Tick -= DeactivatedDebounceTick;
        _deactivatedDebounce.Tick += DeactivatedDebounceTick;
        _pendingRepair = new RepairRequest(window, reason);
        _deactivatedDebounce.Start();
    }

    private void DeactivatedDebounceTick(object? sender, EventArgs e)
    {
        _deactivatedDebounce?.Stop();
        if (_pendingRepair is RepairRequest request)
        {
            _pendingRepair = null;
            Apply(request.Window, enabled: true, request.Reason);
        }
    }

    public bool Verify(Window window, string reason)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero) return false;
        var topmost = WindowTopmostPolicy.IsActuallyTopmost(GetWindowLongPtr(handle, WindowTopmostPolicy.GwlExStyle));
        LogState(window, handle, topmost, reason);
        return topmost;
    }

    private bool Apply(Window window, bool enabled, string reason)
    {
        if (_isExiting || !window.IsLoaded) return false;
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero) return false;

        window.Topmost = enabled;
        var insertAfter = enabled ? WindowTopmostPolicy.HwndTopmost : WindowTopmostPolicy.HwndNotTopmost;
        var wasActuallyTopmost = Verify(window, reason + ":before");
        var applied = SetWindowPos(handle, insertAfter, 0, 0, 0, 0, WindowTopmostPolicy.ApplyFlags);
        var isActuallyTopmost = Verify(window, reason + ":after");
        var verificationSucceeded = isActuallyTopmost == enabled;
        _log.Write($"Topmost apply: reason={reason}; enabled={enabled}; setWindowPos={applied}; verification={verificationSucceeded}");

        if (enabled && (!wasActuallyTopmost || !isActuallyTopmost))
        {
            _log.Write($"Topmost repair required: reason={reason}");
            SetWindowPos(handle, WindowTopmostPolicy.HwndNotTopmost, 0, 0, 0, 0, WindowTopmostPolicy.ApplyFlags);
            var repaired = SetWindowPos(handle, WindowTopmostPolicy.HwndTopmost, 0, 0, 0, 0, WindowTopmostPolicy.ApplyFlags) && Verify(window, reason + ":repair");
            _log.Write($"Topmost repair {(repaired ? "succeeded" : "failed")}: reason={reason}");
            return repaired;
        }
        return verificationSucceeded;
    }

    private void LogState(Window window, nint handle, bool topmost, string reason)
    {
        GetWindowRect(handle, out var rect);
        var previous = GetWindow(handle, GwHwndPrev) != nint.Zero;
        _log.Write($"Topmost diagnostic: reason={reason}; requested={window.Topmost}; wpfTopmost={window.Topmost}; wsExTopmost={topmost}; visible={window.IsVisible}; rect={rect.Left},{rect.Top},{rect.Right},{rect.Bottom}; foreground={ForegroundProcessName()}; zOrderPreviousPresent={previous}");
    }

    private static string ForegroundProcessName()
    {
        try
        {
            var foreground = GetForegroundWindow();
            if (foreground == nint.Zero) return "none";
            GetWindowThreadProcessId(foreground, out var processId);
            return processId == 0 ? "unknown" : Process.GetProcessById((int)processId).ProcessName;
        }
        catch { return "unknown"; }
    }

    private readonly record struct RepairRequest(Window Window, string Reason);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hWnd, nint index);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hWnd, uint command);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hWnd, out NativeRect rect);
}
