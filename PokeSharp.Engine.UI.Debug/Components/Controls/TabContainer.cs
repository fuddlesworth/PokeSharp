using System;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Components.Layout;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Controls;

/// <summary>
/// Tab container that manages a tab bar and content panels.
/// Only the active tab's content is rendered.
/// </summary>
public class TabContainer : Panel
{
    private readonly TabBar _tabBar;
    private readonly List<Panel> _contentPanels = new();
    private readonly List<string> _tabTitles = new();

    /// <summary>Gets the currently active tab index</summary>
    public int ActiveTabIndex => _tabBar.ActiveTabIndex;

    /// <summary>Event triggered when active tab changes</summary>
    public Action<int>? OnTabChanged { get; set; }

    public TabContainer()
    {
        // Create tab bar
        _tabBar = new TabBar
        {
            Id = $"{Id}_tabbar",
            BackgroundColor = UITheme.Dark.BackgroundSecondary,
            BorderColor = UITheme.Dark.BorderPrimary,
            BorderThickness = 1,
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.StretchTop,
                Height = UITheme.Dark.ButtonHeight
            }
        };

        _tabBar.OnTabChanged = (index) =>
        {
            // Update content panel visibility when tab changes
            for (int i = 0; i < _contentPanels.Count; i++)
            {
                _contentPanels[i].Visible = (i == index);
            }

            // Forward event to external handlers
            OnTabChanged?.Invoke(index);
        };

        AddChild(_tabBar);
    }

    /// <summary>
    /// Adds a new tab with the specified title and content panel.
    /// </summary>
    /// <param name="title">Tab title</param>
    /// <param name="content">Content panel to display when tab is active</param>
    public void AddTab(string title, Panel content)
    {
        var tabIndex = _tabTitles.Count;
        var tabId = $"{Id}_tab_{tabIndex}";
        _tabTitles.Add(title);
        _tabBar.AddTab(title, tabId);

        // Configure content panel to fill the space below the tab bar
        content.Id = $"{Id}_content_{tabIndex}";

        // Get the tab bar height from constraint or default
        var tabBarHeight = _tabBar.Constraint.Height ?? UITheme.Dark.ButtonHeight;

        content.Constraint = new LayoutConstraint
        {
            Anchor = Anchor.Fill,
            MarginTop = tabBarHeight, // Below tab bar (use margin so height is calculated correctly)
            Padding = 0 // No padding - let content manage its own
        };

        // Only the first tab (index 0) should be visible by default
        content.Visible = (tabIndex == ActiveTabIndex);

        _contentPanels.Add(content);
        AddChild(content);
    }

    /// <summary>
    /// Sets the active tab by index.
    /// </summary>
    public void SetActiveTab(int index)
    {
        if (index < 0 || index >= _contentPanels.Count)
            return;

        // Hide all content panels
        for (int i = 0; i < _contentPanels.Count; i++)
        {
            _contentPanels[i].Visible = (i == index);
        }

        // Update tab bar active state
        _tabBar.SetActiveTab(index);
    }

    /// <summary>
    /// Gets the content panel for the specified tab index.
    /// </summary>
    public Panel? GetContentPanel(int index)
    {
        if (index >= 0 && index < _contentPanels.Count)
            return _contentPanels[index];
        return null;
    }

    /// <summary>
    /// Gets the content panel for the currently active tab.
    /// </summary>
    public Panel? GetActiveContentPanel()
    {
        return GetContentPanel(ActiveTabIndex);
    }

    protected override void OnRenderContainer(UIContext context)
    {
        base.OnRenderContainer(context);
    }

    protected override void OnRenderChildren(UIContext context)
    {
        // Render all visible children (tab bar + active content panel)
        // The base UIContainer will handle rendering in the correct order
        base.OnRenderChildren(context);
    }
}

