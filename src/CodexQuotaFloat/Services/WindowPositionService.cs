using System.Windows;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

namespace CodexQuotaFloat.Services;

public readonly record struct WorkArea(double Left, double Top, double Right, double Bottom)
{
    public double Width => Math.Max(0, Right - Left);
    public double Height => Math.Max(0, Bottom - Top);
    public Rect Rect => new(Left, Top, Width, Height);
}

public static class WindowPositionService
{
    public const double EdgeSnapDistance = 16;
    public const double RestoreMargin = 20;
    public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    public static WpfPoint Restore(WpfPoint position, WpfSize size, WorkArea area)
    {
        if (!IsFinite(position.X) || !IsFinite(position.Y) || !IsFinite(size.Width) || !IsFinite(size.Height)) return DefaultPosition(size, area);
        var visible = Rect.Intersect(new Rect(position, size), area.Rect);
        if (visible.IsEmpty || (visible.Width < Math.Min(80, size.Width) && visible.Height < Math.Min(40, size.Height))) return DefaultPosition(size, area);
        return Clamp(position, size, area);
    }
    public static WpfPoint Snap(WpfPoint position, WpfSize size, WorkArea area)
    {
        var x = position.X; var y = position.Y;
        if (Math.Abs(x - area.Left) <= EdgeSnapDistance) x = area.Left + RestoreMargin;
        if (Math.Abs(area.Right - (x + size.Width)) <= EdgeSnapDistance) x = area.Right - size.Width - RestoreMargin;
        if (Math.Abs(y - area.Top) <= EdgeSnapDistance) y = area.Top + RestoreMargin;
        if (Math.Abs(area.Bottom - (y + size.Height)) <= EdgeSnapDistance) y = area.Bottom - size.Height - RestoreMargin;
        return Clamp(new WpfPoint(x, y), size, area);
    }
    public static WpfPoint Clamp(WpfPoint position, WpfSize size, WorkArea area) => new(Math.Clamp(position.X, area.Left, Math.Max(area.Left, area.Right - size.Width)), Math.Clamp(position.Y, area.Top, Math.Max(area.Top, area.Bottom - size.Height)));
    public static WpfPoint DefaultPosition(WpfSize size, WorkArea area) => new(Math.Max(area.Left, area.Right - size.Width - RestoreMargin), area.Top + RestoreMargin);
}
