// Achievement Tracker - Example Script-Based Mod
// Demonstrates event-driven modding using ScriptBase
// Tracks: maps visited, NPCs talked to, steps taken, and unlocks achievements

using MonoBallFramework.Game.Scripting.Runtime;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Events.Map;
using MonoBallFramework.Game.Engine.Core.Events.NPC;
using MonoBallFramework.Game.Engine.Core.Events.Flags;
using MonoBallFramework.Game.GameSystems.Events;
using MonoBallFramework.Game.Engine.Core.Types;

// ============================================================================
// Custom Achievement Events - for cross-mod communication
// ============================================================================

/// <summary>
///     Rarity tiers for achievements.
/// </summary>
public enum AchievementRarity
{
    Common = 0,
    Uncommon = 1,
    Rare = 2,
    Epic = 3,
    Legendary = 4
}

/// <summary>
///     Event published when an achievement is unlocked.
///     Other mods can subscribe to react to achievement unlocks.
/// </summary>
public sealed class AchievementUnlockedEvent : NotificationEventBase
{
    public string AchievementId { get; set; } = string.Empty;
    public string AchievementName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "general";
    public AchievementRarity Rarity { get; set; } = AchievementRarity.Common;
    public int Points { get; set; } = 10;
    public string? SourceMod { get; set; }

    public override void Reset()
    {
        base.Reset();
        AchievementId = string.Empty;
        AchievementName = string.Empty;
        Description = string.Empty;
        Category = "general";
        Rarity = AchievementRarity.Common;
        Points = 10;
        SourceMod = null;
    }
}

// ============================================================================
// Main Achievement Tracker Script
// ============================================================================

/// <summary>
///     Achievement tracker script that listens to game events and unlocks achievements.
///     This is a global script (not attached to any specific entity).
/// </summary>
public class AchievementTracker : ScriptBase
{
    // Achievement flag prefixes for organization
    private const string AchievementPrefix = "achievement/";
    private const string StatsPrefix = "stats/";

    // Step tracking (stored as variable since flags are boolean)
    // Thread-safe: accessed from multiple event handlers
    private int _stepCount = 0;

    // Achievement thresholds
    private const int FirstStepsThreshold = 100;
    private const int ExplorerThreshold = 1000;
    private const int MarathonThreshold = 10000;
    private const int SocialButterflyThreshold = 10;
    private const int WorldTravelerThreshold = 5;

    /// <summary>
    ///     Initialize the achievement tracker and load saved progress.
    /// </summary>
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Load saved step count from game variables
        string? savedSteps = ctx.GameState.GetVariable($"{StatsPrefix}total_steps");
        if (!string.IsNullOrEmpty(savedSteps) && int.TryParse(savedSteps, out int steps))
        {
            _stepCount = steps;
        }

