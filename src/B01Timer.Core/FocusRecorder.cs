using System.Globalization;

namespace B01Timer.Core;

public sealed class ProgramRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public long ElapsedTicks { get; set; }
}

public readonly record struct RecordDuration(string Time, string Days)
{
    public static RecordDuration FromTicks(long ticks)
    {
        long seconds = Math.Max(0, ticks) / TimeSpan.TicksPerSecond;
        long days = seconds / 86400, weeks = days / 7;
        string badge = weeks > 0 ? weeks.ToString(CultureInfo.InvariantCulture) + "w" + (days % 7 > 0 ? " " + (days % 7).ToString(CultureInfo.InvariantCulture) + "d" : "")
            : days > 0 ? days.ToString(CultureInfo.InvariantCulture) + "d" : "";
        return new(FormattableString.Invariant($"{seconds / 3600 % 24:00}:{seconds / 60 % 60:00}:{seconds % 60:00}"), badge);
    }
}

// Selection is purely a view concern. Every registered executable can accrue time.
// The supplied monotonic clock excludes computer sleep; fractions survive saves.
public sealed class FocusRecorder(AppSettings settings, Func<long> clock)
{
    private long last = clock();
    public string? ForegroundPath { get; private set; }

    public void SetForeground(string? executablePath)
    {
        Tick();
        ForegroundPath = executablePath;
    }

    public void Tick()
    {
        long now = clock();
        long elapsed = now >= last ? now - last : 0;
        last = now;
        if (elapsed == 0 || ForegroundPath is null) return;
        foreach (var record in settings.Records.Where(r => string.Equals(r.ExecutablePath, ForegroundPath, StringComparison.OrdinalIgnoreCase)))
            record.ElapsedTicks += Math.Min(elapsed, long.MaxValue - record.ElapsedTicks);
    }

    public void Reset(string id)
    {
        Tick();
        var record = settings.Records.Find(r => r.Id == id);
        if (record is not null) record.ElapsedTicks = 0;
    }
}
