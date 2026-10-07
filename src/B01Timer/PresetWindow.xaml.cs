using System.Windows;

namespace B01Timer;

public partial class PresetWindow : Window
{
    public int ResultSeconds { get; private set; }
    public PresetWindow(int initialSeconds, bool editing)
    {
        InitializeComponent();
        Title = CaptionText.Text = HeadingText.Text = editing ? "Edit preset" : "Add preset";
        SaveButton.Content = editing ? "Save" : "Add";
        PresetEditor.NumberSize = 24; PresetEditor.FieldWidth = 80; PresetEditor.AutomationPrefix = "Preset"; PresetEditor.Seconds = initialSeconds;
        PresetEditor.ValidationChanged += message => ErrorText.Text = message;
        PresetEditor.EnterCommitted += () => Save_Click(this, new());
        PresetEditor.EscapeCanceled += () => DialogResult = false;
        Loaded += (_, _) => PresetEditor.FocusMinutes();
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!PresetEditor.Commit()) return;
        if (PresetEditor.Seconds <= 0) { ErrorText.Text = "Set a time greater than 00:00:00."; return; }
        ResultSeconds = PresetEditor.Seconds; DialogResult = true;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
