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
    public static bool IsUsableCoordinate(double value) => IsFinite(value) && Math.Abs(value) <= 10_000_000;
    public static WpfSize GetEffectiveSize(double actualWidth, double width, double actualHeight, double height, bool expanded)
    {
        var fallbackWidth = 340d;
        var fallbackHeight = expanded ? 230d : 46d;
        return new(ValidDimension(actualWidth) ? actualWidth : ValidDimension(width) ? width : fallbackWidth,
            ValidDimension(actualHeight) ? actualHeight : ValidDimension(height) ? height : fallbackHeight);
    }
    private static bool ValidDimension(double value) => IsFinite(value) && value > 0 && value <= 4096;
    public static WpfPoint Restore(WpfPoint position, WpfSize size, WorkArea area)
    {
        if (!IsUsableCoordinate(position.X) || !IsUsableCoordinate(position.Y) || !ValidDimension(size.Width) || !ValidDimension(size.Height)) return DefaultPosition(new WpfSize(340, 46), area);
        var visible = Rect.Intersect(new Rect(position, size), area.Rect);
        if (visible.IsEmpty || (visible.Width < Math.Min(80, size.Width) && visible.Height < Math.Min(40, size.Height))) return DefaultPosition(size, area);
        return Clamp(position, size, area);
    }
    public static WpfPoint Snap(WpfPoint position, WpfSize size, WorkArea area)
    {
        var x = position.X; var y = position.Y;
        if (Math.Abs(x - area.Left) <= EdgeSnapDistance) x = area.Left;
        if (Math.Abs(area.Right - (x + size.Width)) <= EdgeSnapDistance) x = area.Right - size.Width;
        if (Math.Abs(y - area.Top) <= EdgeSnapDistance) y = area.Top;
        if (Math.Abs(area.Bottom - (y + size.Height)) <= EdgeSnapDistance) y = area.Bottom - size.Height;
        return Clamp(new WpfPoint(x, y), size, area);
    }
    public static WpfPoint Clamp(WpfPoint position, WpfSize size, WorkArea area) => new(Math.Clamp(position.X, area.Left, Math.Max(area.Left, area.Right - size.Width)), Math.Clamp(position.Y, area.Top, Math.Max(area.Top, area.Bottom - size.Height)));
    public static WpfPoint DefaultPosition(WpfSize size, WorkArea area) => new(Math.Max(area.Left, area.Right - size.Width - RestoreMargin), area.Top + RestoreMargin);
}
