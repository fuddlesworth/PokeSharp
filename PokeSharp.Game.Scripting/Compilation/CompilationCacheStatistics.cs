namespace PokeSharp.Game.Scripting.Compilation;

using PokeSharp.Game.Systems.Services;

/// <summary>
///     Statistics about the compilation cache.
/// </summary>
public class CompilationCacheStatistics
{
    public int CachedEntries { get; init; }
    public int TotalSize { get; init; }
}
