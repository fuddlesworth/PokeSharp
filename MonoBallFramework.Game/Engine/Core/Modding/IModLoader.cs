namespace MonoBallFramework.Game.Engine.Core.Modding;

/// <summary>
/// Interface for accessing loaded mod information.
/// Used by content systems to resolve mod-provided content paths.
/// </summary>
public interface IModLoader
{
    /// <summary>
    /// Gets a read-only collection of loaded mod manifests, keyed by mod ID.
    /// </summary>
    IReadOnlyDictionary<string, ModManifest> LoadedMods { get; }

    /// <summary>
    /// Discovers and registers mod manifests without loading scripts.
    /// Call this early (before game data loading) to enable content overrides.
    /// </summary>
    Task DiscoverModsAsync();

    /// <summary>
    /// Loads mod scripts and patches for previously discovered mods.
    /// Call this after API providers are set up.
    /// </summary>
    Task LoadModScriptsAsync();
}
