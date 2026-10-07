using B01Timer.Core;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.Windows.Shapes.Path;

namespace B01Timer;

public partial class MainWindow : Window
{
    private readonly CountdownTimer timer = new();
    private readonly AppSettings settings;
    private readonly SettingsStore store;
    private readonly DispatcherTimer pulse = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly CompletionAlarm alarm = new();
    private readonly FocusRecorder recorder;
    private ForegroundPrograms? foreground;
    private readonly Geometry playIcon, pauseIcon;
    private bool recordView;
    private long lastRecordSave;
    private decimal savedTotalTicks;
    private int diagnosticTicks;
    private string validation = "";
    private string statusBrush = "";
    private bool initialized;

    public MainWindow(AppSettings settings, SettingsStore store)
    {
        this.settings = settings; this.store = store;
        InitializeComponent();
        recorder = new(settings, ForegroundPrograms.AwakeTicks);
        var resetGeometry = (Geometry)FindResource("IconRestart");
        double diameter = Math.Max(resetGeometry.Bounds.Width, resetGeometry.Bounds.Height);
        ResetIcon.Data = CenterIcon(resetGeometry, diameter);
        playIcon = CenterIcon((Geometry)FindResource("IconPlay"), diameter);
        pauseIcon = CenterIcon((Geometry)FindResource("IconPause"), diameter);
        timer.Configure(settings.LastSeconds);
        TimeEditor.Seconds = timer.DisplaySeconds;
        TimeEditor.EditingStarted += () => { timer.Pause(); UpdateDisplay(); };
        TimeEditor.DurationChanged += ConfigureTime;
        TimeEditor.ValidationChanged += message => { validation = message; UpdateDisplay(); };
        pulse.Tick += (_, _) =>
        {
            if (diagnosticTicks++ < 3) App.Diagnostic?.Invoke("pulse " + diagnosticTicks + " editing=" + TimeEditor.IsEditing + " remaining=" + timer.RemainingSeconds);
            if (timer.Tick()) { alarm.Play(); FlashWindow(new WindowInteropHelper(this).Handle, false); }
            if (settings.Records.Count > 0)
            {
                foreground?.Sample(); recorder.Tick();
                long now = ForegroundPrograms.AwakeTicks();
                if (now - lastRecordSave >= TimeSpan.TicksPerSecond && settings.Records.Sum(r => (decimal)r.ElapsedTicks) != savedTotalTicks)
                { SaveSettings(); lastRecordSave = now; }
            }
            if (diagnosticTicks % 20 == 0) App.Diagnostic?.Invoke("pulse " + diagnosticTicks + " editing=" + TimeEditor.IsEditing + " remaining=" + timer.RemainingSeconds + " state=" + timer.State);
            UpdateDisplay();
        };
        Loaded += (_, _) =>
        {
            App.Diagnostic?.Invoke("Loaded start");
            SaveSettings(); initialized = true; SetDwmAppearance(); RenderPresets(); RenderRecords(); UpdateThemeButton(); UpdateDisplay();
            App.Diagnostic?.Invoke("foreground construction");
            pulse.Start(); foreground = new(new WindowInteropHelper(this).Handle); App.Diagnostic?.Invoke("foreground created"); foreground.Changed += path => recorder.SetForeground(path); foreground.Sample();
            App.Diagnostic?.Invoke("Loaded end pulse=" + pulse.IsEnabled);
        };
        Closed += (_, _) => { pulse.Stop(); recorder.SetForeground(null); foreground?.Dispose(); alarm.Dispose(); SaveSettings(); };
        StateChanged += (_, _) => MaximizeButton.ToolTip = WindowState == WindowState.Maximized ? "Restore" : "Maximize";
        PreviewKeyDown += Window_PreviewKeyDown;
    }

