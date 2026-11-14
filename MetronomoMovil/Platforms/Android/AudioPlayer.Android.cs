using Android.Media;

namespace MetronomoMovil.Audio;

public sealed partial class AudioPlayer
{
    private AudioTrack? _track;
    private Thread? _thread;
    private volatile bool _running;

    private partial void PlatformStart()
    {
        // Frecuencia nativa del dispositivo → sin remuestreo y ruta de baja latencia (AAudio/fast track).
        int sampleRate = AudioTrack.GetNativeOutputSampleRate(Android.Media.Stream.Music);
        if (sampleRate <= 0) sampleRate = 48000;
        Engine.SetSampleRate(sampleRate);
        Engine.Reset();

        int minBytes = AudioTrack.GetMinBufferSize(sampleRate, ChannelOut.Mono, Encoding.PcmFloat);
        var builder = new AudioTrack.Builder()
            .SetAudioAttributes(new AudioAttributes.Builder()
                .SetUsage(AudioUsageKind.Media)!
                .SetContentType(AudioContentType.Music)!
                .Build()!)
            .SetAudioFormat(new AudioFormat.Builder()
                .SetEncoding(Encoding.PcmFloat)!
                .SetSampleRate(sampleRate)!
                .SetChannelMask(ChannelOut.Mono)!
                .Build()!)
            .SetBufferSizeInBytes(Math.Max(minBytes, 1024) * 2)
            .SetTransferMode(AudioTrackMode.Stream);
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            builder.SetPerformanceMode(AudioTrackPerformanceMode.LowLatency);

        _track = builder.Build();
        _track.Play();

        _running = true;
        _thread = new Thread(AudioLoop) { IsBackground = true, Name = "MetronomeAudio" };
        _thread.Start();
    }

    private void AudioLoop()
    {
        Android.OS.Process.SetThreadPriority(Android.OS.ThreadPriority.UrgentAudio);
        var buffer = new float[256]; // ~5 ms a 48 kHz
        var track = _track!;
        while (_running)
        {
            Engine.Render(buffer);
            track.Write(buffer, 0, buffer.Length, WriteMode.Blocking);
        }
    }

    private partial void PlatformStop()
    {
        _running = false;
        _thread?.Join(500);
        _thread = null;
        if (_track != null)
        {
            _track.Pause();
            _track.Flush();
            _track.Stop();
            _track.Release();
            _track = null;
        }
    }

    private partial long PlatformPlayedFrames() => _track?.PlaybackHeadPosition ?? 0;
}
