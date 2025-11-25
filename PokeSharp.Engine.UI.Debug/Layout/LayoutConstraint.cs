namespace PokeSharp.Engine.UI.Debug.Layout;

/// <summary>
/// Defines layout constraints for positioning and sizing a UI element.
/// These constraints are resolved into absolute coordinates during layout resolution.
/// </summary>
public class LayoutConstraint
{
    private Anchor _anchor = Anchor.TopLeft;
    private float _offsetX;
    private float _offsetY;
    private float? _width;
    private float? _height;

    /// <summary>Dirty flag - true when any constraint property has changed</summary>
    public bool IsDirty { get; private set; } = true;

    /// <summary>Clears the dirty flag after layout resolution</summary>
    public void ClearDirty() => IsDirty = false;

    private void MarkDirty() => IsDirty = true;

    /// <summary>Anchor point for positioning</summary>
    public Anchor Anchor
    {
        get => _anchor;
        set { if (_anchor != value) { _anchor = value; MarkDirty(); } }
    }

    /// <summary>X offset from anchor point (in pixels)</summary>
    public float OffsetX
    {
        get => _offsetX;
        set { if (_offsetX != value) { _offsetX = value; MarkDirty(); } }
    }

    /// <summary>Y offset from anchor point (in pixels)</summary>
    public float OffsetY
    {
        get => _offsetY;
        set { if (_offsetY != value) { _offsetY = value; MarkDirty(); } }
    }

    /// <summary>Width in pixels (null for auto-sizing)</summary>
    public float? Width
    {
        get => _width;
        set { if (_width != value) { _width = value; MarkDirty(); } }
    }

    /// <summary>Height in pixels (null for auto-sizing)</summary>
    public float? Height
    {
        get => _height;
        set { if (_height != value) { _height = value; MarkDirty(); } }
    }

    private float? _widthPercent;
    private float? _heightPercent;
    private float? _minWidth;
    private float? _minHeight;
    private float? _maxWidth;
    private float? _maxHeight;
    private float _margin;
    private float _padding;

    /// <summary>Width as percentage of parent (0.0-1.0, overrides Width if set)</summary>
    public float? WidthPercent
    {
        get => _widthPercent;
        set { if (_widthPercent != value) { _widthPercent = value; MarkDirty(); } }
    }

    /// <summary>Height as percentage of parent (0.0-1.0, overrides Height if set)</summary>
    public float? HeightPercent
    {
        get => _heightPercent;
        set { if (_heightPercent != value) { _heightPercent = value; MarkDirty(); } }
    }

    /// <summary>Minimum width constraint</summary>
    public float? MinWidth
    {
        get => _minWidth;
        set { if (_minWidth != value) { _minWidth = value; MarkDirty(); } }
    }

    /// <summary>Minimum height constraint</summary>
    public float? MinHeight
    {
        get => _minHeight;
        set { if (_minHeight != value) { _minHeight = value; MarkDirty(); } }
    }

    /// <summary>Maximum width constraint</summary>
    public float? MaxWidth
    {
        get => _maxWidth;
        set { if (_maxWidth != value) { _maxWidth = value; MarkDirty(); } }
    }

    /// <summary>Maximum height constraint</summary>
    public float? MaxHeight
    {
        get => _maxHeight;
        set { if (_maxHeight != value) { _maxHeight = value; MarkDirty(); } }
    }

    /// <summary>Margin on all sides (space outside the element)</summary>
    public float Margin
    {
        get => _margin;
        set { if (_margin != value) { _margin = value; MarkDirty(); } }
    }

    /// <summary>Padding on all sides (space inside the element)</summary>
    public float Padding
    {
        get => _padding;
        set { if (_padding != value) { _padding = value; MarkDirty(); } }
    }

    private float? _marginLeft;
    private float? _marginTop;
    private float? _marginRight;
    private float? _marginBottom;
    private float? _paddingLeft;
    private float? _paddingTop;
    private float? _paddingRight;
    private float? _paddingBottom;

    /// <summary>Left margin (overrides Margin if set)</summary>
    public float? MarginLeft
    {
        get => _marginLeft;
        set { if (_marginLeft != value) { _marginLeft = value; MarkDirty(); } }
    }

    /// <summary>Top margin (overrides Margin if set)</summary>
    public float? MarginTop
    {
        get => _marginTop;
        set { if (_marginTop != value) { _marginTop = value; MarkDirty(); } }
    }

    /// <summary>Right margin (overrides Margin if set)</summary>
    public float? MarginRight
    {
        get => _marginRight;
        set { if (_marginRight != value) { _marginRight = value; MarkDirty(); } }
    }

    /// <summary>Bottom margin (overrides Margin if set)</summary>
    public float? MarginBottom
    {
        get => _marginBottom;
        set { if (_marginBottom != value) { _marginBottom = value; MarkDirty(); } }
    }

    /// <summary>Left padding (overrides Padding if set)</summary>
    public float? PaddingLeft
    {
        get => _paddingLeft;
        set { if (_paddingLeft != value) { _paddingLeft = value; MarkDirty(); } }
    }

    /// <summary>Top padding (overrides Padding if set)</summary>
    public float? PaddingTop
    {
        get => _paddingTop;
        set { if (_paddingTop != value) { _paddingTop = value; MarkDirty(); } }
    }

    /// <summary>Right padding (overrides Padding if set)</summary>
    public float? PaddingRight
    {
        get => _paddingRight;
        set { if (_paddingRight != value) { _paddingRight = value; MarkDirty(); } }
    }

    /// <summary>Bottom padding (overrides Padding if set)</summary>
    public float? PaddingBottom
    {
        get => _paddingBottom;
        set { if (_paddingBottom != value) { _paddingBottom = value; MarkDirty(); } }
    }

    /// <summary>
    /// Gets the effective left margin.
    /// </summary>
    public float GetMarginLeft() => MarginLeft ?? Margin;

    /// <summary>
    /// Gets the effective top margin.
    /// </summary>
    public float GetMarginTop() => MarginTop ?? Margin;

    /// <summary>
    /// Gets the effective right margin.
    /// </summary>
    public float GetMarginRight() => MarginRight ?? Margin;

    /// <summary>
    /// Gets the effective bottom margin.
    /// </summary>
    public float GetMarginBottom() => MarginBottom ?? Margin;

    /// <summary>
    /// Gets the effective left padding.
    /// </summary>
    public float GetPaddingLeft() => PaddingLeft ?? Padding;

    /// <summary>
    /// Gets the effective top padding.
    /// </summary>
    public float GetPaddingTop() => PaddingTop ?? Padding;

    /// <summary>
    /// Gets the effective right padding.
    /// </summary>
    public float GetPaddingRight() => PaddingRight ?? Padding;

    /// <summary>
    /// Gets the effective bottom padding.
    /// </summary>
    public float GetPaddingBottom() => PaddingBottom ?? Padding;
}


