using System;
using PokeSharp.Engine.UI.Debug.Core;

namespace PokeSharp.Engine.UI.Debug.Components.Base;

/// <summary>
/// Base class for UI components that can contain other components.
/// Handles child layout and rendering.
/// </summary>
public abstract class UIContainer : UIComponent
{
    /// <summary>Child components</summary>
    protected readonly List<UIComponent> Children = new();

    /// <summary>
    /// Adds a child component.
    /// </summary>
    public void AddChild(UIComponent child)
    {
        if (child == null)
            throw new ArgumentNullException(nameof(child));

        Children.Add(child);
    }

    /// <summary>
    /// Removes a child component.
    /// </summary>
    public void RemoveChild(UIComponent child)
    {
        Children.Remove(child);
    }

    /// <summary>
    /// Removes all child components.
    /// </summary>
    public void ClearChildren()
    {
        Children.Clear();
    }

    /// <summary>
    /// Renders this container and its children.
    /// </summary>
    protected override void OnRender(UIContext context)
    {
        Console.WriteLine($"[UIContainer.OnRender] START for {Id} - Rect=({Rect.X},{Rect.Y},{Rect.Width}x{Rect.Height})");

        // Render container background/borders
        Console.WriteLine($"[UIContainer.OnRender] Calling OnRenderContainer for {Id}");
        OnRenderContainer(context);
        Console.WriteLine($"[UIContainer.OnRender] Finished OnRenderContainer for {Id}");

        // Calculate content rect by applying padding to this container's rect
        var paddingLeft = Constraint.GetPaddingLeft();
        var paddingTop = Constraint.GetPaddingTop();
        var paddingRight = Constraint.GetPaddingRight();
        var paddingBottom = Constraint.GetPaddingBottom();
            Console.WriteLine($"[UIContainer.OnRender] {Id} Padding: L={paddingLeft}, T={paddingTop}, R={paddingRight}, B={paddingBottom}");

            // Begin container for children (sets up coordinate space and clipping)
            // The content area is the container's rect minus padding
            // Calculate offsets relative to parent's ContentRect
            var parentContentRect = context.CurrentContainer.ContentRect;
            var relativeOffsetX = Rect.X - parentContentRect.X + paddingLeft;
            var relativeOffsetY = Rect.Y - parentContentRect.Y + paddingTop;

            Console.WriteLine($"[UIContainer.OnRender] {Id} Parent ContentRect=({parentContentRect.X},{parentContentRect.Y},{parentContentRect.Width}x{parentContentRect.Height}), This Rect=({Rect.X},{Rect.Y}), RelativeOffset=({relativeOffsetX},{relativeOffsetY})");

            var contentRect = context.BeginContainer(Id + "_content", new PokeSharp.Engine.UI.Debug.Layout.LayoutConstraint
            {
                Anchor = PokeSharp.Engine.UI.Debug.Layout.Anchor.TopLeft,
                OffsetX = relativeOffsetX,
                OffsetY = relativeOffsetY,
                Width = Rect.Width - paddingLeft - paddingRight,
                Height = Rect.Height - paddingTop - paddingBottom
            });
            Console.WriteLine($"[UIContainer.OnRender] ContentRect for {Id} = ({contentRect.X},{contentRect.Y},{contentRect.Width}x{contentRect.Height})");

        // Render children
        Console.WriteLine($"[UIContainer.OnRender] Calling OnRenderChildren for {Id}");
        OnRenderChildren(context);
        Console.WriteLine($"[UIContainer.OnRender] Finished OnRenderChildren for {Id}");

        // End container
        context.EndContainer();
        Console.WriteLine($"[UIContainer.OnRender] END for {Id}");
    }

    /// <summary>
    /// Override to render the container itself (background, borders, etc.).
    /// </summary>
    protected virtual void OnRenderContainer(UIContext context)
    {
    }

    /// <summary>
    /// Override to customize child rendering.
    /// Default implementation renders all children in order.
    /// </summary>
    protected virtual void OnRenderChildren(UIContext context)
    {
        Console.WriteLine($"[UIContainer.OnRenderChildren] Rendering {Children.Count} children for {Id}");
        foreach (var child in Children)
        {
            Console.WriteLine($"[UIContainer.OnRenderChildren]   About to render child: Id={child.Id}, Type={child.GetType().Name}, Visible={child.Visible}");
            child.Render(context);
            Console.WriteLine($"[UIContainer.OnRenderChildren]   Finished rendering child: {child.Id}");
        }
        Console.WriteLine($"[UIContainer.OnRenderChildren] Finished rendering all children for {Id}");
    }
}

