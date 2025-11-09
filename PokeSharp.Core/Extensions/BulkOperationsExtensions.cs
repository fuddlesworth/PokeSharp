using Arch.Core;
using Arch.Core.Extensions;

namespace PokeSharp.Core.Extensions;

/// <summary>
///     Extension methods for convenient bulk operations on World and Entity arrays.
///     Provides syntactic sugar for common batch operations.
/// </summary>
/// <example>
///     <code>
/// // Create batch of entities
/// var entities = world.CreateBatch(100);
///
/// // Add component to all
/// entities.AddToAll(new Health { MaxHP = 100, CurrentHP = 100 });
///
/// // Destroy batch
/// world.DestroyBatch(entities);
/// </code>
/// </example>
public static class BulkOperationsExtensions
{
    /// <summary>
    ///     Create multiple empty entities at once.
    ///     All entities created will have no components initially.
    /// </summary>
    /// <param name="world">World to create entities in</param>
    /// <param name="count">Number of entities to create</param>
    /// <returns>Array of created entities</returns>
    /// <example>
    ///     <code>
    /// var entities = world.CreateBatch(50);
    /// foreach (var entity in entities)
    /// {
    ///     entity.Add(new Position(0, 0));
    /// }
    /// </code>
    /// </example>
    public static Entity[] CreateBatch(this World world, int count)
    {
        ArgumentNullException.ThrowIfNull(world, nameof(world));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count, nameof(count));

        var entities = new Entity[count];
        for (int i = 0; i < count; i++)
        {
            entities[i] = world.Create();
        }

