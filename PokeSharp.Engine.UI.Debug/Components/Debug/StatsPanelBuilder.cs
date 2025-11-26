namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
/// Builder for creating StatsPanel with customizable configuration.
/// </summary>
public class StatsPanelBuilder
{
    private float _initialFps = 60.0f;
    private long _initialMemoryMB = 128;
    private int _initialEntityCount = 0;
    private int _initialDrawCalls = 0;

    public static StatsPanelBuilder Create() => new();

    public StatsPanelBuilder WithInitialFps(float fps)
    {
        _initialFps = fps;
        return this;
    }

    public StatsPanelBuilder WithInitialMemory(long memoryMB)
    {
        _initialMemoryMB = memoryMB;
        return this;
    }

    public StatsPanelBuilder WithInitialEntityCount(int count)
    {
        _initialEntityCount = count;
        return this;
    }

    public StatsPanelBuilder WithInitialDrawCalls(int count)
    {
        _initialDrawCalls = count;
        return this;
    }

    public StatsPanel Build()
    {
        return new StatsPanel(_initialFps, _initialMemoryMB, _initialEntityCount, _initialDrawCalls);
    }
}

