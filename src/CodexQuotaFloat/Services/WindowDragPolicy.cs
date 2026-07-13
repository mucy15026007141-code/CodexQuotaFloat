using System.Windows.Controls;

namespace CodexQuotaFloat.Services;

public static class WindowDragPolicy
{
    public static bool IsInteractiveType(Type type) =>
        typeof(System.Windows.Controls.Primitives.ButtonBase).IsAssignableFrom(type) ||
        typeof(System.Windows.Controls.MenuItem).IsAssignableFrom(type) ||
        typeof(System.Windows.Controls.Primitives.TextBoxBase).IsAssignableFrom(type) ||
        typeof(System.Windows.Controls.PasswordBox).IsAssignableFrom(type) ||
        typeof(System.Windows.Controls.ComboBox).IsAssignableFrom(type) ||
        typeof(System.Windows.Controls.Slider).IsAssignableFrom(type) ||
        typeof(System.Windows.Controls.Primitives.ScrollBar).IsAssignableFrom(type) ||
        typeof(System.Windows.Documents.Hyperlink).IsAssignableFrom(type);
}
