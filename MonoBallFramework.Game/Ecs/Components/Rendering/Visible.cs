namespace MonoBallFramework.Game.Ecs.Components.Rendering;

/// <summary>
///     Flag component indicating an entity should be rendered.
///     Entities without this component will not be rendered by the ElevationRenderSystem.
///     This allows behaviors (like HiddenBehavior) to toggle visibility by adding/removing this component.
/// </summary>
/// <remarks>
///     This is a "marker" or "tag" component with no data - its presence/absence controls rendering.
///     Default behavior: entities with Sprite component should also have Visible component.
/// </remarks>
public struct Visible;
