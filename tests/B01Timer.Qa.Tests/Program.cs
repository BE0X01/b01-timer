using B01Timer.Core;

int checks = 0;
void Check(bool condition, string description)
{
    checks++;
    if (!condition) throw new Exception(description);
    Console.WriteLine($"PASS {description}");
}
void Input(string digits, TimeField field, int current, int expected, string description)
    => Check(DurationInput.TryApply(digits, field, current, out int actual) && actual == expected, description);

Input("8", TimeField.Hours, 2 * 3600 + 34 * 60 + 56, 8 * 3600 + 34 * 60 + 56, "Short hours preserve minutes and seconds");
Input("20", TimeField.Minutes, 12 * 3600 + 34 * 60 + 56, 12 * 3600 + 20 * 60 + 56, "Short minutes preserve hours and seconds");
Input("408", TimeField.Minutes, 12 * 3600 + 34 * 60 + 56, 4 * 3600 + 8 * 60 + 56, "Long minutes replace hours but preserve seconds");
Input("408", TimeField.Seconds, 12 * 3600 + 34 * 60 + 56, 12 * 3600 + 4 * 60 + 8, "Three-digit seconds preserve hours");
Input("1234", TimeField.Seconds, 12 * 3600 + 34 * 60 + 56, 12 * 3600 + 12 * 60 + 34, "Four-digit seconds preserve hours");
Input(" 8 ", TimeField.Seconds, 12 * 3600 + 34 * 60 + 56, 12 * 3600 + 34 * 60 + 8, "Whitespace is normalized at input boundaries");
Input("00000", TimeField.Seconds, 12 * 3600 + 34 * 60 + 56, 0, "Five-digit zero clears all units");
Input("90", TimeField.Minutes, 12 * 3600 + 34 * 60 + 56, 13 * 3600 + 30 * 60 + 56, "Minute overflow carries while preserving seconds");
Input("99", TimeField.Seconds, 12 * 3600 + 34 * 60 + 56, 12 * 3600 + 35 * 60 + 39, "Second overflow carries while preserving higher units");
Check(!DurationInput.TryApply("99", TimeField.Seconds, DurationInput.MaximumSeconds, out _), "Overflow at maximum is rejected");
Check(!DurationInput.TryApply("+8", TimeField.Minutes, 0, out _), "Signed input is rejected");
Check(!DurationInput.TryApply("08:30", TimeField.Minutes, 0, out _), "Colon-containing input is rejected");
Check(!DurationInput.TryApply("８", TimeField.Minutes, 0, out _), "Non-ASCII digits are rejected consistently");

bool allRoundTrips = true;
for (int seconds = 0; seconds <= DurationInput.MaximumSeconds; seconds++)
{
    string digits = DurationInput.Format(seconds).Replace(":", "");
    if (!DurationInput.TryApply(digits, TimeField.Seconds, 9876, out int result) || result != seconds) { allRoundTrips = false; break; }
}
Check(allRoundTrips, "All 360000 supported second values round trip through HHMMSS input");

double clock = 0;
var timer = new CountdownTimer(() => clock);
timer.Configure(10);
timer.Start(); clock = 2; timer.Start(); clock = 3;
Check(timer.DisplaySeconds == 7, "Repeated Start cannot extend a running countdown");
timer.Pause(); clock = 30; timer.Pause(); clock = 300;
Check(timer.DisplaySeconds == 7, "Repeated Pause cannot change remaining time");
timer.Start(); clock = 306.999;
Check(timer.DisplaySeconds == 1 && !timer.Tick(), "Countdown preserves final fractional second");
clock = 307;
Check(timer.Tick() && timer.State == CountdownState.Finished && timer.DisplaySeconds == 0, "Exact deadline finishes once");
clock = 1000;
Check(!timer.Tick() && timer.DisplaySeconds == 0, "Delayed updates cannot produce negative time or duplicate completion");
timer.Reset();
Check(timer.State == CountdownState.Ready && timer.DisplaySeconds == 10, "Reset after completion restores waiting baseline");
timer.Configure(35); timer.Start(); clock += 3; timer.Pause(); timer.Start(); timer.Reset();
Check(timer.InitialSeconds == 35 && timer.DisplaySeconds == 35, "Pause/resume preserves most recent configuration as Reset baseline");
timer.Configure(DurationInput.MaximumSeconds); timer.Start(); clock += 1;
Check(timer.DisplaySeconds == DurationInput.MaximumSeconds - 1, "Maximum duration counts down without overflow");
bool negativeRejected = false;
try { timer.Configure(-1); } catch (ArgumentOutOfRangeException) { negativeRejected = true; }
Check(negativeRejected, "Negative programmatic duration is rejected");
bool maximumRejected = false;
try { timer.Configure(DurationInput.MaximumSeconds + 1); } catch (ArgumentOutOfRangeException) { maximumRejected = true; }
Check(maximumRejected, "Programmatic duration above maximum is rejected");

string directory = Path.Combine(Path.GetTempPath(), "b01-timer-independent-qa-" + Guid.NewGuid().ToString("N"));
try
{
    var store = new SettingsStore(directory);
    Check(store.Load().Theme == "dark" && store.Load().Favorites.Count > 0, "First launch returns usable default settings");
    store.Save(new AppSettings { Theme = "light", LastSeconds = 0, Favorites = [] });
    Check(store.Load().Favorites.Count == 0 && store.Load().Theme == "light", "Removing all presets persists without restoring defaults");
    File.WriteAllText(store.FilePath, "{\"Theme\":null,\"LastSeconds\":-5,\"Favorites\":[null,{\"Id\":\"one\",\"Seconds\":7},{\"Id\":\"one\",\"Seconds\":8},{\"Id\":\"\",\"Seconds\":8}]}");
    var recovered = store.Load();
    Check(recovered.Theme == "dark" && recovered.LastSeconds == 0 && recovered.Favorites.Count == 1 && recovered.Favorites[0].Seconds == 7, "Null, duplicate, empty-ID and negative settings recover safely");
    Check(!File.Exists(store.FilePath + ".tmp"), "Successful saves do not leave a temporary settings file");
}
finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

Console.WriteLine($"{checks} independent QA checks passed.");
