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
    File.WriteAllText(store.FilePath, "invalid ini");
    Check(store.Load().Theme == "dark", "Damaged settings recover to defaults");
    File.WriteAllText(store.FilePath, "[Settings]\nTheme=unknown\nLastTime=99:99:99\n[Presets]\nCount=1\n[Preset1]\nId=invalid\nTime=-1\n");
    var recovered = store.Load();
    Check(recovered.Theme == "dark" && recovered.LastSeconds == 0 && recovered.Favorites.Count == 0, "Invalid persisted values are sanitized");
    Check(Path.GetFileName(store.FilePath) == "B01Timer.ini", "Settings use the portable INI filename");
    store.Save(settings);
    string ini = File.ReadAllText(store.FilePath);
    Check(ini.Contains("LastTime=00:20:34") && ini.Contains("[Preset1]") && ini.Contains("Time=00:00:07"), "INI is readable with sectioned hh:mm:ss times");
    store.Save(new AppSettings { Favorites = [] });
    Check(store.Load().Favorites.Count == 0 && File.ReadAllText(store.FilePath).Contains("Count=0"), "Empty preset list remains empty after relaunch");
    File.Delete(store.FilePath);
    string legacy = Path.Combine(directory, "settings.json");
    File.WriteAllText(legacy, "{\"Theme\":\"light\",\"LastSeconds\":1234,\"Favorites\":[{\"Id\":\"migrated\",\"Seconds\":35}]}");
    var migrating = new SettingsStore(directory, legacy);
    var imported = migrating.Load(); migrating.Save(imported);
    Check(imported.Theme == "light" && imported.LastSeconds == 1234 && imported.Favorites[0].Id == "migrated" && File.Exists(legacy), "Legacy JSON imports into INI without deleting the original");
    File.WriteAllText(legacy, "{\"Theme\":\"dark\",\"LastSeconds\":0,\"Favorites\":[]}");
    Check(migrating.Load().Theme == "light" && migrating.Load().Favorites[0].Id == "migrated", "Existing INI prevents repeated legacy migration");
    Check(Directory.GetFiles(directory, "*.tmp-*").Length == 0, "Atomic saves clean up temporary files");
}
finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
// Decode the audio rather than trusting the construction pattern.
byte[] alarm = CompletionTone.CreateWave();
Check(System.Text.Encoding.ASCII.GetString(alarm, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(alarm, 8, 8) == "WAVEfmt "
    && BitConverter.ToInt32(alarm, 4) == alarm.Length - 8 && BitConverter.ToInt32(alarm, 40) == alarm.Length - 44, "Completion sound is a complete RIFF/WAVE file");
int rate = BitConverter.ToInt32(alarm, 24);
Check(rate == 44100 && BitConverter.ToInt16(alarm, 20) == 1 && BitConverter.ToInt16(alarm, 22) == 1 && BitConverter.ToInt16(alarm, 34) == 16, "Completion sound uses supported mono 16-bit PCM");
// Group consecutive 10ms frames containing audible samples into individual beeps.
var beeps = new List<(int start, int end)>();
int startFrame = -1, frameSamples = rate / 100, totalFrames = (alarm.Length - 44) / 2 / frameSamples;
for (int frame = 0; frame < totalFrames; frame++)
{
    bool audible = Enumerable.Range(0, frameSamples).Any(i => Math.Abs((int)BitConverter.ToInt16(alarm, 44 + 2 * (frame * frameSamples + i))) > 1000);
    if (audible && startFrame < 0) startFrame = frame;
    if (!audible && startFrame >= 0) { beeps.Add((startFrame, frame)); startFrame = -1; }
}
if (startFrame >= 0) beeps.Add((startFrame, totalFrames));
Check(beeps.Count == 6, "Completion sound contains six audible beeps");
Check(beeps.Count == 6 && beeps[3].start - beeps[2].end >= 30
    && beeps[1].start - beeps[0].end < 15 && beeps[2].start - beeps[1].end < 15
    && beeps[4].start - beeps[3].end < 15 && beeps[5].start - beeps[4].end < 15, "Completion sound plays two separated three-beep phrases");
Check(beeps.Count == 6 && beeps[2].end - beeps[2].start > beeps[0].end - beeps[0].start
    && beeps[5].end - beeps[5].start > beeps[3].end - beeps[3].start, "Each phrase ends with a longer final beep");
Console.WriteLine($"{checks} checks passed.");
