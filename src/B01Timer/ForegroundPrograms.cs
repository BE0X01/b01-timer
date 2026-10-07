using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace B01Timer;

public sealed record RunningProgram(string ExecutablePath, string WindowTitle)
{
    public string DisplayName => $"{Path.GetFileNameWithoutExtension(ExecutablePath)} · {WindowTitle}";
}

// No keyboard input, document contents, or window titles are persisted.
public sealed class ForegroundPrograms : IDisposable
{
    private delegate void WinEventProc(IntPtr hook, uint evt, IntPtr hwnd, int objectId, int childId, uint thread, uint time);
    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr parameter);
    private readonly WinEventProc callback;
    private readonly IntPtr hook;
    private volatile bool locked;
    public event Action<string?>? Changed;

    public ForegroundPrograms()
    {
        callback = (_, _, _, _, _, _, _) => Sample();
        hook = SetWinEventHook(3, 3, IntPtr.Zero, callback, 0, 0, 0);
        SystemEvents.SessionSwitch += SessionChanged;
    }

    private void SessionChanged(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.SessionLogoff or SessionSwitchReason.RemoteDisconnect or SessionSwitchReason.ConsoleDisconnect) locked = true;
        else if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.SessionLogon or SessionSwitchReason.RemoteConnect or SessionSwitchReason.ConsoleConnect) locked = false;
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(Sample));
    }

    public void Sample() => Changed?.Invoke(locked ? null : ExecutableFor(GetForegroundWindow()));
    public static long AwakeTicks()
    {
        // QueryUnbiasedInterruptTime excludes sleep and hibernation and uses 100ns units.
        if (!QueryUnbiasedInterruptTime(out ulong ticks)) throw new InvalidOperationException("The foreground recording clock is unavailable.");
        return (long)Math.Min(ticks, (ulong)long.MaxValue);
    }

    private static string? ExecutableFor(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return null;
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0 || pid == Environment.ProcessId) return null;
        IntPtr process = OpenProcess(0x1000, false, pid);
        if (process == IntPtr.Zero) return null;
        try
        {
            var text = new StringBuilder(32768); uint length = (uint)text.Capacity;
            return QueryFullProcessImageName(process, 0, text, ref length) ? text.ToString() : null;
        }
        finally { CloseHandle(process); }
    }

    public static IReadOnlyList<RunningProgram> List()
    {
        var programs = new Dictionary<string, RunningProgram>(StringComparer.OrdinalIgnoreCase);
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || GetWindow(hwnd, 4) != IntPtr.Zero) return true;
            var title = new StringBuilder(512);
            if (GetWindowText(hwnd, title, title.Capacity) == 0) return true;
            string? executable = ExecutableFor(hwnd);
            if (executable is not null) programs.TryAdd(executable, new(executable, title.ToString()));
            return true;
        }, IntPtr.Zero);
        return programs.Values.OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= SessionChanged;
        if (hook != IntPtr.Zero) UnhookWinEvent(hook);
        GC.KeepAlive(callback);
    }
    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref uint length);
    [DllImport("kernel32.dll")] private static extern bool QueryUnbiasedInterruptTime(out ulong time);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
}
