using B01Timer.Core;
using System.IO;
using System.Media;

namespace B01Timer;

internal sealed class CompletionAlarm : IDisposable
{
    private readonly MemoryStream wave = new(CompletionTone.CreateWave(), writable: false);
    private readonly SoundPlayer player;

    public CompletionAlarm()
    {
        player = new SoundPlayer(wave);
        player.Load();
    }

    public void Play() => player.Play();
    public void Stop() => player.Stop();
    public void Dispose() { player.Stop(); player.Dispose(); wave.Dispose(); }
}
