using B01Timer.Core;
using System.IO;
using System.Windows;

namespace B01Timer;

public partial class RecordWindow : Window
{
    private readonly AppSettings settings;
    private readonly ProgramRecord? editing;
    public string ResultTitle { get; private set; } = "";
    public string ResultPath { get; private set; } = "";
    public RecordWindow(AppSettings settings, ProgramRecord? editing = null)
    {
        this.settings = settings; this.editing = editing;
        InitializeComponent();
        Title = CaptionText.Text = HeadingText.Text = editing is null ? "Add program" : "Edit program";
        SaveButton.Content = editing is null ? "Add" : "Save";
        TitleInput.Text = editing?.Title ?? "";
        Loaded += (_, _) => { RefreshPrograms(); TitleInput.Focus(); };
        ProgramSelect.DropDownOpened += (_, _) => RefreshPrograms();
    }
    private void RefreshPrograms()
    {
        string? selected = (ProgramSelect.SelectedItem as RunningProgram)?.ExecutablePath ?? editing?.ExecutablePath;
        var programs = ForegroundPrograms.List().ToList();
        if (editing is not null && !programs.Any(p => string.Equals(p.ExecutablePath, editing.ExecutablePath, StringComparison.OrdinalIgnoreCase)))
            programs.Add(new(editing.ExecutablePath, "Not running"));
        ProgramSelect.ItemsSource = programs;
        ProgramSelect.SelectedItem = programs.FirstOrDefault(p => string.Equals(p.ExecutablePath, selected, StringComparison.OrdinalIgnoreCase));
        ErrorText.Text = programs.Count == 0 ? "Open a program, then reopen this dropdown." : "";
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string title = TitleInput.Text.Trim();
        if (title.Length == 0 || title.Any(c => c is '\r' or '\n' or '\0')) { ErrorText.Text = "Enter a title."; TitleInput.Focus(); return; }
        if (ProgramSelect.SelectedItem is not RunningProgram program) { ErrorText.Text = "Select a running program."; return; }
        if (settings.Records.Any(r => r.Id != editing?.Id && string.Equals(r.ExecutablePath, program.ExecutablePath, StringComparison.OrdinalIgnoreCase))) { ErrorText.Text = "This program is already registered."; return; }
        if (editing is null && settings.Records.Count >= 5) { ErrorText.Text = "You can register up to 5 programs."; return; }
        ResultTitle = title; ResultPath = program.ExecutablePath; DialogResult = true;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
