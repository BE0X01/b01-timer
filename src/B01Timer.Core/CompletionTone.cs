using System.Text;

namespace B01Timer.Core;

public static class CompletionTone
{
    // Two short three-beep phrases, with a longer gap between the phrases.
    public static byte[] CreateWave()
    {
        const int sampleRate = 44100;
        const double frequency = 1600;
        (int milliseconds, bool tone)[] pattern =
        [
            (120, true), (60, false), (120, true), (60, false), (200, true), (380, false),
            (120, true), (60, false), (120, true), (60, false), (200, true), (120, false)
        ];
        int sampleCount = pattern.Sum(part => sampleRate * part.milliseconds / 1000);
        using var stream = new MemoryStream(44 + sampleCount * 2);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + sampleCount * 2);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
        writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate);
        writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(sampleCount * 2);
        int fadeSamples = sampleRate * 5 / 1000;
        foreach (var part in pattern)
        {
            int length = sampleRate * part.milliseconds / 1000;
            for (int i = 0; i < length; i++)
            {
                double fade = Math.Min(1, Math.Min((double)i / fadeSamples, (double)(length - 1 - i) / fadeSamples));
                short sample = part.tone ? (short)(short.MaxValue * 0.28 * fade * Math.Sin(2 * Math.PI * frequency * i / sampleRate)) : (short)0;
                writer.Write(sample);
            }
        }
        return stream.ToArray();
    }
}
