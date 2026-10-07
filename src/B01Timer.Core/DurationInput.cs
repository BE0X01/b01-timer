using System.Globalization;

namespace B01Timer.Core;

public enum TimeField { Hours, Minutes, Seconds }

public static class DurationInput
{
    public const int MaximumSeconds = 99 * 3600 + 59 * 60 + 59;
    public const string ValidationMessage = "Enter a time from 00:00:00 to 99:59:59.";

    // Each pair of digits belongs to a unit, starting at the selected field.
    // A short input replaces only that field; a longer one also replaces preceding fields.
    public static bool TryApply(string text, TimeField field, int currentSeconds, out int result)
    {
        result = currentSeconds;
        string digits = text.Trim();
        if (digits.Length == 0) digits = "0";
        int maxDigits = field switch { TimeField.Hours => 2, TimeField.Minutes => 4, _ => 6 };
        if (digits.Length > maxDigits || digits.Any(c => c is < '0' or > '9')) return false;
        int hours = currentSeconds / 3600;
        int minutes = currentSeconds / 60 % 60;
        int seconds = currentSeconds % 60;
        int RightPair(int offset) => offset >= digits.Length ? 0 : int.Parse(digits.Substring(Math.Max(0, digits.Length - offset - 2), Math.Min(2, digits.Length - offset)), CultureInfo.InvariantCulture);
        switch (field)
        {
            case TimeField.Hours:
                hours = int.Parse(digits, CultureInfo.InvariantCulture);
                break;
            case TimeField.Minutes:
                minutes = RightPair(0);
                if (digits.Length > 2) hours = RightPair(2);
                break;
            case TimeField.Seconds:
                seconds = RightPair(0);
                if (digits.Length > 2) minutes = RightPair(2);
                if (digits.Length > 4) hours = RightPair(4);
                break;
        }
        int normalized = hours * 3600 + minutes * 60 + seconds;
        if (normalized > MaximumSeconds) return false;
        result = normalized;
        return true;
    }

    public static string Format(int seconds) => $"{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}";
    public static string FavoriteLabel(int seconds) => Format(seconds);
}
