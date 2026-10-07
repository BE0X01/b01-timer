using B01Timer.Core;

int checks = 0;
void Check(bool result, string name) { checks++; if (!result) throw new Exception(name); Console.WriteLine($"PASS {name}"); }
void Input(string text, TimeField field, int expected, int initial = 0) => Check(DurationInput.TryApply(text, field, initial, out int actual) && actual == expected, $"{field} {text} => {DurationInput.Format(expected)}");
Input("40", TimeField.Hours, 40 * 3600);
Input("20", TimeField.Minutes, 20 * 60);
Input("8", TimeField.Minutes, 8 * 60);
Input("408", TimeField.Minutes, 4 * 3600 + 8 * 60);
Input("123000", TimeField.Seconds, 12 * 3600 + 30 * 60);
Input("60", TimeField.Seconds, 60);
Input("75", TimeField.Minutes, 75 * 60);
Input("8", TimeField.Minutes, 3600 + 8 * 60 + 9, 3600 + 23 * 60 + 9);
Input("000001", TimeField.Seconds, 1, 3600);
Input("995959", TimeField.Seconds, DurationInput.MaximumSeconds);
Check(!DurationInput.TryApply("999999", TimeField.Seconds, 0, out _), "Reject normalized overflow");
Check(!DurationInput.TryApply("100", TimeField.Hours, 0, out _), "Reject three-digit hours");
Check(!DurationInput.TryApply("1234567", TimeField.Seconds, 0, out _), "Reject too many digits without truncating");
Check(!DurationInput.TryApply("abc", TimeField.Minutes, 0, out _), "Reject nonnumeric input");

double now = 100;
var timer = new CountdownTimer(() => now);
timer.Configure(20);
Check(timer.State == CountdownState.Ready && timer.DisplaySeconds == 20, "Configured timer does not run");
timer.Start(); now += 0.1;
Check(timer.DisplaySeconds == 20, "Ceiling display keeps full first second");
now += 6.4;
Check(timer.DisplaySeconds == 14, "Elapsed time uses clock instead of tick counts");
timer.Pause(); now += 500;
Check(timer.State == CountdownState.Paused && timer.DisplaySeconds == 14, "Pause excludes paused interval");
timer.Start(); now += 2;
Check(timer.DisplaySeconds == 12, "Resume preserves fraction of second");
timer.Reset();
Check(timer.State == CountdownState.Ready && timer.DisplaySeconds == 20, "Reset restores original time and waiting state");
timer.Start(); now += 2; timer.Configure(60); now += 5;
Check(timer.State == CountdownState.Ready && timer.DisplaySeconds == 60, "New preset stops running timer and only configures");
timer.Start(); now += 61;
Check(timer.Tick() && timer.State == CountdownState.Finished && timer.DisplaySeconds == 0, "Completes at zero after delayed tick");
Check(!timer.Tick(), "Completion emitted once");
timer.Start();
Check(timer.State == CountdownState.Running && timer.DisplaySeconds == 60, "Start after completion restarts original timer");
timer.Configure(0); timer.Start();
Check(timer.State == CountdownState.Ready && timer.DisplaySeconds == 0, "Cannot start zero-duration timer");

string directory = Path.Combine(Path.GetTempPath(), "b01-timer-test-" + Guid.NewGuid().ToString("N"));
try
{
    var store = new SettingsStore(directory);
    var settings = new AppSettings { Theme = "light", LastSeconds = 1234, Favorites = [new("a", 7), new("b", 999)] };
    store.Save(settings);
    var loaded = store.Load();
    Check(loaded.Theme == "light" && loaded.LastSeconds == 1234 && loaded.Favorites.SequenceEqual(settings.Favorites), "Settings round trip preserves theme, initial time and presets");
    File.WriteAllText(store.FilePath, "invalid json");
    Check(store.Load().Theme == "dark", "Damaged settings recover to defaults");
    File.WriteAllText(store.FilePath, "{\"Theme\":\"unknown\",\"LastSeconds\":9999999,\"Favorites\":[{\"Id\":\"invalid\",\"Seconds\":-1}]}");
    var recovered = store.Load();
    Check(recovered.Theme == "dark" && recovered.LastSeconds == DurationInput.MaximumSeconds && recovered.Favorites.Count == 0, "Invalid persisted values are sanitized");
}
finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
Console.WriteLine($"{checks} checks passed.");
