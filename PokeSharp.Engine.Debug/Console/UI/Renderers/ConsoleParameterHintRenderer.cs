using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Debug.Console.Configuration;
using PokeSharp.Engine.Debug.Console.Features;
using static PokeSharp.Engine.Debug.Console.Configuration.ConsoleColors;

namespace PokeSharp.Engine.Debug.Console.UI.Renderers;

/// <summary>
///     Handles rendering of parameter hints tooltip.
///     Separated from QuakeConsole to follow Single Responsibility Principle.
/// </summary>
public class ConsoleParameterHintRenderer
{
    private readonly ConsoleFontRenderer _fontRenderer;
    private readonly Texture2D _pixel;
    private readonly SpriteBatch _spriteBatch;

    public ConsoleParameterHintRenderer(
        ConsoleFontRenderer fontRenderer,
        SpriteBatch spriteBatch,
        Texture2D pixel
    )
    {
        _fontRenderer = fontRenderer;
        _spriteBatch = spriteBatch;
        _pixel = pixel;
    }

    /// <summary>
    ///     Draws parameter hints tooltip showing method signature and highlighting current parameter.
    /// </summary>
    public void DrawParameterHints(
        int bottomY,
        int lineHeight,
        ParameterHintInfo parameterHints,
        int currentParameterIndex
    )
    {
        if (parameterHints == null || parameterHints.Overloads.Count == 0)
            return;

        var currentOverload = parameterHints.Overloads[parameterHints.CurrentOverloadIndex];
        var signatureLines = BuildSignatureLines(
            parameterHints,
            currentOverload,
            currentParameterIndex
        );

        // Calculate panel size
        var padding = ConsoleConstants.Rendering.ParameterHintPadding;
        var maxWidth = CalculateMaxWidth(signatureLines);

        var panelWidth = maxWidth + padding * 2;
        var panelHeight = signatureLines.Count * lineHeight + padding * 2;
        var panelX = ConsoleConstants.Rendering.Padding + 30;
        var panelY = bottomY - panelHeight - ConsoleConstants.Rendering.ParameterHintGapFromInput;

        // Draw background
        DrawRectangle(panelX, panelY, panelWidth, panelHeight, Background_Elevated);

        // Draw border
        DrawBorder(panelX, panelY, panelWidth, panelHeight);

        // Draw signature text
        DrawSignatureText(signatureLines, parameterHints, panelX, panelY, padding, lineHeight);
    }

    private List<string> BuildSignatureLines(
        ParameterHintInfo hints,
        MethodSignature currentOverload,
        int currentParameterIndex
    )
    {
        var lines = new List<string>();

        // Show overload count if multiple
        if (hints.Overloads.Count > 1)
            lines.Add($"↑↓ {hints.CurrentOverloadIndex + 1} of {hints.Overloads.Count}");

        // Method return type and name
        lines.Add($"{currentOverload.ReturnType} {currentOverload.MethodName}(");

        // Parameters - mark current one
        if (currentOverload.Parameters.Count > 0)
            for (var i = 0; i < currentOverload.Parameters.Count; i++)
            {
                var param = currentOverload.Parameters[i];
                var isCurrentParam = i == currentParameterIndex;

                var paramText = $"  {param.Type} {param.Name}";
                if (param.IsOptional && param.DefaultValue != null)
                    paramText += $" = {param.DefaultValue}";

                if (i < currentOverload.Parameters.Count - 1)
                    paramText += ",";

                if (isCurrentParam)
                    paramText = $"> {paramText}"; // Use ASCII instead of Unicode arrow

                lines.Add(paramText);
            }

        lines.Add(")");
        return lines;
    }

    private int CalculateMaxWidth(List<string> lines)
    {
        var maxWidth = 0;
        foreach (var line in lines)
        {
            var size = _fontRenderer.MeasureString(line);
            if (size.X > maxWidth)
                maxWidth = (int)size.X;
        }

        return maxWidth;
    }

    private void DrawBorder(int panelX, int panelY, int panelWidth, int panelHeight)
    {
        var borderWidth = ConsoleConstants.Rendering.ParameterHintBorderWidth;
        var borderColor = Border_Default;

        DrawRectangle(panelX, panelY, panelWidth, borderWidth, borderColor); // Top
        DrawRectangle(
            panelX,
            panelY + panelHeight - borderWidth,
            panelWidth,
            borderWidth,
            borderColor
        ); // Bottom
        DrawRectangle(panelX, panelY, borderWidth, panelHeight, borderColor); // Left
        DrawRectangle(
            panelX + panelWidth - borderWidth,
            panelY,
            borderWidth,
            panelHeight,
            borderColor
        ); // Right
    }

    private void DrawSignatureText(
        List<string> lines,
        ParameterHintInfo hints,
        int panelX,
        int panelY,
        int padding,
        int lineHeight
    )
    {
        var textY = panelY + padding;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var color = GetLineColor(line, i, hints.Overloads.Count);
            _fontRenderer.DrawString(line, panelX + padding, textY, color);
            textY += lineHeight;
        }
    }

    private Color GetLineColor(string line, int lineIndex, int overloadCount)
    {
        // First line (overload indicator) in yellow
        if (lineIndex == 0 && overloadCount > 1)
            return Info_Dim;

        // Current parameter line in highlight color
        if (line.StartsWith("> "))
            return Primary;

        // Method signature line
        if (lineIndex == (overloadCount > 1 ? 1 : 0))
            return Success;

        return Text_Primary;
    }

    private void DrawRectangle(int x, int y, int width, int height, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), color);
    }
}
