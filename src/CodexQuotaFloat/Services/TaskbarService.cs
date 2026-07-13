using System.Runtime.InteropServices;

namespace CodexQuotaFloat.Services;

public enum TaskbarEdge { Left = 0, Top = 1, Right = 2, Bottom = 3 }

public static class TaskbarService
{
    private const uint AbmGetState = 4;
    private const uint AbmGetTaskbarPos = 5;
    private const uint AbsAutoHide = 1;
    [StructLayout(LayoutKind.Sequential)] private struct AppBarData { public int CbSize; public nint Hwnd; public uint CallbackMessage; public uint Edge; public NativeRect Rect; public nint Param; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("shell32.dll")] private static extern uint SHAppBarMessage(uint message, ref AppBarData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string? className, string? windowName);

    public static (bool AutoHide, TaskbarEdge? Edge) GetState()
    {
        try
        {
            var data = new AppBarData { CbSize = Marshal.SizeOf<AppBarData>() };
            var taskbar = FindWindow("Shell_TrayWnd", null);
            var state = SHAppBarMessage(AbmGetState, ref data);
            var hasPosition = SHAppBarMessage(AbmGetTaskbarPos, ref data) != 0;
            return ((state & AbsAutoHide) != 0, hasPosition && data.Edge <= (uint)TaskbarEdge.Bottom ? (TaskbarEdge)data.Edge : null);
        }
        catch { return (false, null); }
    }
}
