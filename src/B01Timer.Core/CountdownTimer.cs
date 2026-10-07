using System.Diagnostics;

namespace B01Timer.Core;

public enum CountdownState { Ready, Running, Paused, Finished }

public sealed class CountdownTimer
{
    private readonly Func<double> clock;
    private double deadline;
    private double remaining;
    public int InitialSeconds { get; private set; }
    public CountdownState State { get; private set; } = CountdownState.Ready;
    public double RemainingSeconds => State == CountdownState.Running ? Math.Max(0, deadline - clock()) : remaining;
    public int DisplaySeconds => (int)Math.Ceiling(RemainingSeconds);
    public double Progress => InitialSeconds == 0 ? 0 : Math.Clamp(1 - RemainingSeconds / InitialSeconds, 0, 1);

    public CountdownTimer(Func<double>? monotonicClock = null) => clock = monotonicClock ?? (() => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);

    public void Configure(int seconds)
    {
        if (seconds is < 0 or > DurationInput.MaximumSeconds) throw new ArgumentOutOfRangeException(nameof(seconds));
        InitialSeconds = seconds;
        remaining = seconds;
        State = CountdownState.Ready;
    }

    public void Start()
    {
        if (State == CountdownState.Running || InitialSeconds == 0) return;
        if (State == CountdownState.Finished) remaining = InitialSeconds;
        deadline = clock() + remaining;
        State = CountdownState.Running;
    }

    public void Pause()
    {
        if (State != CountdownState.Running) return;
        remaining = RemainingSeconds;
        State = remaining <= 0 ? CountdownState.Finished : CountdownState.Paused;
    }

    public void Reset()
    {
        remaining = InitialSeconds;
        State = CountdownState.Ready;
    }

    // Returns true once, when this countdown reaches zero.
    public bool Tick()
    {
        if (State != CountdownState.Running || RemainingSeconds > 0) return false;
        remaining = 0;
        State = CountdownState.Finished;
        return true;
    }
}
