using MetronomoMovil.Core;

namespace MetronomoMovil.Audio;

/// <summary>
/// Salida de audio de baja latencia. La implementación es nativa en cada plataforma:
/// Platforms/Android/AudioPlayer.Android.cs (AudioTrack) y Platforms/iOS/AudioPlayer.iOS.cs (AVAudioEngine).
/// </summary>
public sealed partial class AudioPlayer
{
    public MetronomeEngine Engine { get; } = new();
    public bool IsPlaying { get; private set; }

    public void Start()
    {
        if (IsPlaying) return;
        PlatformStart();
        IsPlaying = true;
    }

    public void Stop()
    {
        if (!IsPlaying) return;
        PlatformStop();
        IsPlaying = false;
    }

    /// <summary>Muestras que ya han salido por el altavoz (para sincronizar el indicador).</summary>
    public long PlayedFrames => IsPlaying ? PlatformPlayedFrames() : 0;

    private partial void PlatformStart();
    private partial void PlatformStop();
    private partial long PlatformPlayedFrames();
}
