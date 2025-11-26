using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
/// Builder for creating VariablesPanel with customizable components.
/// </summary>
public class VariablesPanelBuilder
{
    private TextBuffer? _variablesBuffer;
    private int _maxLines = 1000;

    public static VariablesPanelBuilder Create() => new();

    public VariablesPanelBuilder WithVariablesBuffer(TextBuffer buffer)
    {
        _variablesBuffer = buffer;
        return this;
    }

    public VariablesPanelBuilder WithMaxLines(int maxLines)
    {
        _maxLines = maxLines;
        return this;
    }

    public VariablesPanel Build()
    {
        return new VariablesPanel(
            _variablesBuffer ?? CreateDefaultVariablesBuffer()
        );
    }

    private TextBuffer CreateDefaultVariablesBuffer()
    {
        return new TextBuffer("variables_buffer")
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

