using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Components.Layout;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Layout;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
/// Panel for viewing script variables and their values.
/// </summary>
public class VariablesPanel : Panel
{
    private readonly TextBuffer _variablesBuffer;
    private readonly Dictionary<string, VariableInfo> _variables = new();
    private readonly List<GlobalInfo> _globals = new();

    public class VariableInfo
    {
        public string Name { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public Func<object?> ValueGetter { get; set; } = () => null;
    }

    public class GlobalInfo
    {
        public string Name { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Creates a VariablesPanel with the specified components.
    /// Use <see cref="VariablesPanelBuilder"/> to construct instances.
    /// </summary>
    internal VariablesPanel(TextBuffer variablesBuffer)
    {
        _variablesBuffer = variablesBuffer;

        Id = "variables_panel";
        BackgroundColor = UITheme.Dark.ConsoleBackground;
        BorderColor = UITheme.Dark.BorderPrimary;
        BorderThickness = 1;
        Constraint.Padding = UITheme.Dark.PaddingMedium;

        AddChild(_variablesBuffer);
    }

    /// <summary>
    /// Adds or updates a script variable.
    /// </summary>
    public void SetVariable(string name, string typeName, Func<object?> valueGetter)
    {
        _variables[name] = new VariableInfo
        {
            Name = name,
            TypeName = typeName,
            ValueGetter = valueGetter
        };

        UpdateVariableDisplay();
    }

    /// <summary>
    /// Removes a script variable.
    /// </summary>
    public void RemoveVariable(string name)
    {
        if (_variables.Remove(name))
        {
            UpdateVariableDisplay();
        }
    }

    /// <summary>
    /// Clears all script variables.
    /// </summary>
    public void ClearVariables()
    {
        _variables.Clear();
        UpdateVariableDisplay();
    }

    /// <summary>
    /// Sets the list of global variables available in the script environment.
    /// </summary>
    public void SetGlobals(IEnumerable<GlobalInfo> globals)
    {
        _globals.Clear();
        _globals.AddRange(globals);
        UpdateVariableDisplay();
    }

    /// <summary>
    /// Forces an immediate update of the variable display.
    /// </summary>
    public void UpdateVariableDisplay()
    {
        _variablesBuffer.Clear();

        // Display header
        _variablesBuffer.AppendLine("═══════════════════════════════════════════════════════════════════", UITheme.Dark.Success);
        _variablesBuffer.AppendLine($"  SCRIPT VARIABLES ({_variables.Count} defined)", UITheme.Dark.Success);
        _variablesBuffer.AppendLine("═══════════════════════════════════════════════════════════════════", UITheme.Dark.Success);
        _variablesBuffer.AppendLine("", Color.White);

        // Display user-defined variables
        if (_variables.Count == 0)
        {
            _variablesBuffer.AppendLine("No variables defined in current script state.", UITheme.Dark.TextDim);
            _variablesBuffer.AppendLine("", Color.White);
            _variablesBuffer.AppendLine("TIP: Create variables in C# scripts:", UITheme.Dark.TextSecondary);
            _variablesBuffer.AppendLine("     var myVar = 42;", UITheme.Dark.Info);
            _variablesBuffer.AppendLine("     var playerPos = Player.GetPlayerPosition();", UITheme.Dark.Info);
        }
        else
        {
            foreach (var kvp in _variables.OrderBy(v => v.Key))
            {
                var variable = kvp.Value;
                try
                {
                    var value = variable.ValueGetter();
                    var valueStr = FormatValue(value);
                    var typeStr = variable.TypeName.PadRight(20);

                    // Format: "name        type                 value"
                    _variablesBuffer.AppendLine($"  {variable.Name,-20} {typeStr} {valueStr}", UITheme.Dark.TextPrimary);
                }
                catch (Exception ex)
                {
                    _variablesBuffer.AppendLine($"  {variable.Name,-20} {variable.TypeName,-20} [Error: {ex.Message}]", UITheme.Dark.Error);
                }
            }
        }

        // Display globals section
        if (_globals.Count > 0)
        {
            _variablesBuffer.AppendLine("", Color.White);
            _variablesBuffer.AppendLine("", Color.White);
            _variablesBuffer.AppendLine("═══════════════════════════════════════════════════════════════════", UITheme.Dark.Info);
            _variablesBuffer.AppendLine("  BUILT-IN GLOBALS", UITheme.Dark.Info);
            _variablesBuffer.AppendLine("═══════════════════════════════════════════════════════════════════", UITheme.Dark.Info);
            _variablesBuffer.AppendLine("", Color.White);
            _variablesBuffer.AppendLine("These objects are always available in scripts:", UITheme.Dark.TextSecondary);
            _variablesBuffer.AppendLine("", Color.White);

            foreach (var global in _globals.OrderBy(g => g.Name))
            {
                var typeStr = global.TypeName.PadRight(30);
                _variablesBuffer.AppendLine($"  {global.Name,-15} {typeStr}", UITheme.Dark.Info);
                if (!string.IsNullOrEmpty(global.Description))
                {
                    _variablesBuffer.AppendLine($"    → {global.Description}", UITheme.Dark.TextSecondary);
                }
            }
        }

        // Footer
        _variablesBuffer.AppendLine("", Color.White);
        _variablesBuffer.AppendLine("─────────────────────────────────────────────────────────────────", UITheme.Dark.BorderPrimary);
        _variablesBuffer.AppendLine($"User Variables: {_variables.Count} | Globals: {_globals.Count}", UITheme.Dark.TextSecondary);
        _variablesBuffer.AppendLine("TIP: Use 'script reset' to clear all user variables", UITheme.Dark.TextSecondary);
    }

    /// <summary>
    /// Formats a value for display.
    /// </summary>
    private string FormatValue(object? value)
    {
        if (value == null)
            return "<null>";

        var type = value.GetType();

        // Handle primitives
        if (type.IsPrimitive || type == typeof(string))
        {
            if (value is bool b)
                return b ? "true" : "false";
            if (value is float f)
                return f.ToString("F2");
            if (value is double d)
                return d.ToString("F2");
            if (value is string s)
                return $"\"{s}\"";
            return value.ToString() ?? "<null>";
        }

        // Handle Vector2
        if (value is Vector2 v2)
            return $"({v2.X:F2}, {v2.Y:F2})";

        // Handle Vector3
        if (value is Microsoft.Xna.Framework.Vector3 v3)
            return $"({v3.X:F2}, {v3.Y:F2}, {v3.Z:F2})";

        // Handle Color
        if (value is Color color)
            return $"({color.R}, {color.G}, {color.B}, {color.A})";

        // Handle collections
        if (value is System.Collections.ICollection collection)
            return $"[{collection.Count} items]";

        // Handle DateTime
        if (value is DateTime dt)
            return dt.ToString("HH:mm:ss");

        // Default: type name
        return $"<{type.Name} instance>";
    }

    /// <summary>
    /// Gets the count of user-defined variables.
    /// </summary>
    public int GetVariableCount() => _variables.Count;

    /// <summary>
    /// Gets the count of global variables.
    /// </summary>
    public int GetGlobalCount() => _globals.Count;
}

