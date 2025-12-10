using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Engine.UI.Components.Base;
using MonoBallFramework.Game.Engine.UI.Components.Controls;
using MonoBallFramework.Game.Engine.UI.Core;
using MonoBallFramework.Game.Engine.UI.Layout;
using MonoBallFramework.Game.Engine.UI.Models;

// Uses NerdFontIcons from MonoBallFramework.Game.Engine.UI.Core

namespace MonoBallFramework.Game.Engine.UI.Components.Debug;

/// <summary>
///     A scrollable pane that displays detailed component information for a selected entity.
///     Used as the right pane in a dual-pane entity inspector.
/// </summary>
public class ComponentDetailPane : UIComponent
{
    private const int MaxComponentsToShow = 50;
    private const int MaxPropertiesToShow = 20;
    private const int MaxDetailBufferLines = 10000;
    private const int MaxRelationshipsDisplay = 10;
    private const int MaxRelationshipMetadataFields = 5;
    private const int MaxPropertyValueLength = 50;

    private readonly TextBuffer _detailBuffer;
    private EntityInfo? _entity;

    /// <summary>
    ///     Callback for loading entity details on-demand (lazy loading).
    /// </summary>
    private Func<int, EntityInfo, EntityInfo?>? _entityDetailLoader;

    /// <summary>
    ///     Cache for loaded entity details.
    /// </summary>
    private readonly Dictionary<int, EntityInfo> _loadedEntityCache = new();

    public ComponentDetailPane(string id)
    {
        Id = id;

        _detailBuffer = new TextBuffer($"{id}_buffer")
        {
            AutoScroll = false,
            MaxLines = MaxDetailBufferLines,
        };
    }

    /// <summary>Gets the underlying TextBuffer for direct access if needed.</summary>
    public TextBuffer DetailBuffer => _detailBuffer;

    /// <summary>Gets the currently displayed entity.</summary>
    public EntityInfo? Entity => _entity;

    /// <summary>
    ///     Sets the entity detail loader for on-demand loading of component data.
    /// </summary>
    public void SetEntityDetailLoader(Func<int, EntityInfo, EntityInfo?>? loader)
    {
        _entityDetailLoader = loader;
    }

    /// <summary>
    ///     Clears the loaded entity cache.
    /// </summary>
    public void ClearCache()
    {
        _loadedEntityCache.Clear();
    }

    /// <summary>
    ///     Sets the entity to display details for.
    /// </summary>
    public void SetEntity(EntityInfo? entity)
    {
        _entity = entity;
        UpdateDisplay();
    }

    /// <summary>
    ///     Refreshes the display (useful after entity data changes).
    /// </summary>
    public void Refresh()
    {
        UpdateDisplay();
    }

    protected override bool IsInteractive() => true;

    protected override void OnRender(UIContext context)
    {
        // Update buffer constraint to fill this component's rect
        _detailBuffer.Constraint = new LayoutConstraint
        {
            Anchor = Anchor.Fill,
        };

        // Render the buffer
        _detailBuffer.Render(context);
    }

    private void UpdateDisplay()
    {
        int previousScrollOffset = _detailBuffer.ScrollOffset;
        _detailBuffer.Clear();

        if (_entity == null)
        {
            RenderEmptyState();
            return;
        }

        // Load entity details on-demand if needed
        EntityInfo entity = LoadEntityDetails(_entity);

        RenderEntityHeader(entity);
        RenderRelationships(entity);
        RenderComponents(entity);

        // Restore scroll position
        _detailBuffer.SetScrollOffset(Math.Min(previousScrollOffset, Math.Max(0, _detailBuffer.TotalLines - 1)));
    }

    private EntityInfo LoadEntityDetails(EntityInfo entity)
    {
        if (_entityDetailLoader == null)
        {
            return entity;
        }

        // Check cache first
        if (_loadedEntityCache.TryGetValue(entity.Id, out EntityInfo? cachedEntity))
        {
            return cachedEntity;
        }

        // Load if needed (empty components = lightweight mode)
        if (entity.Components.Count == 0 || entity.ComponentData.Count == 0)
        {
            EntityInfo? loaded = _entityDetailLoader(entity.Id, entity);
            if (loaded != null)
            {
                _loadedEntityCache[entity.Id] = loaded;
                return loaded;
            }
        }

        return entity;
    }

    private void RenderEmptyState()
    {
        _detailBuffer.AppendLine("", ThemeManager.Current.TextDim);
        _detailBuffer.AppendLine("  Select an entity to view details", ThemeManager.Current.TextDim);
        _detailBuffer.AppendLine("", ThemeManager.Current.TextDim);
        _detailBuffer.AppendLine("  Use arrow keys or click to select", ThemeManager.Current.TextDim);
    }

    private void RenderEntityHeader(EntityInfo entity)
    {
        UITheme theme = ThemeManager.Current;

        // Entity name and ID
        _detailBuffer.AppendLine($"  {NerdFontIcons.Entity} Entity Details", theme.Info);
        _detailBuffer.AppendLine($"  ─────────────────────", theme.BorderPrimary);
        _detailBuffer.AppendLine($"  ID: {entity.Id}", theme.TextPrimary);
        _detailBuffer.AppendLine($"  Name: {entity.Name}", theme.TextPrimary);

        if (entity.Tag != null && entity.Tag != entity.Name)
        {
            _detailBuffer.AppendLine($"  Tag: {entity.Tag}", theme.TextSecondary);
        }

        _detailBuffer.AppendLine($"  Active: {(entity.IsActive ? "Yes" : "No")}",
            entity.IsActive ? theme.Success : theme.TextDim);
        _detailBuffer.AppendLine($"  Components: {entity.Components.Count}", theme.TextSecondary);

        if (entity.Relationships.Count > 0)
        {
            int totalRelationships = entity.Relationships.Values.Sum(r => r.Count);
            _detailBuffer.AppendLine($"  Relationships: {totalRelationships}", theme.TextSecondary);
        }

        _detailBuffer.AppendLine("", theme.TextDim);
    }

