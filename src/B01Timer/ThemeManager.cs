using System.Windows;
using System.Windows.Media;

namespace B01Timer;

public static class ThemeManager
{
    public static string Current { get; private set; } = "dark";
    public static void Apply(string theme)
    {
        Current = theme == "light" ? "light" : "dark";
        bool dark = Current == "dark";
        var colors = new Dictionary<string, string>
        {
            ["Window"] = dark ? "#181C1F" : "#F7F8F5", ["Surface"] = dark ? "#20262A" : "#FFFFFF",
            ["Raised"] = dark ? "#282F33" : "#EDEFEA", ["Hover"] = dark ? "#333D42" : "#E3E8E1",
            ["Border"] = dark ? "#353E43" : "#DDE2DA", ["Text"] = dark ? "#F1F4EF" : "#202923",
            ["Muted"] = dark ? "#A5B1AA" : "#64716A", ["Disabled"] = dark ? "#68736D" : "#9AA59E",
            ["Accent"] = dark ? "#A9CDBA" : "#27624F", ["AccentForeground"] = dark ? "#1C3328" : "#FFFFFF",
            ["AccentHover"] = dark ? "#BDDCCB" : "#1E503F", ["Selected"] = dark ? "#2F4439" : "#DCEBE1",
            ["Error"] = dark ? "#F3A8A3" : "#AF3C37"
        };
        foreach (var (name, color) in colors) Application.Current.Resources[name + "Brush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }
}
