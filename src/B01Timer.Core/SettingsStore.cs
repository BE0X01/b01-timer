using System.Text.Json;

namespace B01Timer.Core;

public sealed record Favorite(string Id, int Seconds);
public sealed class AppSettings
{
    public string Theme { get; set; } = "dark";
    public int LastSeconds { get; set; }
    public List<Favorite> Favorites { get; set; } = [new(Guid.NewGuid().ToString("N"), 15 * 60), new(Guid.NewGuid().ToString("N"), 30 * 60), new(Guid.NewGuid().ToString("N"), 60 * 60)];
}

public sealed class SettingsStore(string directory)
{
    public string FilePath => Path.Combine(directory, "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
            settings.Theme = settings.Theme == "light" ? "light" : "dark";
            settings.LastSeconds = Math.Clamp(settings.LastSeconds, 0, DurationInput.MaximumSeconds);
            settings.Favorites = (settings.Favorites ?? []).Where(f => f is not null && !string.IsNullOrEmpty(f.Id) && f.Seconds is > 0 and <= DurationInput.MaximumSeconds).DistinctBy(f => f.Id).ToList();
            return settings;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(directory);
        string temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, overwrite: true);
    }
}
