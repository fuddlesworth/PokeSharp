using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Components.Layout;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
/// A panel that displays performance and system statistics.
/// Example debug component using the new UI framework.
/// </summary>
public class StatsPanel : Panel
{
    private Label? _fpsLabel;
    private Label? _memoryLabel;
    private Label? _entityCountLabel;
    private Label? _drawCallsLabel;
    private Label? _updateTimeLabel;
    private Label? _renderTimeLabel;

    // Stats data (would be populated from game systems)
    public float Fps { get; set; } = 60.0f;
    public long MemoryUsageMB { get; set; } = 128;
    public int EntityCount { get; set; } = 1234;
    public int DrawCalls { get; set; } = 42;
    public double UpdateTimeMs { get; set; } = 5.2;
    public double RenderTimeMs { get; set; } = 11.8;

    /// <summary>
    /// Creates a StatsPanel with the specified initial values.
    /// Use <see cref="StatsPanelBuilder"/> to construct instances.
    /// </summary>
    internal StatsPanel(float fps, long memoryMB, int entityCount, int drawCalls)
    {
        Fps = fps;
        MemoryUsageMB = memoryMB;
        EntityCount = entityCount;
        DrawCalls = drawCalls;

        BackgroundColor = UITheme.Dark.BackgroundSecondary;
        BorderColor = UITheme.Dark.BorderPrimary;
        BorderThickness = 1;

        InitializeLabels();
    }

    private void InitializeLabels()
    {
        var theme = UITheme.Dark;

        // Title
        var titleLabel = new Label
        {
            Id = Id + "_title",
            Text = "Performance Stats",
            Color = theme.Info,
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.TopLeft,
                Margin = theme.PaddingMedium
            }
        };
        AddChild(titleLabel);

        float yOffset = 30;
        float lineHeight = 25;

        // FPS
        _fpsLabel = CreateStatLabel("fps", yOffset, theme.Success);
        AddChild(_fpsLabel);
        yOffset += lineHeight;

        // Memory
        _memoryLabel = CreateStatLabel("memory", yOffset, theme.Warning);
        AddChild(_memoryLabel);
        yOffset += lineHeight;

        // Entity count
        _entityCountLabel = CreateStatLabel("entities", yOffset, theme.TextPrimary);
        AddChild(_entityCountLabel);
        yOffset += lineHeight;

        // Draw calls
        _drawCallsLabel = CreateStatLabel("draw_calls", yOffset, theme.TextPrimary);
        AddChild(_drawCallsLabel);
        yOffset += lineHeight;

        // Update time
        _updateTimeLabel = CreateStatLabel("update_time", yOffset, theme.Info);
        AddChild(_updateTimeLabel);
        yOffset += lineHeight;

        // Render time
        _renderTimeLabel = CreateStatLabel("render_time", yOffset, theme.Info);
        AddChild(_renderTimeLabel);
    }

    private Label CreateStatLabel(string id, float yOffset, Color color)
    {
        return new Label
        {
            Id = Id + "_" + id,
            Color = color,
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.TopLeft,
                OffsetY = yOffset,
                MarginLeft = UITheme.Dark.PaddingMedium
            }
        };
    }

    protected override void OnRenderContainer(UIContext context)
    {
        base.OnRenderContainer(context);

        // Update label texts with current stats
        if (_fpsLabel != null)
            _fpsLabel.Text = $"FPS: {Fps:F1}";

        if (_memoryLabel != null)
            _memoryLabel.Text = $"Memory: {MemoryUsageMB} MB";

        if (_entityCountLabel != null)
            _entityCountLabel.Text = $"Entities: {EntityCount:N0}";

        if (_drawCallsLabel != null)
            _drawCallsLabel.Text = $"Draw Calls: {DrawCalls}";

        if (_updateTimeLabel != null)
            _updateTimeLabel.Text = $"Update: {UpdateTimeMs:F2}ms";

        if (_renderTimeLabel != null)
            _renderTimeLabel.Text = $"Render: {RenderTimeMs:F2}ms";
    }
}

