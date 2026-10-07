using B01Timer.Core;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace B01Timer;

public partial class DurationEditor : UserControl
{
    private int seconds;
    private int editBase;
    private TextBox? editing;
    private bool updating;
    private bool dirty;
    private bool hasValidationError;
    public event Action? EditingStarted;
    public event Action<int>? DurationChanged;
    public event Action<string>? ValidationChanged;
    public event Action? EnterCommitted;
    public bool IsEditing => editing is not null;
    public bool HasPositivePendingInput => editing is not null && DurationInput.TryApply(editing.Text, Enum.Parse<TimeField>((string)editing.Tag), editBase, out int value) && value > 0;
    public double NumberSize { get; set; } = 60;
    public double FieldWidth { get; set; } = 92;
    public string AutomationPrefix { get; set; } = "";
    public int Seconds { get => seconds; set { seconds = value; if (!IsEditing) UpdateFields(); } }

    public DurationEditor()
    {
        InitializeComponent();
        foreach (TextBox box in Boxes)
        {
            box.GotKeyboardFocus += BeginEditing;
            box.LostKeyboardFocus += (_, _) => Commit();
            box.PreviewMouseLeftButtonDown += (_, e) => { if (!box.IsKeyboardFocusWithin) { box.Focus(); e.Handled = true; } };
            box.PreviewTextInput += (_, e) => e.Handled = e.Text.Any(c => c is < '0' or > '9');
            box.TextChanged += (_, _) => { if (!updating && editing == box) { dirty = true; box.FontSize = Math.Max(16, NumberSize * 2 / Math.Max(2, box.Text.Length)); ClearValidation(); } };
            box.PreviewKeyDown += KeyDownInField;
            DataObject.AddPastingHandler(box, (_, e) => { if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText) || ((string)e.SourceDataObject.GetData(DataFormats.UnicodeText)).Any(c => c is < '0' or > '9')) e.CancelCommand(); });
        }
        Loaded += (_, _) => { ApplySizing(); UpdateFields(); foreach (var box in Boxes) AutomationProperties.SetAutomationId(box, AutomationPrefix + box.Tag + "Input"); };
    }
    private TextBox[] Boxes => [HoursInput, MinutesInput, SecondsInput];
    private void ApplySizing()
    {
        foreach (var box in Boxes) { box.Width = FieldWidth; box.Height = NumberSize + 14; box.FontSize = NumberSize; }
        foreach (var colon in new[] { ColonOne, ColonTwo }) { colon.Width = NumberSize > 30 ? 24 : 16; colon.FontSize = NumberSize; }
    }
    private void UpdateFields()
    {
        if (!IsInitialized) return;
        updating = true;
        HoursInput.Text = (seconds / 3600).ToString("00"); MinutesInput.Text = (seconds / 60 % 60).ToString("00"); SecondsInput.Text = (seconds % 60).ToString("00");
        foreach (var box in Boxes) box.FontSize = NumberSize;
        updating = false;
    }
    private void BeginEditing(object sender, KeyboardFocusChangedEventArgs e)
    {
        EditingStarted?.Invoke();
        ClearValidation();
        editing = (TextBox)sender; editBase = seconds; dirty = false;
        editing.SelectAll();
    }
    public bool Commit()
    {
        if (editing is null) return !hasValidationError;
        TextBox box = editing; editing = null;
        if (!dirty) { UpdateFields(); return true; }
        if (!DurationInput.TryApply(box.Text, Enum.Parse<TimeField>((string)box.Tag), editBase, out int result))
        {
            seconds = editBase; hasValidationError = true; UpdateFields(); ValidationChanged?.Invoke("Time must be 00:00:00–99:59:59."); return false;
        }
        seconds = result; UpdateFields(); ClearValidation(); DurationChanged?.Invoke(result); return true;
    }
    private void KeyDownInField(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { bool valid = Commit(); Keyboard.ClearFocus(); if (valid) EnterCommitted?.Invoke(); e.Handled = true; }
        else if (e.Key == Key.Escape) { editing = null; seconds = editBase; UpdateFields(); ClearValidation(); Keyboard.ClearFocus(); e.Handled = true; }
        else if (e.Key == Key.Space) e.Handled = true;
    }
    public void FocusMinutes() { MinutesInput.Focus(); MinutesInput.SelectAll(); }
    public void ClearValidation() { hasValidationError = false; ValidationChanged?.Invoke(""); }
}
