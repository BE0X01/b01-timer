using B01Timer.Core;
using System.Media;
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
    private readonly DispatcherTimer pulse = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private string validation = "";
    private bool initialized;

    public MainWindow(AppSettings settings, SettingsStore store)
    {
        this.settings = settings; this.store = store;
        InitializeComponent();
        timer.Configure(settings.LastSeconds);
        TimeEditor.Seconds = timer.DisplaySeconds;
        TimeEditor.EditingStarted += () => { timer.Pause(); UpdateDisplay(); };
        TimeEditor.DurationChanged += ConfigureTime;
        TimeEditor.ValidationChanged += message => { validation = message; UpdateDisplay(); };
        pulse.Tick += (_, _) => { if (timer.Tick()) { SystemSounds.Asterisk.Play(); FlashWindow(new WindowInteropHelper(this).Handle, false); } UpdateDisplay(); };
        Loaded += (_, _) => { SaveSettings(); initialized = true; SetDwmAppearance(); RenderPresets(); UpdateThemeButton(); UpdateDisplay(); pulse.Start(); };
        Closed += (_, _) => { pulse.Stop(); SaveSettings(); };
        StateChanged += (_, _) => MaximizeButton.ToolTip = WindowState == WindowState.Maximized ? "Restore" : "Maximize";
        PreviewKeyDown += Window_PreviewKeyDown;
    }

    private void ConfigureTime(int seconds)
    {
        timer.Configure(seconds); settings.LastSeconds = seconds; validation = ""; TimeEditor.ClearValidation();
        SaveSettings(); RenderPresets(); UpdateDisplay();
    }
    private void SaveSettings()
    {
        try { store.Save(settings); }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException) { validation = "Settings could not be saved."; }
    }
    private void UpdateDisplay()
    {
        if (!TimeEditor.IsEditing) TimeEditor.Seconds = timer.DisplaySeconds;
        bool running = timer.State == CountdownState.Running;
        StartPauseIcon.Data = (Geometry)FindResource(running ? "IconPause" : "IconPlay");
        string action = running ? "Pause timer" : "Start timer";
        StartPauseButton.ToolTip = action; AutomationProperties.SetName(StartPauseButton, action);
        StartPauseButton.IsEnabled = timer.InitialSeconds > 0 || TimeEditor.HasPositivePendingInput;
        StatusDot.Visibility = running && validation.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = validation.Length > 0 ? validation : timer.State switch { CountdownState.Running => "Running", CountdownState.Paused => "Paused", CountdownState.Finished => "Time’s up", _ => "Ready" };
        StatusText.SetResourceReference(TextBlock.ForegroundProperty, validation.Length > 0 ? "ErrorBrush" : timer.State == CountdownState.Finished ? "AccentBrush" : "MutedBrush");
    }
    private void StartPause_Click(object sender, RoutedEventArgs e)
    {
        if (!TimeEditor.Commit()) return;
        if (timer.State == CountdownState.Running) timer.Pause(); else timer.Start();
        validation = ""; UpdateDisplay();
    }
    private void Reset_Click(object sender, RoutedEventArgs e) { TimeEditor.Commit(); timer.Reset(); TimeEditor.ClearValidation(); validation = ""; UpdateDisplay(); }
    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        settings.Theme = settings.Theme == "dark" ? "light" : "dark"; ThemeManager.Apply(settings.Theme); SaveSettings(); UpdateThemeButton(); SetDwmAppearance();
    }
    private void UpdateThemeButton()
    {
        bool dark = settings.Theme == "dark"; ThemeIcon.Data = (Geometry)FindResource(dark ? "IconSun" : "IconMoon");
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
            if (favorite.Seconds == timer.InitialSeconds) { chip.SetResourceReference(Button.BackgroundProperty, "SelectedBrush"); chip.SetResourceReference(Button.ForegroundProperty, "AccentBrush"); }
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
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox) return;
        if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.None) { StartPause_Click(this, new()); e.Handled = true; }
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
