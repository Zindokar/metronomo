using AudioToolbox;
using AVFoundation;

namespace MetronomoMovil.Audio;

public sealed partial class AudioPlayer
{
    private AVAudioEngine? _avEngine;
    private AVAudioSourceNode? _source;
    private long _renderedFrames;
    private double _latencySeconds;

    private partial void PlatformStart()
    {
        // Categoría Playback: suena aunque el interruptor de silencio del iPhone esté activado.
        var session = AVAudioSession.SharedInstance();
        session.SetCategory(AVAudioSessionCategory.Playback);
        session.SetPreferredIOBufferDuration(0.005, out _); // ~5 ms
        session.SetActive(true);

        int sampleRate = (int)session.SampleRate;
        if (sampleRate <= 0) sampleRate = 48000;
        Engine.SetSampleRate(sampleRate);
        Engine.Reset();
        Interlocked.Exchange(ref _renderedFrames, 0);
        _latencySeconds = session.OutputLatency + session.IOBufferDuration;

        var format = new AVAudioFormat(sampleRate, 1); // float32 mono, no entrelazado
        _source = new AVAudioSourceNode(format, Render);
        _avEngine = new AVAudioEngine();
        _avEngine.AttachNode(_source);
        _avEngine.Connect(_source, _avEngine.MainMixerNode, format);
        _avEngine.Prepare();
        _avEngine.StartAndReturnError(out var error);
        if (error != null) throw new InvalidOperationException(error.LocalizedDescription);
    }

    // Callback de tiempo real del sistema de audio.
    private unsafe int Render(ref bool isSilence, ref AudioTimeStamp timestamp, uint frameCount, AudioBuffers outputData)
    {
        int frames = (int)frameCount;
        var first = outputData[0];
        var span = new Span<float>((void*)first.Data, frames);
        Engine.Render(span);
        for (int i = 1; i < outputData.Count; i++)
            span.CopyTo(new Span<float>((void*)outputData[i].Data, frames));
        Interlocked.Add(ref _renderedFrames, frames);
        isSilence = false;
        return 0;
    }

    private partial void PlatformStop()
    {
        _avEngine?.Stop();
        _avEngine?.Dispose();
        _avEngine = null;
        _source?.Dispose();
        _source = null;
    }

    private partial long PlatformPlayedFrames()
    {
        // Lo renderizado menos lo que aún está en el búfer/latencia de salida.
        long latencyFrames = (long)(_latencySeconds * Engine.SampleRate);
        return Math.Max(0, Interlocked.Read(ref _renderedFrames) - latencyFrames);
    }
}
