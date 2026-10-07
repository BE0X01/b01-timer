using B01Timer;
using B01Timer.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Reflection;
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
        var settings = new AppSettings { LastSeconds = 900 };
        var window = new MainWindow(settings, new SettingsStore(folder));
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
            VerifyClockLayout(window, settings, Check);
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

    private static void VerifyClockLayout(MainWindow window, AppSettings settings, Action<bool, string> check)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        const int seconds = 12 * 3600 + 34 * 60 + 56;
        var timer = (CountdownTimer)typeof(MainWindow).GetField("timer", flags)!.GetValue(window)!;
        var clockField = typeof(CountdownTimer).GetField("clock", flags)!;
        var originalClock = clockField.GetValue(timer);
        var recorderField = typeof(MainWindow).GetField("recorder", flags)!;
        var originalRecorder = recorderField.GetValue(window);
        var originalRecords = settings.Records;
        string originalSelection = settings.SelectedRecordId;
        string originalTheme = ThemeManager.Current;
        long awakeTicks = 0;
        var record = new ProgramRecord { Id = "clock-layout", Title = "Clock layout", ExecutablePath = @"C:\QA\ClockLayout.exe", ElapsedTicks = seconds * TimeSpan.TicksPerSecond };
        var recorder = new FocusRecorder(settings, () => awakeTicks);
        // Freeze both clocks only for image comparison. The real dispatcher/clock
        // regression below runs after these production dependencies are restored.
        clockField.SetValue(timer, (Func<double>)(() => 0));
        recorderField.SetValue(window, recorder);
        settings.Records = [record]; settings.SelectedRecordId = record.Id;
        ThemeManager.Apply("dark");
        var start = (Button)window.FindName("StartPauseButton");
        var editor = (DurationEditor)window.FindName("TimeEditor");
        var snapshots = new Dictionary<string, ClockCapture>();
        void Refresh() { typeof(MainWindow).GetMethod("UpdateDisplay", flags)!.Invoke(window, null); window.UpdateLayout(); }
        void Click(string id) => ((Button)window.FindName(id)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ClockCapture Capture(string name)
        {
            Refresh();
            var image = CaptureClock(window, name);
            snapshots.Add(name, image);
            return image;
        }
        try
        {
            typeof(MainWindow).GetMethod("RenderRecords", flags)!.Invoke(window, null);
            start.Focus(); timer.Configure(seconds); Refresh();
            var reference = Capture("timer-ready");
            check(((TextBlock)window.FindName("StatusText")).Text == "Ready" && reference.InkBounds.All(r => !r.IsEmpty && r.Width > 30 && r.Height > 30), "Ready clock raster comparison contains real digit ink in all three fields");
            Click("StartPauseButton");
            var running = Capture("timer-running");
            check(((TextBlock)window.FindName("StatusText")).Text == "Running" && timer.State == CountdownState.Running && reference.Pixels.SequenceEqual(running.Pixels), "Ready and Running have identical rendered digits, colons and labels at 12:34:56");
            Click("StartPauseButton");
            var paused = Capture("timer-paused");
            check(((TextBlock)window.FindName("StatusText")).Text == "Paused" && timer.State == CountdownState.Paused && reference.Pixels.SequenceEqual(paused.Pixels), "Paused retains the exact Ready/Running glyph baseline and clock pixels");
            Click("RecordTab");
            var waiting = Capture("record-waiting");
            check(((TextBlock)window.FindName("StatusText")).Text.StartsWith("Waiting for focus") && reference.Pixels.SequenceEqual(waiting.Pixels), "Record waiting shares the exact Timer clock pixels and label positions");
            recorder.SetForeground(record.ExecutablePath); awakeTicks += TimeSpan.TicksPerMillisecond * 100; recorder.Tick();
            var recording = Capture("record-recording");
            check(((TextBlock)window.FindName("StatusText")).Text.StartsWith("Recording") && reference.Pixels.SequenceEqual(recording.Pixels), "Record recording preserves the same baseline, digit spacing, colons and labels");
            Click("TimerTab"); Click("StartPauseButton");
            var resumed = Capture("timer-resumed");
            check(timer.State == CountdownState.Running && reference.Pixels.SequenceEqual(resumed.Pixels), "Switching back and resuming cannot move the clock ink");
            Click("StartPauseButton"); editor.FocusField(TimeField.Hours);
            var hours = (TextBox)editor.FindName("HoursInput");
            var minutes = (TextBox)editor.FindName("MinutesInput");
            var oldHoursCaret = hours.CaretBrush; var oldMinutesCaret = minutes.CaretBrush;
            try
            {
                // Selection and caret are intentional editing decoration. Hide
                // only the caret, collapse the selection, then measure glyph ink.
                hours.CaretBrush = Brushes.Transparent; hours.Select(hours.Text.Length, 0);
                var editingHours = Capture("timer-edit-hours");
                check(hours.IsKeyboardFocused && reference.InkBounds.SequenceEqual(editingHours.InkBounds), "Focused two-digit Hours editing preserves the passive glyph ink bounds and baseline");
                hours.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                minutes.CaretBrush = Brushes.Transparent; minutes.Select(minutes.Text.Length, 0);
                var editingMinutes = Capture("timer-edit-minutes");
                check(minutes.IsKeyboardFocused && reference.InkBounds.SequenceEqual(editingMinutes.InkBounds), "Tab traversal edits Minutes without shifting any digit ink");
            }
            finally { hours.CaretBrush = oldHoursCaret; minutes.CaretBrush = oldMinutesCaret; }
            start.Focus(); editor.Commit();
            var committed = Capture("timer-edit-committed");
            check(!editor.IsEditing && reference.Pixels.SequenceEqual(committed.Pixels), "Leaving an unchanged editor restores the exact passive clock raster");
            string output = ClockArtifactDirectory();
            System.IO.File.WriteAllText(System.IO.Path.Combine(output, "ink-bounds.json"), System.Text.Json.JsonSerializer.Serialize(snapshots.ToDictionary(x => x.Key, x => x.Value.InkBounds.Select(r => new { r.X, r.Y, r.Width, r.Height })), new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            start.Focus(); editor.Commit();
            clockField.SetValue(timer, originalClock); recorderField.SetValue(window, originalRecorder);
            settings.Records = originalRecords; settings.SelectedRecordId = originalSelection;
            typeof(MainWindow).GetMethod("RenderRecords", flags)!.Invoke(window, null);
            timer.Configure(900); Click("TimerTab"); ThemeManager.Apply(originalTheme); Refresh();
        }
    }

    private sealed record ClockCapture(byte[] Pixels, Int32Rect[] InkBounds);

    private static string ClockArtifactDirectory()
    {
        string path = System.IO.Path.Combine(Environment.CurrentDirectory, "artifacts", "qa-ui", "clock-layout");
        System.IO.Directory.CreateDirectory(path); return path;
    }

    private static ClockCapture CaptureClock(MainWindow window, string name)
    {
        var panel = (Border)window.FindName("TimerPanel");
        Point origin = panel.TranslatePoint(new Point(), window);
        // The 324-DIP clock is centered in the same panel. Include every digit,
        // both colons and all unit labels, excluding status and the day badge.
        var region = new Int32Rect((int)Math.Round(origin.X + (panel.ActualWidth - 324) / 2), (int)Math.Round(origin.Y + (panel.ActualHeight - 100) / 2), 324, 100);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = System.IO.File.Create(System.IO.Path.Combine(ClockArtifactDirectory(), name + ".png"))) encoder.Save(stream);
        byte[] pixels = new byte[region.Width * region.Height * 4];
        bitmap.CopyPixels(region, pixels, region.Width * 4, 0);
        // This matches the old 2-pixel regression's bright-ink measurement and
        // ignores dark backgrounds, muted labels and the colored focus border.
        var ink = new List<Int32Rect>();
        foreach (int fieldLeft in new[] { 0, 116, 232 })
        {
            int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
            for (int y = 0; y < region.Height; y++) for (int x = fieldLeft; x < fieldLeft + 92; x++)
            {
                int offset = (y * region.Width + x) * 4;
                if (pixels[offset] <= 210 || pixels[offset + 1] <= 210 || pixels[offset + 2] <= 210) continue;
                left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y);
            }
            ink.Add(right < 0 ? Int32Rect.Empty : new Int32Rect(left, top, right - left + 1, bottom - top + 1));
        }
        Console.WriteLine($"TRACE {name} clock ink={string.Join(";", ink)}");
        return new(pixels, ink.ToArray());
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
