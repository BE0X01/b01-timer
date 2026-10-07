using System.Globalization;
using System.Text;
using System.Text.Json;

namespace B01Timer.Core;

public sealed record Favorite(string Id, int Seconds);
public sealed class AppSettings
{
    public string Theme { get; set; } = "dark";
    public int LastSeconds { get; set; }
    public string SelectedRecordId { get; set; } = "";
    public List<ProgramRecord> Records { get; set; } = [];
    public List<Favorite> Favorites { get; set; } = [new(Guid.NewGuid().ToString("N"), 15 * 60), new(Guid.NewGuid().ToString("N"), 30 * 60), new(Guid.NewGuid().ToString("N"), 60 * 60)];
}

public sealed class SettingsStore(string directory, string? legacyJsonPath = null)
{
    public string FilePath => Path.Combine(directory, "B01Timer.ini");

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath)) return ReadIni(File.ReadAllLines(FilePath, Encoding.UTF8));
            if (legacyJsonPath is not null && File.Exists(legacyJsonPath))
                return Sanitize(JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(legacyJsonPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new());
            return new();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    private static AppSettings Sanitize(AppSettings settings)
    {
        settings.Theme = string.Equals(settings.Theme, "light", StringComparison.OrdinalIgnoreCase) ? "light" : "dark";
        settings.LastSeconds = Math.Clamp(settings.LastSeconds, 0, DurationInput.MaximumSeconds);
        settings.Favorites = (settings.Favorites ?? []).Where(f => f is not null && ValidId(f.Id) && f.Seconds is > 0 and <= DurationInput.MaximumSeconds).DistinctBy(f => f.Id).ToList();
        settings.Records = (settings.Records ?? []).Where(r => r is not null && ValidId(r.Id) && ValidId(r.Title) && ValidId(r.ExecutablePath))
            .DistinctBy(r => r.Id).DistinctBy(r => r.ExecutablePath, StringComparer.OrdinalIgnoreCase).Take(5).ToList();
        foreach (var record in settings.Records) { record.Title = record.Title.Trim(); record.ElapsedTicks = Math.Max(0, record.ElapsedTicks); }
        if (!settings.Records.Any(r => r.Id == settings.SelectedRecordId)) settings.SelectedRecordId = settings.Records.FirstOrDefault()?.Id ?? "";
        return settings;
    }

    private static bool ValidId(string? id) => !string.IsNullOrWhiteSpace(id) && !id.Any(c => c is '\r' or '\n' or '\0');
    private static bool TryTime(string? text, out int seconds)
    {
        seconds = 0;
        if (text is null || text.Length != 8 || text[2] != ':' || text[5] != ':') return false;
        for (int i = 0; i < text.Length; i++) if (i != 2 && i != 5 && text[i] is < '0' or > '9') return false;
        int hours = int.Parse(text.AsSpan(0, 2), CultureInfo.InvariantCulture);
        int minutes = int.Parse(text.AsSpan(3, 2), CultureInfo.InvariantCulture);
        int remaining = int.Parse(text.AsSpan(6, 2), CultureInfo.InvariantCulture);
        if (minutes >= 60 || remaining >= 60) return false;
        seconds = hours * 3600 + minutes * 60 + remaining;
        return true;
    }

    private static AppSettings ReadIni(IEnumerable<string> lines)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? section = null;
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                string name = line[1..^1].Trim();
                if (!sections.TryGetValue(name, out section)) sections[name] = section = new(StringComparer.OrdinalIgnoreCase);
                continue;
            }
            int equals = line.IndexOf('=');
            if (section is not null && equals > 0) section[line[..equals].Trim()] = line[(equals + 1)..].Trim();
        }
        var settings = new AppSettings();
        if (sections.TryGetValue("Settings", out var values))
        {
            if (values.TryGetValue("Theme", out string? theme)) settings.Theme = theme;
            if (values.TryGetValue("LastTime", out string? time) && TryTime(time, out int seconds)) settings.LastSeconds = seconds;
            if (values.TryGetValue("SelectedRecordId", out string? selected)) settings.SelectedRecordId = selected;
        }
        bool explicitCount = sections.TryGetValue("Presets", out var presetValues) && presetValues.TryGetValue("Count", out _);
        int? count = null;
        if (explicitCount && int.TryParse(presetValues!["Count"], NumberStyles.None, CultureInfo.InvariantCulture, out int parsed)) count = parsed;
        var presetSections = sections.Select(pair => (pair.Key, pair.Value, Index: pair.Key.StartsWith("Preset", StringComparison.OrdinalIgnoreCase) && int.TryParse(pair.Key.AsSpan(6), NumberStyles.None, CultureInfo.InvariantCulture, out int index) ? index : -1)).Where(p => p.Index > 0).DistinctBy(p => p.Index).OrderBy(p => p.Index).ToList();
        if (explicitCount || presetSections.Count > 0)
        {
            settings.Favorites = [];
            foreach (var preset in presetSections)
            {
                if (count is not null && preset.Index > count) continue;
                if (preset.Value.TryGetValue("Id", out string? id) && ValidId(id) && preset.Value.TryGetValue("Time", out string? time) && TryTime(time, out int seconds) && seconds > 0) settings.Favorites.Add(new(id, seconds));
            }
        }
        int recordCount = sections.TryGetValue("Records", out var recordValues) && recordValues.TryGetValue("Count", out string? recordCountText)
            && int.TryParse(recordCountText, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedCount) ? Math.Clamp(parsedCount, 0, 5) : 5;
        for (int i = 1; i <= recordCount; i++)
        {
            if (!sections.TryGetValue($"Record{i}", out var record)) continue;
            if (!record.TryGetValue("Id", out string? id) || !record.TryGetValue("Title", out string? title) || !record.TryGetValue("ExecutablePath", out string? executable)) continue;
            long elapsed = record.TryGetValue("ElapsedTicks", out string? ticks) && long.TryParse(ticks, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsedTicks) ? Math.Max(0, parsedTicks) : 0;
            settings.Records.Add(new() { Id = id, Title = title, ExecutablePath = executable, ElapsedTicks = elapsed });
        }
        return Sanitize(settings);
    }

    public void Save(AppSettings settings)
    {
        Sanitize(settings);
        Directory.CreateDirectory(directory);
        string temporary = FilePath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.WriteLine("; B01 Timer portable settings. Times use hh:mm:ss.");
                writer.WriteLine("[Settings]"); writer.WriteLine($"Theme={settings.Theme}"); writer.WriteLine($"LastTime={DurationInput.Format(settings.LastSeconds)}"); writer.WriteLine($"SelectedRecordId={settings.SelectedRecordId}"); writer.WriteLine();
                writer.WriteLine("[Presets]"); writer.WriteLine($"Count={settings.Favorites.Count}");
                for (int i = 0; i < settings.Favorites.Count; i++)
                {
                    Favorite favorite = settings.Favorites[i];
                    writer.WriteLine(); writer.WriteLine($"[Preset{i + 1}]"); writer.WriteLine($"Id={favorite.Id}"); writer.WriteLine($"Time={DurationInput.Format(favorite.Seconds)}");
                }
                writer.WriteLine(); writer.WriteLine("[Records]"); writer.WriteLine($"Count={settings.Records.Count}");
                for (int i = 0; i < settings.Records.Count; i++)
                {
                    var record = settings.Records[i];
                    writer.WriteLine(); writer.WriteLine($"[Record{i + 1}]"); writer.WriteLine($"Id={record.Id}"); writer.WriteLine($"Title={record.Title}"); writer.WriteLine($"ExecutablePath={record.ExecutablePath}");
                    writer.WriteLine($"ElapsedTicks={record.ElapsedTicks.ToString(CultureInfo.InvariantCulture)}");
                }
                writer.Flush(); stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