    private static Geometry CenterIcon(Geometry source, double diameter)
    {
        var geometry = source.Clone(); Rect ink = geometry.Bounds;
        double scale = diameter / Math.Max(ink.Width, ink.Height);
        var matrix = Matrix.Identity;
        matrix.Translate(-ink.Left - ink.Width / 2, -ink.Top - ink.Height / 2); matrix.Scale(scale, scale); matrix.Translate(12, 12);
        geometry.Transform = new MatrixTransform(matrix); geometry.Freeze(); return geometry;
    }

    private void ConfigureTime(int seconds)
    {
        alarm.Stop();
        timer.Configure(seconds); settings.LastSeconds = seconds; validation = ""; TimeEditor.ClearValidation();
        SaveSettings(); RenderPresets(); UpdateDisplay();
    }
    private void SaveSettings()
    {
        try { store.Save(settings); savedTotalTicks = settings.Records.Sum(r => (decimal)r.ElapsedTicks); }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException) { validation = "Settings could not be saved."; }
    }
    private void UpdateDisplay()
    {
        if (!TimeEditor.IsEditing) TimeEditor.Seconds = timer.DisplaySeconds;
        bool running = timer.State == CountdownState.Running;
        StartPauseIcon.Data = running ? pauseIcon : playIcon;
        string action = running ? "Pause timer" : "Start timer";
        StartPauseButton.ToolTip = action; AutomationProperties.SetName(StartPauseButton, action);
        StartPauseButton.IsEnabled = timer.InitialSeconds > 0 || TimeEditor.HasPositivePendingInput;
        if (recordView)
        {
            var record = settings.Records.Find(r => r.Id == settings.SelectedRecordId);
            var duration = RecordDuration.FromTicks(record?.ElapsedTicks ?? 0);
            string[] parts = duration.Time.Split(':'); RecordHours.Text = parts[0]; RecordMinutes.Text = parts[1]; RecordSeconds.Text = parts[2]; RecordDays.Text = duration.Days;
            AutomationProperties.SetName(RecordDisplay, duration.Time);
            bool active = record is not null && string.Equals(record.ExecutablePath, recorder.ForegroundPath, StringComparison.OrdinalIgnoreCase);
            StatusDot.Visibility = active && validation.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = validation.Length > 0 ? validation : record is null ? "Add a program to track" : active ? "Recording · " + record.Title : "Waiting for focus · " + record.Title;
            SetStatusBrush(validation.Length > 0 ? "ErrorBrush" : "MutedBrush");
            ResetButton.IsEnabled = record is not null;
            return;
        }
        ResetButton.IsEnabled = true;
        StatusDot.Visibility = running && validation.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = validation.Length > 0 ? validation : timer.State switch { CountdownState.Running => "Running", CountdownState.Paused => "Paused", CountdownState.Finished => "Time’s up", _ => "Ready" };
        SetStatusBrush(validation.Length > 0 ? "ErrorBrush" : timer.State == CountdownState.Finished ? "AccentBrush" : "MutedBrush");
    }
    private void SetStatusBrush(string brush)
    {
        if (statusBrush == brush) return;
        statusBrush = brush; StatusText.SetResourceReference(TextBlock.ForegroundProperty, brush);
    }
    private void StartPause_Click(object sender, RoutedEventArgs e)
    {
        if (!TimeEditor.Commit()) return;
        alarm.Stop();
        if (timer.State == CountdownState.Running) timer.Pause(); else timer.Start();
        validation = ""; UpdateDisplay();
        App.Diagnostic?.Invoke("StartPause state=" + timer.State + " remaining=" + timer.RemainingSeconds + " editing=" + TimeEditor.IsEditing + " pulse=" + pulse.IsEnabled + " ticks=" + diagnosticTicks);
    }
    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (recordView) { recorder.Reset(settings.SelectedRecordId); validation = ""; SaveSettings(); UpdateDisplay(); return; }
        alarm.Stop(); TimeEditor.Commit(); timer.Reset(); TimeEditor.ClearValidation(); validation = ""; UpdateDisplay();
    }
    private void TimerTab_Click(object sender, RoutedEventArgs e) => SwitchView(false);
    private void RecordTab_Click(object sender, RoutedEventArgs e) => SwitchView(true);
    private void SwitchView(bool record)
    {
        Keyboard.ClearFocus(); TimeEditor.Commit(); recordView = record;
        TimeEditor.Visibility = PresetsPanel.Visibility = StartPauseButton.Visibility = record ? Visibility.Collapsed : Visibility.Visible;
        RecordDisplay.Visibility = RecordsPanel.Visibility = record ? Visibility.Visible : Visibility.Collapsed;
        TimerTab.SetResourceReference(Button.BackgroundProperty, record ? "SurfaceBrush" : "RaisedBrush");
        RecordTab.SetResourceReference(Button.BackgroundProperty, record ? "RaisedBrush" : "SurfaceBrush");
        ResetButton.Margin = new Thickness(record ? 0 : 10, 0, 0, 0);
        string reset = record ? "Reset record" : "Reset timer";
        ResetButton.ToolTip = reset; AutomationProperties.SetName(ResetButton, reset);
        UpdateDisplay();
    }
    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        settings.Theme = settings.Theme == "dark" ? "light" : "dark"; ThemeManager.Apply(settings.Theme); SaveSettings(); UpdateThemeButton(); SetDwmAppearance();
    }
    private void UpdateThemeButton()
    {
        bool dark = settings.Theme == "dark"; ThemeIcon.Data = CenterIcon((Geometry)FindResource(dark ? "IconSun" : "IconMoon"), 21.5);
        string action = dark ? "Switch to light theme" : "Switch to dark theme";
        ThemeButton.ToolTip = action; AutomationProperties.SetName(ThemeButton, action);
    }
    private void RenderPresets()
    {
        if (!IsInitialized) return;
        PresetsPanel.Children.Clear();
        for (int i = 0; i < settings.Favorites.Count; i++)
        {
            Favorite favorite = settings.Favorites[i];
            var chip = new Button { Content = DurationInput.Format(favorite.Seconds), Height = 32, Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(0, 0, 8, 0), Style = (Style)FindResource("ButtonBase") };
            AutomationProperties.SetAutomationId(chip, $"PresetChip_{i}"); AutomationProperties.SetName(chip, DurationInput.Format(favorite.Seconds));
            chip.ToolTip = "Set timer · Right-click to edit or remove";
            chip.Click += (_, _) => { Keyboard.ClearFocus(); ConfigureTime(favorite.Seconds); };
            var menu = new ContextMenu();
            var edit = new MenuItem { Header = "Edit" }; edit.Click += (_, _) => OpenPreset(favorite);
            var remove = new MenuItem { Header = "Remove" }; remove.SetResourceReference(MenuItem.ForegroundProperty, "ErrorBrush"); remove.Click += (_, _) => { settings.Favorites.RemoveAll(f => f.Id == favorite.Id); SaveSettings(); RenderPresets(); };
            menu.Items.Add(edit); menu.Items.Add(remove); chip.ContextMenu = menu; PresetsPanel.Children.Add(chip);
        }
        var add = new Button { Width = 32, Height = 32, Padding = new Thickness(8), Style = (Style)FindResource("ButtonBase"), Background = Brushes.Transparent, BorderThickness = new Thickness(1), ToolTip = "Add preset" };
        add.SetResourceReference(Button.BorderBrushProperty, "BorderBrush");
        var glyph = new Path { Width = 24, Height = 24, Data = (Geometry)FindResource("IconAdd"), StrokeThickness = 1.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round }; glyph.SetResourceReference(Path.StrokeProperty, "MutedBrush"); add.Content = new Viewbox { Width = 16, Height = 16, Child = glyph };
        AutomationProperties.SetName(add, "Add preset"); AutomationProperties.SetAutomationId(add, "AddPresetButton");
        add.Click += (_, _) => OpenPreset(null); PresetsPanel.Children.Add(add);
    }
    private void OpenPreset(Favorite? favorite)
    {
        var dialog = new PresetWindow(favorite?.Seconds ?? timer.InitialSeconds, favorite is not null) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        if (favorite is null) settings.Favorites.Add(new(Guid.NewGuid().ToString("N"), dialog.ResultSeconds));
        else { int index = settings.Favorites.FindIndex(f => f.Id == favorite.Id); if (index >= 0) settings.Favorites[index] = favorite with { Seconds = dialog.ResultSeconds }; }
        SaveSettings(); RenderPresets();
    }
    private void RenderRecords()
    {
        RecordsPanel.Children.Clear();
        for (int i = 0; i < settings.Records.Count; i++)
        {
            var record = settings.Records[i];
            var chip = new Button { Content = new TextBlock { Text = record.Title, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 126 }, Height = 32,
                Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(0, 0, 8, 0), Style = (Style)FindResource("ButtonBase"), ToolTip = record.Title + "\n" + record.ExecutablePath + "\nRight-click to edit or remove" };
            AutomationProperties.SetAutomationId(chip, $"RecordChip_{i}"); AutomationProperties.SetName(chip, record.Title);
            chip.Click += (_, _) => { settings.SelectedRecordId = record.Id; SaveSettings(); UpdateDisplay(); };
            var menu = new ContextMenu();
            var edit = new MenuItem { Header = "Edit" }; edit.Click += (_, _) => OpenRecord(record);
            var remove = new MenuItem { Header = "Remove" }; remove.SetResourceReference(MenuItem.ForegroundProperty, "ErrorBrush");
            remove.Click += (_, _) => { recorder.Tick(); settings.Records.Remove(record); SaveSettings(); RenderRecords(); UpdateDisplay(); };
            menu.Items.Add(edit); menu.Items.Add(remove); chip.ContextMenu = menu; RecordsPanel.Children.Add(chip);
        }
        var add = new Button { Width = 32, Height = 32, Padding = new Thickness(8), Style = (Style)FindResource("ButtonBase"), Background = Brushes.Transparent,
            BorderThickness = new Thickness(1), IsEnabled = settings.Records.Count < 5, ToolTip = settings.Records.Count < 5 ? "Add program" : "Up to 5 programs" };
        add.SetResourceReference(Button.BorderBrushProperty, "BorderBrush");
        var glyph = new Path { Width = 24, Height = 24, Data = (Geometry)FindResource("IconAdd"), StrokeThickness = 1.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        glyph.SetResourceReference(Path.StrokeProperty, "MutedBrush"); add.Content = new Viewbox { Width = 16, Height = 16, Child = glyph };
        AutomationProperties.SetName(add, "Add program"); AutomationProperties.SetAutomationId(add, "AddRecordButton");
        add.Click += (_, _) => OpenRecord(null); RecordsPanel.Children.Add(add);
    }
    private void OpenRecord(ProgramRecord? record)
    {
        var dialog = new RecordWindow(settings, record) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        recorder.Tick();
        if (record is null)
        {
            record = new() { Title = dialog.ResultTitle, ExecutablePath = dialog.ResultPath };
            settings.Records.Add(record); settings.SelectedRecordId = record.Id;
        }
        else { record.Title = dialog.ResultTitle; record.ExecutablePath = dialog.ResultPath; }
        SaveSettings(); RenderRecords(); UpdateDisplay();
    }
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox) return;
        if (!recordView && e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.None) { StartPause_Click(this, new()); e.Handled = true; }
        if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control) { Reset_Click(this, new()); e.Handled = true; }
    }
    private void PresetsPanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!initialized) return;
        double required = 360 + Math.Min(40, Math.Max(0, e.NewSize.Height - 32));
        MinHeight = required - 20;
        if (Height < required && WindowState == WindowState.Normal) Height = required;
    }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ClickCount == 2) Maximize_Click(sender, new()); }
    private void SetDwmAppearance()
    {
        int dark = settings.Theme == "dark" ? 1 : 0; int rounded = 2; IntPtr handle = new WindowInteropHelper(this).Handle;
        DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int)); DwmSetWindowAttribute(handle, 33, ref rounded, sizeof(int));
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("user32.dll")] private static extern bool FlashWindow(IntPtr hwnd, bool invert);
}
