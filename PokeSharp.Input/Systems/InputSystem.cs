using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Core.Components;
using PokeSharp.Core.Systems;
using PokeSharp.Input.Components;
using PokeSharp.Input.Services;

namespace PokeSharp.Input.Systems;

/// <summary>
///     System that processes keyboard and gamepad input and converts it to movement requests.
///     Implements Pokemon-style grid-locked input with queue-based buffering for responsive controls.
///     Movement validation and collision checking happens in MovementSystem.
/// </summary>
public class InputSystem : BaseSystem
{
    private readonly InputBuffer _inputBuffer;
    private Direction _lastBufferedDirection = Direction.None;
    private float _lastBufferTime = -1f;
    private float _totalTime;

    /// <summary>
    ///     Initializes a new instance of the InputSystem class.
    /// </summary>
    /// <param name="maxBufferSize">Maximum number of inputs to buffer (default: 5).</param>
    /// <param name="bufferTimeout">How long inputs remain valid in seconds (default: 0.2s).</param>
    public InputSystem(int maxBufferSize = 5, float bufferTimeout = 0.2f)
    {
        _inputBuffer = new InputBuffer(maxBufferSize, bufferTimeout);
    }

    /// <inheritdoc />
    public override int Priority => SystemPriority.Input;

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        _totalTime += deltaTime;

        var keyboardState = Keyboard.GetState();
        var gamepadState = GamePad.GetState(PlayerIndex.One);

        // Query player entities with input state and direction component
        var query = new QueryDescription().WithAll<
            Player,
            Position,
            GridMovement,
            InputState,
            Direction
        >();

        world.Query(
            in query,
            (
                Entity entity,
                ref Position position,
                ref GridMovement movement,
                ref InputState input
            ) =>
            {
                if (!input.InputEnabled)
                    return;

                // Get current input direction
                var currentDirection = GetInputDirection(keyboardState, gamepadState);

                // Update pressed direction if input detected
                if (currentDirection != Direction.None)
                {
                    input.PressedDirection = currentDirection;

                    // Synchronize Direction component with input direction
                    ref var direction = ref entity.Get<Direction>();
                    direction = currentDirection;

                    // Buffer input if:
                    // 1. Not currently moving (allows holding keys for continuous movement), OR
                    // 2. Direction changed (allows queuing direction changes during movement)
                    // But only if we haven't buffered this exact direction very recently (prevents duplicates)
                    var shouldBuffer =
                        !movement.IsMoving || currentDirection != _lastBufferedDirection;

                    // Also prevent buffering the same direction multiple times per frame
                    var isDifferentTiming =
                        _totalTime != _lastBufferTime || currentDirection != _lastBufferedDirection;

                    if (shouldBuffer && isDifferentTiming)
                        if (_inputBuffer.AddInput(currentDirection, _totalTime))
                        {
                            _lastBufferedDirection = currentDirection;
                            _lastBufferTime = _totalTime;
                        }
                }

                // Check for action button
                input.ActionPressed =
                    keyboardState.IsKeyDown(Keys.Space)
                    || keyboardState.IsKeyDown(Keys.Enter)
                    || keyboardState.IsKeyDown(Keys.Z)
                    || gamepadState.Buttons.A == ButtonState.Pressed;

                // Try to consume buffered input if not currently moving
                if (
                    !movement.IsMoving
                    && _inputBuffer.TryConsumeInput(_totalTime, out var bufferedDirection)
                    && !entity.Has<MovementRequest>()
                )
                {
                    world.Add(entity, new MovementRequest(bufferedDirection));
                    _lastBufferedDirection = Direction.None; // Reset after consuming
                }
            }
        );
    }

    private static Direction GetInputDirection(KeyboardState keyboard, GamePadState gamepad)
    {
        // Keyboard input (priority: most recently pressed)
        if (keyboard.IsKeyDown(Keys.Up) || keyboard.IsKeyDown(Keys.W))
            return Direction.Up;
        if (keyboard.IsKeyDown(Keys.Down) || keyboard.IsKeyDown(Keys.S))
            return Direction.Down;
        if (keyboard.IsKeyDown(Keys.Left) || keyboard.IsKeyDown(Keys.A))
            return Direction.Left;
        if (keyboard.IsKeyDown(Keys.Right) || keyboard.IsKeyDown(Keys.D))
            return Direction.Right;

        // Gamepad input
        var thumbstick = gamepad.ThumbSticks.Left;
        if (thumbstick.Y > 0.5f || gamepad.DPad.Up == ButtonState.Pressed)
            return Direction.Up;
        if (thumbstick.Y < -0.5f || gamepad.DPad.Down == ButtonState.Pressed)
            return Direction.Down;
        if (thumbstick.X < -0.5f || gamepad.DPad.Left == ButtonState.Pressed)
            return Direction.Left;
        if (thumbstick.X > 0.5f || gamepad.DPad.Right == ButtonState.Pressed)
            return Direction.Right;

        return Direction.None;
    }

}