    private void RenderRelationships(EntityInfo entity)
    {
        if (entity.Relationships.Count == 0)
        {
            return;
        }

        UITheme theme = ThemeManager.Current;

        _detailBuffer.AppendLine($"  {NerdFontIcons.Relationship} Relationships", theme.Warning);
        _detailBuffer.AppendLine($"  ─────────────────────", theme.BorderPrimary);

        foreach ((string relationType, List<EntityRelationship> relationships) in entity.Relationships)
        {
            _detailBuffer.AppendLine($"    {relationType}:", theme.Info);

            foreach (EntityRelationship rel in relationships.Take(MaxRelationshipsDisplay))
            {
                string entityRef = rel.EntityName != null
                    ? $"[{rel.EntityId}] {rel.EntityName}"
                    : $"[{rel.EntityId}]";

                Color relColor = rel.IsValid ? theme.Success : theme.Error;
                _detailBuffer.AppendLine($"      → {entityRef}", relColor);

                // Show relationship metadata
                foreach ((string key, string value) in rel.Metadata.Take(MaxRelationshipMetadataFields))
                {
                    _detailBuffer.AppendLine($"        {key}: {value}", theme.TextDim);
                }
            }

            if (relationships.Count > MaxRelationshipsDisplay)
            {
                _detailBuffer.AppendLine($"      ... ({relationships.Count - MaxRelationshipsDisplay} more)", theme.TextDim);
            }
        }

        _detailBuffer.AppendLine("", theme.TextDim);
    }

    private void RenderComponents(EntityInfo entity)
    {
        UITheme theme = ThemeManager.Current;

        _detailBuffer.AppendLine($"  {NerdFontIcons.Component} Components", theme.Info);
        _detailBuffer.AppendLine($"  ─────────────────────", theme.BorderPrimary);

        if (entity.Components.Count == 0)
        {
            _detailBuffer.AppendLine("    No components attached", theme.TextDim);
            return;
        }

        int componentsShown = 0;
        foreach (string component in entity.Components.Take(MaxComponentsToShow))
        {
            Color componentColor = GetComponentColor(component);
            _detailBuffer.AppendLine($"    {NerdFontIcons.Dot} {component}", componentColor);

            // Show component field values if available
            if (entity.ComponentData.TryGetValue(component, out Dictionary<string, string>? fields) && fields.Count > 0)
            {
                var sortedFields = fields.OrderBy(kvp => kvp.Key).Take(MaxPropertiesToShow).ToList();

                foreach ((string fieldName, string fieldValue) in sortedFields)
                {
                    RenderPropertyValue(fieldName, fieldValue, "        ");
                }

                if (fields.Count > MaxPropertiesToShow)
                {
                    _detailBuffer.AppendLine($"        ... ({fields.Count - MaxPropertiesToShow} more fields)",
                        theme.TextDim);
                }
            }

            componentsShown++;
        }

        if (entity.Components.Count > MaxComponentsToShow)
        {
            _detailBuffer.AppendLine($"    ... ({entity.Components.Count - MaxComponentsToShow} more components)",
                theme.TextDim);
        }
    }

    private void RenderPropertyValue(string fieldName, string fieldValue, string indent)
    {
        UITheme theme = ThemeManager.Current;

        // Determine value color based on content
        Color valueColor = theme.TextSecondary;

        if (fieldValue == "null" || fieldValue == "None" || fieldValue == "N/A")
        {
            valueColor = theme.TextDim;
        }
        else if (fieldValue.StartsWith("[") && fieldValue.EndsWith("]"))
        {
            // Entity reference
            valueColor = theme.Info;
        }
        else if (bool.TryParse(fieldValue, out bool boolVal))
        {
            valueColor = boolVal ? theme.Success : theme.Error;
        }
        else if (fieldValue.Contains("Error") || fieldValue.Contains("Invalid"))
        {
            valueColor = theme.Error;
        }

        // Truncate long values
        string displayValue = fieldValue;
        if (displayValue.Length > MaxPropertyValueLength)
        {
            displayValue = displayValue.Substring(0, MaxPropertyValueLength - 3) + "...";
        }

        _detailBuffer.AppendLine($"{indent}{fieldName}: {displayValue}", valueColor);
    }

    private Color GetComponentColor(string componentName)
    {
        UITheme theme = ThemeManager.Current;

        // Color-code components by category
        if (componentName.Contains("Transform") || componentName.Contains("Position"))
        {
            return theme.Info;
        }

        if (componentName.Contains("Sprite") || componentName.Contains("Render") || componentName.Contains("Visual"))
        {
            return theme.Success;
        }

        if (componentName.Contains("Collider") || componentName.Contains("Physics") || componentName.Contains("Body"))
        {
            return theme.Warning;
        }

        if (componentName.Contains("Script") || componentName.Contains("Behavior") || componentName.Contains("AI"))
        {
            return theme.Error;
        }

        if (componentName.Contains("Audio") || componentName.Contains("Sound"))
        {
            return theme.SyntaxType;
        }

        return theme.TextPrimary;
    }

    /// <summary>
    ///     Converts HSL color to RGB.
    /// </summary>
    private static Color HslToRgb(float h, float s, float l)
    {
        float c = (1 - Math.Abs((2 * l) - 1)) * s;
        float x = c * (1 - Math.Abs((h / 60 % 2) - 1));
        float m = l - (c / 2);

        float r, g, b;

        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return new Color((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }
}

