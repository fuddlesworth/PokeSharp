namespace MonoBallFramework.Game.Engine.Audio.Core;

/// <summary>
/// Converts mono audio to stereo by duplicating samples to both channels.
/// Thread-safe for concurrent Read() calls.
/// </summary>
public class MonoToStereoProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly AudioFormat _outputFormat;
    private readonly object _lock = new();
    private float[]? _monoBuffer;

    public MonoToStereoProvider(ISampleProvider source)
    {
        if (source.Format.Channels != 1)
            throw new ArgumentException("Source must be mono.", nameof(source));

        _source = source;
        _outputFormat = new AudioFormat(source.Format.SampleRate, 2, 32, true);
    }

    public AudioFormat Format => _outputFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        lock (_lock)
        {
            // Read mono samples
            int monoSamples = count / 2;

            // Ensure buffer
            if (_monoBuffer == null || _monoBuffer.Length < monoSamples)
                _monoBuffer = new float[monoSamples];

            int read = _source.Read(_monoBuffer, 0, monoSamples);

            // Duplicate to stereo (interleaved L R L R...)
            for (int i = 0; i < read; i++)
            {
                buffer[offset + i * 2] = _monoBuffer[i];      // Left
                buffer[offset + i * 2 + 1] = _monoBuffer[i];  // Right
            }

            return read * 2;  // Return stereo sample count
        }
    }

    /// <summary>
    /// Wraps source in mono-to-stereo converter if needed.
    /// </summary>
    public static ISampleProvider CreateIfNeeded(ISampleProvider source)
    {
        return source.Format.Channels == 1
            ? new MonoToStereoProvider(source)
            : source;
    }
}