        return entities;
    }

    /// <summary>
    ///     Create multiple entities with a single component type.
    /// </summary>
    /// <typeparam name="T">Component type</typeparam>
    /// <param name="world">World to create entities in</param>
    /// <param name="count">Number of entities to create</param>
    /// <returns>Array of created entities</returns>
    /// <example>
    ///     <code>
    /// // Create 100 entities with Position component (default initialized)
    /// var entities = world.CreateBatch&lt;Position&gt;(100);
    /// </code>
    /// </example>
    public static Entity[] CreateBatch<T>(this World world, int count)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(world, nameof(world));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count, nameof(count));

        var entities = new Entity[count];
        for (int i = 0; i < count; i++)
        {
            entities[i] = world.Create<T>();
        }

        return entities;
    }

    /// <summary>
    ///     Create multiple entities with two component types.
    /// </summary>
    /// <typeparam name="T1">First component type</typeparam>
    /// <typeparam name="T2">Second component type</typeparam>
    /// <param name="world">World to create entities in</param>
    /// <param name="count">Number of entities to create</param>
    /// <returns>Array of created entities</returns>
    /// <example>
    ///     <code>
    /// // Create 50 entities with Position and Velocity
    /// var entities = world.CreateBatch&lt;Position, Velocity&gt;(50);
    /// </code>
    /// </example>
    public static Entity[] CreateBatch<T1, T2>(this World world, int count)
        where T1 : struct
        where T2 : struct
    {
        ArgumentNullException.ThrowIfNull(world, nameof(world));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count, nameof(count));

        var entities = new Entity[count];
        for (int i = 0; i < count; i++)
        {
            entities[i] = world.Create<T1, T2>();
        }

        return entities;
    }

    /// <summary>
    ///     Create multiple entities with three component types.
    /// </summary>
    /// <typeparam name="T1">First component type</typeparam>
    /// <typeparam name="T2">Second component type</typeparam>
    /// <typeparam name="T3">Third component type</typeparam>
    /// <param name="world">World to create entities in</param>
    /// <param name="count">Number of entities to create</param>
    /// <returns>Array of created entities</returns>
    /// <example>
    ///     <code>
    /// // Create 20 entities with Position, Velocity, and Sprite
    /// var entities = world.CreateBatch&lt;Position, Velocity, Sprite&gt;(20);
    /// </code>
    /// </example>
    public static Entity[] CreateBatch<T1, T2, T3>(this World world, int count)
        where T1 : struct
        where T2 : struct
        where T3 : struct
    {
        ArgumentNullException.ThrowIfNull(world, nameof(world));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count, nameof(count));

        var entities = new Entity[count];
        for (int i = 0; i < count; i++)
        {
            entities[i] = world.Create<T1, T2, T3>();
        }

        return entities;
    }

    /// <summary>
    ///     Destroy multiple entities in one operation.
    ///     More efficient than destroying individually.
    /// </summary>
    /// <param name="world">World containing the entities</param>
    /// <param name="entities">Entities to destroy</param>
    /// <example>
    ///     <code>
    /// world.DestroyBatch(enemyArray);
    /// </code>
    /// </example>
    public static void DestroyBatch(this World world, params Entity[] entities)
    {
        ArgumentNullException.ThrowIfNull(world, nameof(world));
        ArgumentNullException.ThrowIfNull(entities, nameof(entities));

        foreach (var entity in entities)
        {
            if (world.IsAlive(entity))
            {
                world.Destroy(entity);
            }
        }
    }

    /// <summary>
    ///     Destroy multiple entities from a collection.
    /// </summary>
    /// <param name="world">World containing the entities</param>
    /// <param name="entities">Collection of entities to destroy</param>
    /// <example>
    ///     <code>
    /// var deadEntities = new List&lt;Entity&gt;();
    /// // ... collect dead entities ...
    /// world.DestroyBatch(deadEntities);
    /// </code>
    /// </example>
    public static void DestroyBatch(this World world, IEnumerable<Entity> entities)
    {
        ArgumentNullException.ThrowIfNull(world, nameof(world));
        ArgumentNullException.ThrowIfNull(entities, nameof(entities));

        foreach (var entity in entities)
        {
            if (world.IsAlive(entity))
            {
                world.Destroy(entity);
            }
        }
    }

    /// <summary>
    ///     Add the same component to all entities in the array.
    /// </summary>
    /// <typeparam name="T">Component type</typeparam>
    /// <param name="entities">Entities to modify</param>
    /// <param name="component">Component to add to all entities</param>
    /// <example>
    ///     <code>
    /// // Apply poison status to all affected entities
    /// affectedEntities.AddToAll(new PoisonStatus { Duration = 5.0f });
    /// </code>
    /// </example>
    public static void AddToAll<T>(this Entity[] entities, T component)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(entities, nameof(entities));

        foreach (var entity in entities)
        {
            entity.Add<T>(component);
        }
    }

    /// <summary>
    ///     Add component to all entities with different values per entity.
    /// </summary>
    /// <typeparam name="T">Component type</typeparam>
    /// <param name="entities">Entities to modify</param>
    /// <param name="componentFactory">Factory to create component per entity</param>
    /// <example>
    ///     <code>
    /// // Give each entity a unique ID
    /// entities.AddToAll&lt;EntityId&gt;((entity, i) => new EntityId { Value = i });
    /// </code>
    /// </example>
    public static void AddToAll<T>(this Entity[] entities, Func<Entity, int, T> componentFactory)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(entities, nameof(entities));
        ArgumentNullException.ThrowIfNull(componentFactory, nameof(componentFactory));

        for (int i = 0; i < entities.Length; i++)
        {
            entities[i].Add<T>(componentFactory(entities[i], i));
        }
    }

    /// <summary>
    ///     Remove component from all entities in the array.
    /// </summary>
    /// <typeparam name="T">Component type to remove</typeparam>
    /// <param name="entities">Entities to modify</param>
    /// <example>
    ///     <code>
    /// // Remove stunned status from all entities
    /// stunned Entities.RemoveFromAll&lt;StunnedStatus&gt;();
    /// </code>
    /// </example>
    public static void RemoveFromAll<T>(this Entity[] entities)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(entities, nameof(entities));

        foreach (var entity in entities)
        {
            if (entity.Has<T>())
            {
                entity.Remove<T>();
            }
        }
    }

    /// <summary>
    ///     Set component value for all entities (replaces existing).
    /// </summary>
    /// <typeparam name="T">Component type</typeparam>
    /// <param name="entities">Entities to modify</param>
    /// <param name="component">Component value to set</param>
    /// <example>
    ///     <code>
    /// // Reset all health to full
    /// playerParty.SetOnAll(new Health { MaxHP = 100, CurrentHP = 100 });
    /// </code>
    /// </example>
    public static void SetOnAll<T>(this Entity[] entities, T component)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(entities, nameof(entities));

        foreach (var entity in entities)
        {
            if (entity.Has<T>())
            {
                entity.Set<T>(component);
            }
        }
    }

    /// <summary>
    ///     Apply an action to all entities in the array.
    /// </summary>
    /// <param name="entities">Entities to process</param>
    /// <param name="action">Action to apply to each entity</param>
    /// <example>
    ///     <code>
    /// enemies.ForEachEntity(entity =>
    /// {
    ///     if (entity.Get&lt;Health&gt;().CurrentHP &lt;= 0)
    ///         entity.Destroy();
    /// });
    /// </code>
    /// </example>
    public static void ForEachEntity(this Entity[] entities, Action<Entity> action)
    {
        ArgumentNullException.ThrowIfNull(entities, nameof(entities));
        ArgumentNullException.ThrowIfNull(action, nameof(action));

        foreach (var entity in entities)
        {
            action(entity);
        }
    }

    /// <summary>
    ///     Apply an action to all entities with their index.
    /// </summary>
    /// <param name="entities">Entities to process</param>
    /// <param name="action">Action with entity and index</param>
    /// <example>
    ///     <code>
    /// entities.ForEachEntity((entity, i) =>
    /// {
    ///     entity.Add(new IndexTag { Value = i });
    /// });
    /// </code>
    /// </example>
    public static void ForEachEntity(this Entity[] entities, Action<Entity, int> action)
    {
        ArgumentNullException.ThrowIfNull(entities, nameof(entities));
        ArgumentNullException.ThrowIfNull(action, nameof(action));

        for (int i = 0; i < entities.Length; i++)
        {
            action(entities[i], i);
        }
    }

    /// <summary>
    ///     Filter entities that have a specific component.
    /// </summary>
    /// <typeparam name="T">Component type to check</typeparam>
    /// <param name="entities">Entities to filter</param>
    /// <returns>Array of entities that have the component</returns>
    /// <example>
    ///     <code>
    /// var damagedEnemies = enemies.WhereHas&lt;DamagedTag&gt;();
    /// </code>
    /// </example>
    public static Entity[] WhereHas<T>(this Entity[] entities)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(entities, nameof(entities));
        return entities.Where(e => e.Has<T>()).ToArray();
    }

    /// <summary>
    ///     Check if all entities have a specific component.
    /// </summary>
    /// <typeparam name="T">Component type to check</typeparam>
    /// <param name="entities">Entities to check</param>
    /// <returns>True if all entities have the component</returns>
    public static bool AllHave<T>(this Entity[] entities)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(entities, nameof(entities));
        return entities.All(e => e.Has<T>());
    }

    /// <summary>
    ///     Check if any entity has a specific component.
    /// </summary>
    /// <typeparam name="T">Component type to check</typeparam>
    /// <param name="entities">Entities to check</param>
    /// <returns>True if any entity has the component</returns>
    public static bool AnyHave<T>(this Entity[] entities)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(entities, nameof(entities));
        return entities.Any(e => e.Has<T>());
    }
}
