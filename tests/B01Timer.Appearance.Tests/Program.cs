using B01Timer;
using B01Timer.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using GlyphPath = System.Windows.Shapes.Path;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        int checks = 0;
        void Check(bool result, string name) { if (!result) throw new Exception(name); checks++; Console.WriteLine($"PASS {name}"); }
        string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "b01-appearance-" + Guid.NewGuid().ToString("N"));
        var app = new App(); app.InitializeComponent(); ThemeManager.Apply("dark");
        var window = new MainWindow(new AppSettings { LastSeconds = 900 }, new SettingsStore(folder));
        try
        {
            window.Show(); window.UpdateLayout();
            var numbers = (TextBox)((DurationEditor)window.FindName("TimeEditor")).FindName("HoursInput");
            Check(new Typeface(numbers.FontFamily, numbers.FontStyle, numbers.FontWeight, numbers.FontStretch).TryGetGlyphTypeface(out var regular)
                && regular.FontUri.OriginalString.Contains("pretendard-regular.otf", StringComparison.OrdinalIgnoreCase), "Timer digits resolve to the embedded Pretendard font without installation");
            var start = (Button)window.FindName("StartPauseButton");
            Check(new Typeface(start.FontFamily, start.FontStyle, start.FontWeight, start.FontStretch).TryGetGlyphTypeface(out var semiBold)
                && semiBold.FontUri.OriginalString.Contains("pretendard-semibold.otf", StringComparison.OrdinalIgnoreCase), "Buttons resolve to the embedded Pretendard SemiBold font");
            Check(Typography.GetNumeralAlignment(numbers) == FontNumeralAlignment.Tabular, "Timer uses tabular digits to avoid changing digit widths");
            var reset = (Button)window.FindName("ResetButton");
            var glyph = (GlyphPath)window.FindName("ResetIcon");
            Rect ink = glyph.Data.Bounds;
            Point glyphCenter = glyph.TransformToAncestor(window).Transform(new Point(ink.Left + ink.Width / 2, ink.Top + ink.Height / 2));
            Point buttonCenter = reset.TransformToAncestor(window).Transform(new Point(reset.ActualWidth / 2, reset.ActualHeight / 2));
            Check((glyphCenter - buttonCenter).Length < 0.05, "Reset ink center matches the rendered button center");
            foreach (string theme in new[] { "dark", "light" })
            {
                ThemeManager.Apply(theme); start.Focus(); window.UpdateLayout();
                Check(Descendants(start).OfType<Border>().All(b => b.BorderThickness == new Thickness(0))
                    && Descendants(reset).OfType<Border>().All(b => b.BorderThickness == new Thickness(0)), $"{theme} focused action buttons do not draw a stroke");
            }
            start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var pause = ((GlyphPath)window.FindName("StartPauseIcon")).Data;
            Check(pause.FillContains(new Point(6, 12)) && pause.FillContains(new Point(18, 12)) && !pause.FillContains(new Point(12, 12)), "Running timer displays two filled Pause bars");
            Console.WriteLine($"{checks} Windows appearance checks passed.");
        }
        finally { window.Close(); if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true); }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
