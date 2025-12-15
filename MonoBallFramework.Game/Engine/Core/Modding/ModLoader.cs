using System.Text.Json;
using Arch.Core;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Scripting.Api;
using MonoBallFramework.Game.Scripting.Runtime;
using MonoBallFramework.Game.Scripting.Services;

namespace MonoBallFramework.Game.Engine.Core.Modding;

/// <summary>
///     Unified mod loader that handles all mod types: scripts, patches, content overrides, and code mods.
///     Discovers, loads, and manages mods from the /Mods/ directory.
/// </summary>
public sealed class ModLoader : IModLoader
{
    private const string ModManifestFileName = "mod.json";
    private const string ModsDirectoryName = "Mods";

    private readonly IScriptingApiProvider _apis;
    private readonly ModDependencyResolver _dependencyResolver;
    private readonly IEventBus _eventBus;
    private readonly Dictionary<string, ModManifest> _loadedMods = new();
    private readonly ILogger<ModLoader> _logger;
    private readonly string _modsBasePath;
    private readonly Dictionary<string, List<object>> _modScriptInstances = new();
    private readonly Dictionary<string, List<ModPatch>> _modPatches = new();
    private readonly PatchApplicator _patchApplicator;
    private readonly PatchFileLoader _patchFileLoader;
    private readonly ScriptService _scriptService;
    private readonly World _world;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ModLoader" /> class.
    /// </summary>
    public ModLoader(
        ScriptService scriptService,
        ILogger<ModLoader> logger,
        World world,
        IEventBus eventBus,
        IScriptingApiProvider apis,
        PatchApplicator patchApplicator,
        PatchFileLoader patchFileLoader,
        string gameBasePath
    )
    {
        _scriptService = scriptService ?? throw new ArgumentNullException(nameof(scriptService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _apis = apis ?? throw new ArgumentNullException(nameof(apis));
        _patchApplicator = patchApplicator ?? throw new ArgumentNullException(nameof(patchApplicator));
        _patchFileLoader = patchFileLoader ?? throw new ArgumentNullException(nameof(patchFileLoader));

        _modsBasePath = Path.Combine(gameBasePath, ModsDirectoryName);
        _dependencyResolver = new ModDependencyResolver();
    }

    /// <summary>
    ///     Gets a read-only collection of loaded mod manifests.
    /// </summary>
    public IReadOnlyDictionary<string, ModManifest> LoadedMods => _loadedMods;

    /// <summary>
    ///     Gets all patches for all loaded mods.
    /// </summary>
    public IReadOnlyDictionary<string, List<ModPatch>> AllPatches => _modPatches;

    /// <summary>
    ///     Discovers and loads all mods from the /Mods/ directory.
    ///     Must be called AFTER core scripts are loaded.
    /// </summary>
    public async Task LoadModsAsync()
    {
        // Two-phase loading for backward compatibility
        await DiscoverModsAsync();
        await LoadModScriptsAsync();
    }

    /// <summary>
    ///     Phase 1: Discovers mod manifests and registers content folders.
    ///     Call this BEFORE game data loading to enable content overrides.
    ///     Does not load scripts or patches.
    /// </summary>
    public async Task DiscoverModsAsync()
    {
        _logger.LogInformation("🔍 Discovering mods in: {Path}", _modsBasePath);

        if (!Directory.Exists(_modsBasePath))
        {
            _logger.LogWarning(
                "⚠️  Mods directory not found: {Path}. Creating it...",
                _modsBasePath
            );
            Directory.CreateDirectory(_modsBasePath);
            return;
        }

        try
        {
            // Discover all mod manifests
            List<ModManifest> manifests = DiscoverMods();

            if (manifests.Count == 0)
            {
                _logger.LogInformation("ℹ️  No mods found in {Path}", _modsBasePath);
                return;
            }

            _logger.LogInformation("📦 Found {Count} mod(s)", manifests.Count);

            // Resolve dependencies and determine load order
            List<ModManifest> orderedManifests;
            try
            {
                orderedManifests = _dependencyResolver.ResolveDependencies(manifests);
            }
            catch (ModDependencyException ex)
            {
                _logger.LogError(ex, "❌ Failed to resolve mod dependencies: {Message}", ex.Message);
                throw;
            }

            // Register manifests (content folders become available to ContentProvider)
            foreach (ModManifest manifest in orderedManifests)
            {
                if (_loadedMods.ContainsKey(manifest.Id))
                {
                    _logger.LogWarning("⚠️  Mod '{Id}' already registered. Skipping.", manifest.Id);
                    continue;
                }

                _loadedMods[manifest.Id] = manifest;
                _logger.LogInformation(
                    "📁 Registered mod: {Name} v{Version} (priority {Priority}, {ContentCount} content folders)",
                    manifest.Name, manifest.Version, manifest.Priority, manifest.ContentFolders.Count);
            }

            _logger.LogInformation("✅ Discovered and registered {Count} mod(s)", _loadedMods.Count);
        }
        catch (ModDependencyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Unexpected error during mod discovery");
            throw;
        }

        await Task.CompletedTask; // Async for interface compatibility
    }

    /// <summary>
    ///     Phase 2: Loads scripts and patches for previously discovered mods.
    ///     Call this AFTER API providers are set up.
    /// </summary>
    public async Task LoadModScriptsAsync()
    {
        _logger.LogInformation("⚙️  Loading mod scripts and patches for {Count} mod(s)", _loadedMods.Count);

        foreach (var manifest in _loadedMods.Values.OrderByDescending(m => m.Priority))
        {
            await LoadModScriptsAndPatchesAsync(manifest);
        }

        _logger.LogInformation("✅ Loaded scripts and patches for {Count} mod(s)", _loadedMods.Count);
    }

    /// <summary>
    ///     Loads scripts and patches for a single mod (Phase 2).
    /// </summary>
    private async Task LoadModScriptsAndPatchesAsync(ModManifest manifest)
    {
        var scriptInstances = new List<object>();
        var patches = new List<ModPatch>();

        try
        {
            var loadedMod = new LoadedMod { Manifest = manifest, RootPath = manifest.DirectoryPath };

            // Load patches
            if (manifest.Patches.Count > 0)
            {
                patches = _patchFileLoader.LoadModPatches(loadedMod);
                _modPatches[manifest.Id] = patches;
                _logger.LogInformation("📝 Loaded {Count} patch(es) for mod '{Id}'", patches.Count, manifest.Id);
            }

            // Load scripts
            if (manifest.Scripts.Count > 0)
            {
                foreach (string scriptFile in manifest.Scripts)
                {
                    string scriptPath = Path.Combine(manifest.DirectoryPath, scriptFile);

                    if (!File.Exists(scriptPath))
                    {
                        _logger.LogError("❌ Script file not found for mod '{Id}': {Path}", manifest.Id, scriptPath);
                        continue;
                    }

                    // Pass full absolute path - ScriptService handles both absolute and relative paths
                    object? instance = await _scriptService.LoadScriptAsync(scriptPath);

                    if (instance == null)
                    {
                        _logger.LogError("❌ Failed to load script '{Script}' for mod '{Id}'", scriptFile, manifest.Id);
                        continue;
                    }

                    if (instance is ScriptBase scriptBase)
                    {
                        _scriptService.InitializeScript(scriptBase, _world, null, _logger);
                        _logger.LogDebug("✅ Loaded and initialized script: {Script} ({Type})", scriptFile, instance.GetType().Name);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️  Script '{Script}' is not a ScriptBase. Loaded but not initialized.", scriptFile);
                    }

                    scriptInstances.Add(instance);
                }
            }

            if (scriptInstances.Count > 0)
            {
                _modScriptInstances[manifest.Id] = scriptInstances;
            }

            _logger.LogDebug("Mod '{Id}' scripts loaded: {Scripts} script(s), {Patches} patch(es)",
                manifest.Id, scriptInstances.Count, patches.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to load scripts for mod '{Id}'", manifest.Id);
        }
    }

    /// <summary>
    ///     Discovers all mod manifests by scanning subdirectories for mod.json files.
    /// </summary>
    private List<ModManifest> DiscoverMods()
    {
        var manifests = new List<ModManifest>();

        foreach (string modDirectory in Directory.GetDirectories(_modsBasePath))
        {
            string manifestPath = Path.Combine(modDirectory, ModManifestFileName);

            if (!File.Exists(manifestPath))
            {
                _logger.LogDebug(
                    "Skipping {Path} (no {FileName})",
                    modDirectory,
                    ModManifestFileName
                );
                continue;
            }

            try
            {
                ModManifest? manifest = ParseManifest(manifestPath, modDirectory);
                if (manifest != null)
                {
                    manifests.Add(manifest);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "❌ Failed to parse manifest at {Path}: {Message}",
                    manifestPath,
                    ex.Message
                );
                // Continue with other mods
            }
        }

        return manifests;
    }

    /// <summary>
    ///     Parses a mod.json manifest file.
    /// </summary>
    private ModManifest? ParseManifest(string manifestPath, string modDirectory)
    {
        string json = File.ReadAllText(manifestPath);

        ModManifest? manifest = JsonSerializer.Deserialize<ModManifest>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        if (manifest == null)
        {
            _logger.LogError("❌ Failed to deserialize manifest: {Path}", manifestPath);
            return null;
        }

        manifest.DirectoryPath = modDirectory;

        try
        {
            manifest.Validate();
        }
        catch (Exception ex)
        {
            _logger.LogError("❌ Invalid manifest at {Path}: {Error}", manifestPath, ex.Message);
            return null;
        }

        // Validate content folder keys against known content types
        ValidateContentFolderKeys(manifest, manifestPath);

        _logger.LogDebug("✅ Parsed manifest: {Mod}", manifest);
        return manifest;
    }

    /// <summary>
    ///     Validates that content folder keys match known content types.
    ///     Warns about unknown keys that won't be recognized by ContentProvider.
    /// </summary>
    private void ValidateContentFolderKeys(ModManifest manifest, string manifestPath)
    {
        // Known content type keys from ContentProviderOptions
        var validContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Root", "Definitions", "Graphics", "Audio", "Scripts", "Fonts", "Tiled", "Tilesets",
            "TileBehaviors", "Behaviors", "Sprites", "MapDefinitions", "AudioDefinitions",
            "PopupBackgrounds", "PopupOutlines", "PopupThemes", "MapSections"
        };

        foreach (string key in manifest.ContentFolders.Keys)
        {
            if (!validContentTypes.Contains(key))
            {
                _logger.LogWarning(
                    "⚠️  Unknown content folder key '{Key}' in mod '{Id}' ({Path}). " +
                    "Valid keys: {ValidKeys}. This content type will be ignored.",
                    key, manifest.Id, manifestPath,
                    string.Join(", ", validContentTypes.OrderBy(k => k)));
            }
        }
    }

    /// <summary>
    ///     Loads a single mod: patches, content folders, scripts, and code mods.
    /// </summary>
    private async Task LoadModAsync(ModManifest manifest)
    {
        _logger.LogInformation("⚙️  Loading mod: {Mod}", manifest);

        // Check for conflicts
        if (_loadedMods.ContainsKey(manifest.Id))
        {
            _logger.LogWarning(
                "⚠️  Mod '{Id}' is already loaded. Skipping duplicate.",
                manifest.Id
            );
            return;
        }

        var scriptInstances = new List<object>();
        var patches = new List<ModPatch>();

        try
        {
            var loadedMod = new LoadedMod { Manifest = manifest, RootPath = manifest.DirectoryPath };

            // Step 1: Load patches (data modification)
            if (manifest.Patches.Count > 0)
            {
                patches = _patchFileLoader.LoadModPatches(loadedMod);
                _modPatches[manifest.Id] = patches;

                _logger.LogInformation(
                    "📝 Loaded {Count} patch(es) for mod '{Id}'",
                    patches.Count,
                    manifest.Id
                );
            }

            // Step 2: Content folders are handled by the data loader during game data loading
            // They're registered via the manifest and resolved when loading content
            if (manifest.ContentFolders.Count > 0)
            {
                _logger.LogInformation(
                    "📁 Registered {Count} content folder(s) for mod '{Id}'",
                    manifest.ContentFolders.Count,
                    manifest.Id
                );
            }

            // Step 3: Load scripts (behavior/logic)
            if (manifest.Scripts.Count > 0)
            {
                foreach (string scriptFile in manifest.Scripts)
                {
                    string scriptPath = Path.Combine(manifest.DirectoryPath, scriptFile);

                    if (!File.Exists(scriptPath))
                    {
                        _logger.LogError(
                            "❌ Script file not found for mod '{Id}': {Path}",
                            manifest.Id,
                            scriptPath
                        );
                        continue;
                    }

                    // Pass full absolute path - ScriptService handles both absolute and relative paths
                    object? instance = await _scriptService.LoadScriptAsync(scriptPath);

                    if (instance == null)
                    {
                        _logger.LogError(
                            "❌ Failed to load script '{Script}' for mod '{Id}'",
                            scriptFile,
                            manifest.Id
                        );
                        continue;
                    }

                    // Initialize the script
                    if (instance is ScriptBase scriptBase)
                    {
                        _scriptService.InitializeScript(scriptBase, _world, null, _logger);

                        _logger.LogDebug(
                            "✅ Loaded and initialized script: {Script} ({Type})",
                            scriptFile,
                            instance.GetType().Name
                        );
                    }
                    else
                    {
                        _logger.LogWarning(
                            "⚠️  Script '{Script}' is not a ScriptBase. Loaded but not initialized.",
                            scriptFile
                        );
                    }

                    scriptInstances.Add(instance);
                }
            }

            // Step 4: Load code mods (compiled DLLs) - if supported
            // This would load .dll files from the mod directory
            // For now, this is a placeholder for future compiled mod support

            // Mark mod as loaded
            _loadedMods[manifest.Id] = manifest;
            if (scriptInstances.Count > 0)
            {
                _modScriptInstances[manifest.Id] = scriptInstances;
            }

            _logger.LogWorkflowStatus(
                "Mod loaded successfully",
                ("id", manifest.Id),
                ("name", manifest.Name),
                ("scripts", scriptInstances.Count.ToString()),
                ("patches", patches.Count.ToString()),
                ("contentFolders", manifest.ContentFolders.Count.ToString())
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to load mod '{Id}': {Message}", manifest.Id, ex.Message);

            // Cleanup partially loaded mod
            await UnloadModAsync(manifest.Id);
            throw;
        }
    }

    /// <summary>
    ///     Unloads a mod and disposes its script instances.
    /// </summary>
    public async Task UnloadModAsync(string modId)
    {
        if (!_loadedMods.ContainsKey(modId))
        {
            _logger.LogWarning("⚠️  Mod '{Id}' is not loaded", modId);
            return;
        }

        _logger.LogInformation("🔄 Unloading mod: {Id}", modId);

        // Dispose all script instances
        if (_modScriptInstances.TryGetValue(modId, out List<object>? instances))
        {
            foreach (object instance in instances)
            {
                try
                {
                    // Call OnUnload for ScriptBase instances
                    if (instance is ScriptBase scriptBase)
                    {
                        scriptBase.OnUnload();
                    }

                    // Dispose if applicable
                    if (instance is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync();
                    }
                    else if (instance is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "⚠️  Error disposing script instance for mod '{Id}'",
                        modId
                    );
                }
            }

            _modScriptInstances.Remove(modId);
        }

        _modPatches.Remove(modId);
        _loadedMods.Remove(modId);
        _logger.LogInformation("✅ Mod '{Id}' unloaded", modId);
    }

    /// <summary>
    ///     Reloads a mod (unload then load).
    /// </summary>
    public async Task ReloadModAsync(string modId)
    {
        if (!_loadedMods.TryGetValue(modId, out ModManifest? manifest))
        {
            _logger.LogWarning("⚠️  Cannot reload mod '{Id}': not loaded", modId);
            return;
        }

        _logger.LogInformation("🔄 Reloading mod: {Id}", modId);

        await UnloadModAsync(modId);
        await LoadModAsync(manifest);
    }

    /// <summary>
    ///     Gets the manifest for a loaded mod.
    /// </summary>
    public ModManifest? GetModManifest(string modId)
    {
        return _loadedMods.TryGetValue(modId, out ModManifest? manifest) ? manifest : null;
    }

    /// <summary>
    ///     Checks if a mod is currently loaded.
    /// </summary>
    public bool IsModLoaded(string modId)
    {
        return _loadedMods.ContainsKey(modId);
    }

    /// <summary>
    ///     Gets all patches for a loaded mod.
    /// </summary>
    public List<ModPatch> GetModPatches(string modId)
    {
        return _modPatches.TryGetValue(modId, out List<ModPatch>? patches) ? patches : new List<ModPatch>();
    }

    /// <summary>
    ///     Gets the content folder path for a specific content type in a mod.
    /// </summary>
    public string? GetContentFolderPath(string modId, string contentType)
    {
        if (!_loadedMods.TryGetValue(modId, out ModManifest? manifest))
        {
            return null;
        }

        if (!manifest.ContentFolders.TryGetValue(contentType, out string? relativePath))
        {
            return null;
        }

        return Path.Combine(manifest.DirectoryPath, relativePath);
    }

    /// <summary>
    ///     Gets all content folder paths for a mod.
    /// </summary>
    public Dictionary<string, string> GetContentFolders(string modId)
    {
        if (!_loadedMods.TryGetValue(modId, out ModManifest? manifest))
        {
            return new Dictionary<string, string>();
        }

        var result = new Dictionary<string, string>();
        foreach (var kvp in manifest.ContentFolders)
        {
            result[kvp.Key] = Path.Combine(manifest.DirectoryPath, kvp.Value);
        }

        return result;
    }

    /// <summary>
    /// Loads the base game content as a special mod.
    /// Should be called before LoadModsAsync().
    /// </summary>
    public async Task LoadBaseGameAsync(string baseGameRoot)
    {
        string manifestPath = Path.Combine(baseGameRoot, "mod.json");

        if (!File.Exists(manifestPath))
        {
            _logger.LogWarning(
                "Base game mod.json not found at {Path}. Creating default manifest.",
                manifestPath);
            await CreateDefaultBaseManifestAsync(manifestPath, baseGameRoot);
        }

        ModManifest? manifest = ParseManifest(manifestPath, baseGameRoot);
        if (manifest != null)
        {
            // Base game should have high priority but allow mods to override
            _loadedMods[manifest.Id] = manifest;
            _logger.LogInformation(
                "Loaded base game: {Name} v{Version} (priority {Priority})",
                manifest.Name, manifest.Version, manifest.Priority);
        }
    }

    private async Task CreateDefaultBaseManifestAsync(string path, string baseGameRoot)
    {
        var manifest = new
        {
            id = "base:pokesharp-core",
            name = "PokeSharp Core Content",
            author = "PokeSharp Team",
            version = "1.0.0",
            description = "Base game content",
            priority = 1000,
            contentFolders = new Dictionary<string, string>
            {
                // Broad content categories
                ["Root"] = "",
                ["Definitions"] = "Definitions",
                ["Graphics"] = "Graphics",
                ["Audio"] = "Audio",
                ["Scripts"] = "Scripts",
                ["Fonts"] = "Fonts",
                ["Tiled"] = "Tiled",
                ["Tilesets"] = "Tilesets",
                // Fine-grained definition types for mod overrides
                ["TileBehaviors"] = "Definitions/TileBehaviors",
                ["Behaviors"] = "Definitions/Behaviors",
                ["Sprites"] = "Definitions/Sprites",
                ["MapDefinitions"] = "Definitions/Maps/Regions",
                ["AudioDefinitions"] = "Definitions/Audio",
                ["PopupBackgrounds"] = "Definitions/Maps/Popups/Backgrounds",
                ["PopupOutlines"] = "Definitions/Maps/Popups/Outlines",
                ["PopupThemes"] = "Definitions/Maps/Popups/Themes",
                ["MapSections"] = "Definitions/Maps/Sections"
            },
            scripts = Array.Empty<string>(),
            patches = Array.Empty<string>(),
            dependencies = Array.Empty<string>()
        };

        string json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
        _logger.LogInformation("Created default base game manifest at {Path}", path);
    }
}

/// <summary>
///     Represents a loaded mod with its manifest and file system location.
/// </summary>
public sealed class LoadedMod
{
    public required ModManifest Manifest { get; init; }
    public required string RootPath { get; init; }

    /// <summary>
    ///     Resolves a relative path within this mod's directory.
    /// </summary>
    public string ResolvePath(string relativePath)
    {
        return Path.Combine(RootPath, relativePath);
    }

    public override string ToString()
    {
        return Manifest.ToString();
    }
}
