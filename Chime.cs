using System;
using System.IO;
using System.Media;

namespace ElectronicSchedule;

/// <summary>
/// 上下课提示音。程序内直接合成 WAV 音频，不需要携带任何音频文件，
/// 也不依赖系统音效方案（有的教室机器把系统声音关掉了）。
/// </summary>
internal static class Chime
{
    private const int SampleRate = 22050;

    // SoundPlayer 播放是异步的，必须留一个静态引用，否则对象被回收后声音会被掐断。
    private static SoundPlayer? player;

    private static readonly Lazy<byte[]> PrepareSound = new(() => Build((523.25, 140), (659.25, 140)));
    private static readonly Lazy<byte[]> StartSound = new(() => Build((659.25, 170), (880.00, 260)));
    private static readonly Lazy<byte[]> EndSound = new(() => Build((880.00, 150), (659.25, 150), (523.25, 220)));

    public static void PlayPrepare() => Play(PrepareSound.Value);
    public static void PlayStart() => Play(StartSound.Value);
    public static void PlayEnd() => Play(EndSound.Value);

    /// <summary>试听，用于设置界面确认音量是否合适。</summary>
    public static void Preview() => PlayStart();

    private static void Play(byte[] wav)
    {
        try
        {
            player?.Dispose();
            player = new SoundPlayer(new MemoryStream(wav));
            player.Play();
        }
        catch (Exception ex)
        {
            Log.Write(ex);
        }
    }

    /// <summary>按给定的频率和时长生成一段 WAV，音符之间加淡入淡出，避免爆音。</summary>
    private static byte[] Build(params (double frequency, int milliseconds)[] notes)
    {
        int totalSamples = 0;
        foreach (var note in notes)
            totalSamples += SampleRate * note.milliseconds / 1000;

        using var stream = new MemoryStream(44 + totalSamples * 2);
        using var writer = new BinaryWriter(stream);

        int dataBytes = totalSamples * 2;

        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);              // fmt 块长度
        writer.Write((short)1);        // PCM
        writer.Write((short)1);        // 单声道
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);  // 字节率
        writer.Write((short)2);        // 块对齐
        writer.Write((short)16);       // 位深
        writer.Write("data"u8.ToArray());
        writer.Write(dataBytes);

        foreach (var (frequency, milliseconds) in notes)
        {
            int samples = SampleRate * milliseconds / 1000;
            int fade = Math.Max(1, samples / 8);

            for (int i = 0; i < samples; i++)
            {
                double t = (double)i / SampleRate;
                double envelope = 1.0;

                if (i < fade)
                    envelope = (double)i / fade;
                else if (i > samples - fade)
                    envelope = (double)(samples - i) / fade;

                // 主音 + 一个八度泛音，听起来更像提示音而不是单音蜂鸣
                double value = Math.Sin(2 * Math.PI * frequency * t) * 0.62
                             + Math.Sin(2 * Math.PI * frequency * 2 * t) * 0.18;

                short sample = (short)(value * envelope * short.MaxValue * 0.7);
                writer.Write(sample);
            }
        }

        writer.Flush();
        return stream.ToArray();
    }
}