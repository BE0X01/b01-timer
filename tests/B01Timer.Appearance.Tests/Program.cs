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
        typeof(App).GetField("Diagnostic", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.SetValue(null, new Action<string>(Console.WriteLine));
        var app = new TestApp();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/B01Timer;component/Styles.xaml") });
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/B01Timer;component/Icons.xaml") });
        ThemeManager.Apply("dark");
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
            double RenderedSize(GlyphPath path) { var bounds = path.Data.Bounds; var a = path.TransformToAncestor(window).Transform(bounds.TopLeft); var b = path.TransformToAncestor(window).Transform(bounds.BottomRight); return Math.Max(b.X - a.X, b.Y - a.Y); }
            var play = (GlyphPath)window.FindName("StartPauseIcon");
            Check(Math.Abs(RenderedSize(play) - RenderedSize(glyph)) < 0.05, "Play ink has the same maximum dimension as Reset");
            Point IconCenter(GlyphPath path) { var bounds = path.Data.Bounds; return path.TransformToAncestor(window).Transform(new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2)); }
            Check((IconCenter(play) - start.TransformToAncestor(window).Transform(new Point(start.ActualWidth / 2, start.ActualHeight / 2))).Length < 0.05, "Play ink is centered in its button");
            var themeButton = (Button)window.FindName("ThemeButton");
            Check(themeButton.ActualWidth == 36 && themeButton.ActualHeight == 36, "Theme toggle is enlarged to 36 by 36");
            foreach (string theme in new[] { "dark", "light" })
            {
                ThemeManager.Apply(theme); start.Focus(); window.UpdateLayout();
                Check(Descendants(start).OfType<Border>().All(b => b.BorderThickness == new Thickness(0))
                    && Descendants(reset).OfType<Border>().All(b => b.BorderThickness == new Thickness(0)), $"{theme} focused action buttons do not draw a stroke");
            }
            start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var pause = ((GlyphPath)window.FindName("StartPauseIcon")).Data;
            Check(pause.FillContains(new Point(6, 12)) && pause.FillContains(new Point(18, 12)) && !pause.FillContains(new Point(12, 12)), "Running timer displays two filled Pause bars");
            window.UpdateLayout();
            Check(Math.Abs(RenderedSize(play) - RenderedSize(glyph)) < 0.05 && (IconCenter(play) - start.TransformToAncestor(window).Transform(new Point(start.ActualWidth / 2, start.ActualHeight / 2))).Length < 0.05, "Filled Pause matches Reset size and is centered");
            using var watchdog = new System.Threading.Timer(_ =>
            {
                Console.Error.WriteLine("FAIL WPF clock update did not complete within 10 seconds.");
                try
                {
                    string? tool = Environment.GetEnvironmentVariable("B01TIMER_STACK_TOOL");
                    if (tool is not null)
                    {
                        var info = new System.Diagnostics.ProcessStartInfo(tool) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
                        info.ArgumentList.Add("report"); info.ArgumentList.Add("-p"); info.ArgumentList.Add(Environment.ProcessId.ToString());
                        using var process = System.Diagnostics.Process.Start(info)!;
                        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
                        if (process.WaitForExit(5000)) { Console.Error.WriteLine(output.GetAwaiter().GetResult()); Console.Error.WriteLine(errors.GetAwaiter().GetResult()); }
                        else process.Kill(true);
                    }
                }
                catch (Exception error) { Console.Error.WriteLine(error.Message); }
                Environment.Exit(1);
            }, null, 10000, System.Threading.Timeout.Infinite);
            var frame = new System.Windows.Threading.DispatcherFrame();
            using var wait = new System.Threading.Timer(_ => window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, new Action(() => frame.Continue = false)), null, 1250, System.Threading.Timeout.Infinite);
            Console.WriteLine("TRACE entering WPF frame"); System.Windows.Threading.Dispatcher.PushFrame(frame); Console.WriteLine("TRACE exited WPF frame");
            Check(((TextBlock)window.FindName("RecordSeconds")).Text == "59", "Live countdown clock advances while editable inputs remain separate");
            ((Button)window.FindName("RecordTab")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
            Check(start.Visibility == Visibility.Collapsed && reset.Visibility == Visibility.Visible, "Record has only the Reset action");
            var recordPanel = (Grid)window.FindName("RecordDisplay"); var badge = (TextBlock)window.FindName("RecordDays"); var hours = (TextBlock)window.FindName("RecordHours");
            Point position = hours.TransformToAncestor(window).Transform(new Point()); Size panelSize = recordPanel.RenderSize;
            badge.Text = "12345w 6d"; window.UpdateLayout();
            Check(position == hours.TransformToAncestor(window).Transform(new Point()) && panelSize == recordPanel.RenderSize, "Day and week badge does not move the clock or alter panel size");
            Check(Typography.GetNumeralAlignment(hours) == FontNumeralAlignment.Tabular, "Record uses stable-width tabular Pretendard digits");
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

internal sealed class TestApp : Application
{
    // App startup is queued even when tests manually pump the dispatcher.
    // The fixture supplies its own window and isolated SettingsStore.
    protected override void OnStartup(StartupEventArgs e) { }
}
