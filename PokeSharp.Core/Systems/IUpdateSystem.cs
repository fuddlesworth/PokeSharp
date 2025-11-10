using Arch.Core;

namespace PokeSharp.Core.Systems;

/// <summary>
/// Interface for systems that perform game logic updates.
/// Update systems modify component data and game state.
/// These systems execute during the Update() phase of the game loop.
/// </summary>
public interface IUpdateSystem : ISystem
{
    /// <summary>
    /// Gets the priority for update execution order.
    /// Lower values execute first. Typical range: 0-1000.
    /// </summary>
    int UpdatePriority { get; }

    /// <summary>
    /// Updates the system logic for the current frame.
    /// This method is called during the Update phase of the game loop.
    /// </summary>
    /// <param name="world">The ECS world containing all entities.</param>
    /// <param name="deltaTime">Time elapsed since the last update, in seconds.</param>
    new void Update(World world, float deltaTime);
}
