using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;

namespace B01Timer;

public sealed record RunningProgram(string ExecutablePath, string WindowTitle)
{
    public string DisplayName => $"{Path.GetFileNameWithoutExtension(ExecutablePath)} · {WindowTitle}";
}

// No keyboard input, document contents, or window titles are persisted.
public sealed class ForegroundPrograms : IDisposable
{
    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr parameter);
    private readonly HwndSource source;
    private readonly IntPtr window;
    private bool locked;
    private bool disposed;
    public event Action<string?>? Changed;

    public ForegroundPrograms(IntPtr window)
    {
        this.window = window;
        source = HwndSource.FromHwnd(window)!;
        source.AddHook(SessionMessage);
        WTSRegisterSessionNotification(window, 0);
    }

    private IntPtr SessionMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x02B1)
        {
            int reason = wParam.ToInt32();
            if (reason is 2 or 4 or 6 or 7) locked = true;
            else if (reason is 1 or 3 or 5 or 8) locked = false;
            Sample();
        }
        return IntPtr.Zero;
    }

    public void Sample() { if (!disposed) Changed?.Invoke(locked ? null : ExecutableFor(GetForegroundWindow())); }
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
            var text = new StringBuilder(1024); uint length = (uint)text.Capacity;
            if (QueryFullProcessImageName(process, 0, text, ref length)) return text.ToString();
            text = new StringBuilder(32768); length = (uint)text.Capacity;
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
        disposed = true;
        WTSUnRegisterSessionNotification(window);
        if (!source.IsDisposed) source.RemoveHook(SessionMessage);
    }
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
    [DllImport("wtsapi32.dll")] private static extern bool WTSRegisterSessionNotification(IntPtr hwnd, uint flags);
    [DllImport("wtsapi32.dll")] private static extern bool WTSUnRegisterSessionNotification(IntPtr hwnd);
}
