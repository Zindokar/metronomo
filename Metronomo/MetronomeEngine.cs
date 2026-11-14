using NAudio.Wave;

namespace Metronomo;

/// <summary>
/// Genera los clics directamente dentro del callback de audio, contando muestras.
/// Así el tempo tiene precisión de muestra (≈0,02 ms a 48 kHz) y no depende de
/// temporizadores del sistema ni del hilo de la interfaz.
/// </summary>
public sealed class MetronomeEngine : ISampleProvider
{
    public WaveFormat WaveFormat { get; }

    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly float[] _accentClick;
    private readonly float[] _beatClick;
    private readonly float[] _subClick;

    // Parámetros (escritos por la UI, leídos por el hilo de audio).
    private volatile int _bpm = 120;
    private volatile int _beatsPerBar = 4;
    private volatile int _subdivision = 1;
    private volatile bool _accentFirst = true;
    private float _masterVolume = 0.8f;
    private float _subVolume = 0.85f;

    // Estado del hilo de audio.
    private double _samplesToNextTick;
    private int _beatIndex;
    private int _subIndex;
    private long _samplePosition;
    private float[]? _current;
    private int _currentPos;
    private float _currentGain;

    // Cola de eventos para sincronizar el indicador visual con lo que realmente suena.
    private readonly object _eventLock = new();
    private readonly Queue<TickEvent> _events = new();

    public readonly record struct TickEvent(long SamplePosition, int Beat, int Sub);

    public MetronomeEngine(int sampleRate, int channels)
    {
        _sampleRate = sampleRate;
        _channels = channels;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        _accentClick = MakeClick(1760, 1.0f);
        _beatClick = MakeClick(1320, 0.9f);
        _subClick = MakeClick(2400, 0.85f);
    }

    public int Bpm { get => _bpm; set => _bpm = Math.Clamp(value, 20, 400); }
    public int BeatsPerBar { get => _beatsPerBar; set => _beatsPerBar = Math.Clamp(value, 1, 16); }
    public int Subdivision { get => _subdivision; set => _subdivision = Math.Clamp(value, 1, 8); }
    public bool AccentFirst { get => _accentFirst; set => _accentFirst = value; }
    public float MasterVolume { get => Volatile.Read(ref _masterVolume); set => Volatile.Write(ref _masterVolume, Math.Clamp(value, 0f, 1f)); }
    public float SubVolume { get => Volatile.Read(ref _subVolume); set => Volatile.Write(ref _subVolume, Math.Clamp(value, 0f, 1f)); }

    /// <summary>Reinicia al primer tiempo del compás. Llamar antes de empezar a reproducir.</summary>
    public void Reset()
    {
        _samplesToNextTick = 0;
        _beatIndex = 0;
        _subIndex = 0;
        _samplePosition = 0;
        _current = null;
        lock (_eventLock) _events.Clear();
    }

    public int Read(float[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public int Read(Span<float> buffer)
    {
        const int offset = 0;
        int frames = buffer.Length / _channels;
        float master = MasterVolume;

        for (int f = 0; f < frames; f++)
        {
            if (_samplesToNextTick <= 0)
            {
                TriggerTick();
                _samplesToNextTick += _sampleRate * 60.0 / (_bpm * (double)_subdivision);
            }

            float s = 0f;
            if (_current != null)
            {
                s = _current[_currentPos++] * _currentGain * master;
                if (_currentPos >= _current.Length) _current = null;
            }

            int idx = offset + f * _channels;
            for (int c = 0; c < _channels; c++) buffer[idx + c] = s;

            _samplesToNextTick -= 1.0;
            _samplePosition++;
        }
        return frames * _channels;
    }

    private void TriggerTick()
    {
        int beats = _beatsPerBar;
        int subdiv = _subdivision;
        if (_beatIndex >= beats) _beatIndex = 0;
        if (_subIndex >= subdiv) { _subIndex = 0; _beatIndex = (_beatIndex + 1) % beats; }

        if (_subIndex == 0)
        {
            bool accent = _accentFirst && _beatIndex == 0 && beats > 1;
            _current = accent ? _accentClick : _beatClick;
            _currentGain = 1f;
        }
        else
        {
            _current = _subClick;
            _currentGain = SubVolume;
            if (_currentGain <= 0f) _current = null;
        }
        _currentPos = 0;

        lock (_eventLock)
        {
            _events.Enqueue(new TickEvent(_samplePosition, _beatIndex, _subIndex));
            while (_events.Count > 256) _events.Dequeue();
        }

        // Avanzar al siguiente tick.
        _subIndex++;
        if (_subIndex >= subdiv) { _subIndex = 0; _beatIndex = (_beatIndex + 1) % beats; }
    }

    /// <summary>Devuelve el último tick que ya ha sonado según la posición de reproducción real.</summary>
    public TickEvent? GetLatestPlayed(long playedSamples)
    {
        TickEvent? latest = null;
        lock (_eventLock)
        {
            while (_events.Count > 0 && _events.Peek().SamplePosition <= playedSamples)
                latest = _events.Dequeue();
        }
        return latest;
    }

    private float[] MakeClick(double freq, float amp)
    {
        int len = (int)(_sampleRate * 0.05);            // 50 ms
        int attack = Math.Max(1, (int)(_sampleRate * 0.001)); // 1 ms de ataque para evitar "pops"
        var data = new float[len];
        for (int i = 0; i < len; i++)
        {
            double t = (double)i / _sampleRate;
            double env = Math.Exp(-t / 0.012) * Math.Min(1.0, (double)i / attack);
            data[i] = (float)(amp * env * Math.Sin(2 * Math.PI * freq * t));
        }
        return data;
    }
}
