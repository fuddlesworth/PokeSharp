using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
/// Builder for creating WatchPanel with customizable components.
/// </summary>
public class WatchPanelBuilder
{
    private TextBuffer? _watchBuffer;
    private int _maxLines = 1000;
    private double _updateInterval = 0.5;
    private bool _autoUpdate = true;

    public static WatchPanelBuilder Create() => new();

    public WatchPanelBuilder WithWatchBuffer(TextBuffer buffer)
    {
        _watchBuffer = buffer;
        return this;
    }

    public WatchPanelBuilder WithMaxLines(int maxLines)
    {
        _maxLines = maxLines;
        return this;
    }

    public WatchPanelBuilder WithUpdateInterval(double seconds)
    {
        _updateInterval = Math.Clamp(seconds, WatchPanel.MinUpdateInterval, WatchPanel.MaxUpdateInterval);
        return this;
    }

    public WatchPanelBuilder WithAutoUpdate(bool enabled)
    {
        _autoUpdate = enabled;
        return this;
    }

    public WatchPanel Build()
    {
        return new WatchPanel(
            _watchBuffer ?? CreateDefaultWatchBuffer(),
            _updateInterval,
            _autoUpdate
        );
    }

    private TextBuffer CreateDefaultWatchBuffer()
    {
        return new TextBuffer("watch_buffer")
        {
            BackgroundColor = UITheme.Dark.ConsoleOutputBackground,
            AutoScroll = false,
            MaxLines = _maxLines,
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.Fill
            }
        };
    }
}