        ctx.Logger.LogInformation("Achievement Tracker initialized. Total steps: {Steps}", _stepCount);
    }

    /// <summary>
    ///     Register event handlers for tracking player progress.
    /// </summary>
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Track map transitions for "World Traveler" achievement
        On<MapTransitionEvent>(OnMapTransition);

        // Track NPC interactions for "Social Butterfly" achievement
        On<NPCInteractionEvent>(OnNPCInteraction);

        // Track movement for step counter achievements
        On<MovementCompletedEvent>(OnMovementCompleted);

        // Listen for flag changes (can react to other mods' achievements)
        On<FlagChangedEvent>(OnFlagChanged);

        ctx.Logger.LogInformation("Achievement Tracker event handlers registered");
    }

    /// <summary>
    ///     Handle map transitions - track unique maps visited.
    /// </summary>
    private void OnMapTransition(MapTransitionEvent evt)
    {
        try
        {
            if (evt.ToMapId == null)
            {
                Context.Logger.LogDebug("OnMapTransition: ToMapId is null, skipping");
                return;
            }

            string mapVisitedFlag = $"{StatsPrefix}visited_map/{evt.ToMapId.Value}";
            var flagId = GameFlagId.Create(mapVisitedFlag);

            Context.Logger.LogDebug("OnMapTransition: Checking flag '{FlagName}' -> GameFlagId: '{FlagId}'",
                mapVisitedFlag, flagId.Value);

            // Check if this is the first time visiting this map
            if (!Context.GameState.GetFlag(flagId))
            {
                Context.GameState.SetFlag(flagId, true);

                Context.Logger.LogInformation(
                    "First visit to map: {MapName} ({MapId}) - Flag set: {Flag}",
                    evt.ToMapName,
                    evt.ToMapId.Value,
                    flagId.Value);

                // Count total unique maps visited
                int mapsVisited = CountMapsVisited();
                Context.Logger.LogInformation("Total unique maps visited: {Count} (threshold: {Threshold})",
                    mapsVisited, WorldTravelerThreshold);

                // Check for World Traveler achievement
                CheckWorldTravelerAchievement(mapsVisited);
            }
            else
            {
                Context.Logger.LogDebug("OnMapTransition: Already visited {MapId}", evt.ToMapId.Value);
            }
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Error in OnMapTransition handler");
        }
    }

    /// <summary>
    ///     Handle NPC interactions - track unique NPCs talked to.
    /// </summary>
    private void OnNPCInteraction(NPCInteractionEvent evt)
    {
        try
        {
            if (evt.NpcId == null) return;

            string npcTalkedFlag = $"{StatsPrefix}talked_to/{evt.NpcId.Value}";

            // Check if this is the first interaction with this NPC
            if (!Context.GameState.GetFlag(GameFlagId.Create(npcTalkedFlag)))
            {
                Context.GameState.SetFlag(GameFlagId.Create(npcTalkedFlag), true);

                Context.Logger.LogInformation("First conversation with NPC: {NpcId}", evt.NpcId.Value);

                // Count total unique NPCs talked to
                int npcsMetCount = CountNPCsMet();
                Context.Logger.LogDebug("Total unique NPCs met: {Count}", npcsMetCount);

                // Check for Social Butterfly achievement
                CheckSocialButterflyAchievement(npcsMetCount);
            }
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Error in OnNPCInteraction handler");
        }
    }

    /// <summary>
    ///     Handle movement completion - track steps taken.
    /// </summary>
    private void OnMovementCompleted(MovementCompletedEvent evt)
    {
        try
        {
            // Only count player steps (check if this is the player entity)
            // For now, count all movement events
            // Thread-safe increment
            int newStepCount = System.Threading.Interlocked.Increment(ref _stepCount);

            // Save every 100 steps to reduce write frequency
            if (newStepCount % 100 == 0)
            {
                Context.GameState.SetVariable($"{StatsPrefix}total_steps", newStepCount.ToString());
                Context.Logger.LogDebug("Steps saved: {Steps}", newStepCount);
            }

            // Check step-based achievements at milestones
            CheckStepAchievements(newStepCount);
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Error in OnMovementCompleted handler");
        }
    }

    /// <summary>
    ///     React to flag changes (useful for cross-mod interactions).
    /// </summary>
    private void OnFlagChanged(FlagChangedEvent evt)
    {
        try
        {
            if (evt.FlagId == null) return;

            // Log when achievements are unlocked (including from other sources)
            if (evt.FlagId.Value.StartsWith(AchievementPrefix) && evt.NewValue)
            {
                string achievementName = evt.FlagId.Value.Substring(AchievementPrefix.Length);
                Context.Logger.LogInformation("Achievement flag changed: {Achievement}", achievementName);
            }
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Error in OnFlagChanged handler");
        }
    }

    /// <summary>
    ///     Check and unlock step-based achievements.
    /// </summary>
    private void CheckStepAchievements(int currentStepCount)
    {
        // First Steps (100 steps) - Common
        if (currentStepCount >= FirstStepsThreshold)
        {
            UnlockAchievement("first_steps", "First Steps", "Take your first 100 steps!",
                "exploration", AchievementRarity.Common, 10);
        }

        // Explorer (1000 steps) - Uncommon
        if (currentStepCount >= ExplorerThreshold)
        {
            UnlockAchievement("explorer", "Explorer", "Walk 1,000 steps across the world!",
                "exploration", AchievementRarity.Uncommon, 25);
        }

        // Marathon Runner (10000 steps) - Rare
        if (currentStepCount >= MarathonThreshold)
        {
            UnlockAchievement("marathon_runner", "Marathon Runner", "Walk an incredible 10,000 steps!",
                "exploration", AchievementRarity.Rare, 50);
        }
    }

    /// <summary>
    ///     Check and unlock Social Butterfly achievement.
    /// </summary>
    private void CheckSocialButterflyAchievement(int npcsMetCount)
    {
        if (npcsMetCount >= SocialButterflyThreshold)
        {
            UnlockAchievement("social_butterfly", "Social Butterfly", $"Talk to {SocialButterflyThreshold} different NPCs!",
                "social", AchievementRarity.Uncommon, 25);
        }
    }

    /// <summary>
    ///     Check and unlock World Traveler achievement.
    /// </summary>
    private void CheckWorldTravelerAchievement(int mapsVisited)
    {
        if (mapsVisited >= WorldTravelerThreshold)
        {
            UnlockAchievement("world_traveler", "World Traveler", $"Visit {WorldTravelerThreshold} different maps!",
                "exploration", AchievementRarity.Uncommon, 25);
        }
    }

    /// <summary>
    ///     Unlock an achievement if not already unlocked.
    /// </summary>
    private void UnlockAchievement(string achievementId, string name, string description,
        string category = "general", AchievementRarity rarity = AchievementRarity.Common, int points = 10)
    {
        string flagId = $"{AchievementPrefix}{achievementId}";
        var flag = GameFlagId.Create(flagId);

        // Check if already unlocked
        if (Context.GameState.GetFlag(flag))
        {
            return; // Already unlocked
        }

        // Unlock the achievement
        Context.GameState.SetFlag(flag, true);

        // Log the achievement
        Context.Logger.LogInformation("ACHIEVEMENT UNLOCKED: {Name} - {Description}", name, description);

        // Publish AchievementUnlockedEvent for other mods to react
        var evt = new AchievementUnlockedEvent
        {
            AchievementId = achievementId,
            AchievementName = name,
            Description = description,
            Category = category,
            Rarity = rarity,
            Points = points,
            SourceMod = "pokesharp.achievements"
        };
        Publish(evt);
        Context.Logger.LogDebug("Published AchievementUnlockedEvent for '{AchievementId}'", achievementId);

        // Show achievement notification to player
        // Note: This uses the dialogue system for now. A dedicated achievement popup would be better.
        Context.Dialogue.ShowMessage($"Achievement Unlocked!\n{name}\n{description}");
    }

    /// <summary>
    ///     Count unique maps visited by checking stats flags.
    /// </summary>
    private int CountMapsVisited()
    {
        // GameFlagId.Create strips slashes/colons, so "stats/visited_map/{mapId}" becomes
        // "base:flag:misc/statsvisited_map{mapIdWithoutSpecialChars}"
        // We need to search for the normalized pattern without slashes
        string searchPattern = "statsvisited_map";
        int count = 0;
        foreach (string flag in Context.GameState.GetActiveFlags())
        {
            if (flag.Contains(searchPattern))
            {
                count++;
            }
        }
        Context.Logger.LogDebug("CountMapsVisited: found {Count} flags matching pattern '{Pattern}'", count, searchPattern);
        return count;
    }

    /// <summary>
    ///     Count unique NPCs met by checking stats flags.
    /// </summary>
    private int CountNPCsMet()
    {
        // GameFlagId.Create strips slashes/colons, so "stats/talked_to/{npcId}" becomes
        // "base:flag:misc/statstalked_to{npcIdWithoutSpecialChars}"
        // We need to search for the normalized pattern without slashes
        string searchPattern = "statstalked_to";
        int count = 0;
        foreach (string flag in Context.GameState.GetActiveFlags())
        {
            if (flag.Contains(searchPattern))
            {
                count++;
            }
        }
        Context.Logger.LogDebug("CountNPCsMet: found {Count} flags matching pattern '{Pattern}'", count, searchPattern);
        return count;
    }

    /// <summary>
    ///     Clean up when the script is unloaded.
    /// </summary>
    public override void OnUnload()
    {
        // Save final step count (thread-safe read)
        int finalStepCount = System.Threading.Volatile.Read(ref _stepCount);
        Context.GameState.SetVariable($"{StatsPrefix}total_steps", finalStepCount.ToString());
        Context.Logger.LogInformation("Achievement Tracker unloading. Final step count: {Steps}", finalStepCount);

        // Base class handles event subscription cleanup automatically
        base.OnUnload();
    }
}

// Return an instance of the script for the runtime to use
new AchievementTracker()
