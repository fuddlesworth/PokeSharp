# Debug Console - Roslyn API Guide

This guide shows you how to use your game's Roslyn scripting API directly from the debug console.

## Table of Contents
- [Overview](#overview)
- [Player API](#player-api)
- [GameState API](#gamestate-api)
- [Dialogue API](#dialogue-api)
- [Map API](#map-api)
- [NPC API](#npc-api)
- [Effects API](#effects-api)
- [Combining APIs](#combining-apis)

## Overview

The console provides access to your game's scripting API through the **`Api`** global object. This is the same API used by your `.csx` script files, so anything you can do in scripts, you can do in the console!

### Available API Services

```csharp
Api.Player      // Player-related operations
Api.GameState   // Flags, variables, and game state
Api.Dialogue    // Show messages and dialogue
Api.Map         // Map queries and transitions
Api.Npc         // NPC management
Api.Effects     // Visual effects
```

---

## Player API

### Get Player Information

```csharp
// Get player name
var name = Api.Player.GetPlayerName();
Print($"Player name: {name}");

// Get player position
var pos = Api.Player.GetPlayerPosition();
Print($"Player at: ({pos.X}, {pos.Y})");

// Get facing direction
var dir = Api.Player.GetPlayerFacing();
Print($"Facing: {dir}");
```

### Money Management

```csharp
// Get current money
var money = Api.Player.GetMoney();
Print($"Money: ${money}");

// Give money
Api.Player.GiveMoney(1000);
Print($"New balance: ${Api.Player.GetMoney()}");

// Take money
bool success = Api.Player.TakeMoney(500);
Print(success ? "Payment successful" : "Not enough money");

// Check if player has enough money
bool canAfford = Api.Player.HasMoney(100);
Print($"Can afford $100? {canAfford}");
```

### Movement Control

```csharp
// Lock player movement (e.g., during cutscenes)
Api.Player.SetPlayerMovementLocked(true);
Print("Player movement locked");

// Unlock movement
Api.Player.SetPlayerMovementLocked(false);
Print("Player movement unlocked");

// Check lock status
bool isLocked = Api.Player.IsPlayerMovementLocked();
Print($"Movement locked: {isLocked}");

// Change facing direction
Api.Player.SetPlayerFacing(Direction.Down);
Print("Player now facing down");
```

### Example: Teleport Player

```csharp
// Get player entity and teleport
var player = GetPlayer();
if (player.HasValue)
{
    ref var pos = ref player.Value.Get<PokeSharp.Game.Components.Movement.Position>();
    pos.X = 10;
    pos.Y = 10;
    Print($"Player teleported to (10, 10)");
}
```

---

## GameState API

### Flags (Boolean State)

Flags track game events like "defeated_gym_leader" or "got_item".

```csharp
// Set a flag
Api.GameState.SetFlag("defeated_brock", true);
Print("Flag set: defeated_brock");

// Get a flag
bool defeated = Api.GameState.GetFlag("defeated_brock");
Print($"Defeated Brock: {defeated}");

// Check if flag exists
bool exists = Api.GameState.FlagExists("defeated_brock");
Print($"Flag exists: {exists}");

// List all active flags
var activeFlags = Api.GameState.GetActiveFlags();
foreach (var flag in activeFlags)
{
    Print($"  - {flag}");
}
```

### Variables (String State)

Variables store text data like "rival_name" or "starter_pokemon".

```csharp
// Set a variable
Api.GameState.SetVariable("rival_name", "BLUE");
Print("Set rival name to BLUE");

// Get a variable
var rivalName = Api.GameState.GetVariable("rival_name");
Print($"Rival name: {rivalName ?? "not set"}");

// Check if variable exists
bool hasRival = Api.GameState.VariableExists("rival_name");
Print($"Has rival: {hasRival}");

// Delete a variable
Api.GameState.DeleteVariable("rival_name");
Print("Deleted rival_name variable");

// List all variables
var varKeys = Api.GameState.GetVariableKeys();
foreach (var key in varKeys)
{
    var value = Api.GameState.GetVariable(key);
    Print($"  {key} = {value}");
}
```

### Random Numbers

```csharp
// Get random float (0.0 to 1.0)
var rand = Api.GameState.Random();
Print($"Random: {rand}");

// Get random integer in range
var dice = Api.GameState.RandomRange(1, 7);  // 1-6
Print($"Dice roll: {dice}");

// Example: Random encounter check
var encounterChance = Api.GameState.Random();
if (encounterChance < 0.1)
{
    Print("Wild Pokemon appeared!");
}
else
{
    Print("No encounter");
}
```

---

## Dialogue API

### Show Messages

```csharp
// Show simple message
Api.Dialogue.ShowMessage("Hello, World!");

// Show message with speaker
Api.Dialogue.ShowMessage("Welcome to Pallet Town!", "Professor Oak");

// Show message with priority (higher = more important)
Api.Dialogue.ShowMessage("CRITICAL ALERT!", "System", 10);

// Check if dialogue is active
bool isActive = Api.Dialogue.IsDialogueActive;
Print($"Dialogue active: {isActive}");

// Clear all messages
Api.Dialogue.ClearMessages();
```

### Example: Simple Conversation

```csharp
Api.Dialogue.ShowMessage("Hello there!", "Oak");
// Wait for player to advance...
Api.Dialogue.ShowMessage("Welcome to the world of Pokemon!", "Oak");
```

---

## Map API

Check the Map API service for methods like:
- Getting current map
- Querying tiles
- Map transitions

```csharp
// Example usage (methods depend on your MapApiService implementation)
// var currentMap = Api.Map.GetCurrentMap();
// Print($"Current map: {currentMap}");
```

---

## NPC API

Check the NPC API service for methods like:
- Spawning NPCs
- Moving NPCs
- NPC interactions

```csharp
// Example usage (methods depend on your NpcApiService implementation)
// Api.Npc.SpawnNpc("trainer_001", new Point(5, 5));
```

---

## Effects API

Check the Effects API service for visual effect methods like:
- Screen shake
- Flash effects
- Particle effects

```csharp
// Example usage (methods depend on your EffectApiService implementation)
// Api.Effects.ScreenShake(0.5f, 1.0f);
```

---

## Combining APIs

### Example: Give Money and Show Message

```csharp
int amount = 1000;
Api.Player.GiveMoney(amount);
Api.Dialogue.ShowMessage($"You found ${amount}!", "System");
Print($"Player received ${amount}");
```

### Example: Check Flag and Set Variable

```csharp
if (Api.GameState.GetFlag("met_professor"))
{
    Api.GameState.SetVariable("professor_dialogue", "greeting_repeat");
    Print("Player has met professor before");
}
else
{
    Api.GameState.SetFlag("met_professor", true);
    Api.GameState.SetVariable("professor_dialogue", "greeting_first");
    Print("First time meeting professor");
}
```

### Example: Complete Quest Flow

```csharp
// Check if quest is complete
if (Api.GameState.GetFlag("delivered_parcel"))
{
    // Give reward
    Api.Player.GiveMoney(500);
    Api.GameState.SetFlag("parcel_quest_complete", true);

    // Show dialogue
    Api.Dialogue.ShowMessage("Thank you! Here's your reward.", "Professor Oak");

    // Update quest counter
    var questCount = Api.GameState.GetVariable("quests_completed");
    int count = int.Parse(questCount ?? "0");
    Api.GameState.SetVariable("quests_completed", (count + 1).ToString());

    Print($"Quest complete! Total quests: {count + 1}");
}
```

### Example: Debug Player State

```csharp
Print("=== PLAYER DEBUG INFO ===");
Print($"Name: {Api.Player.GetPlayerName()}");
Print($"Money: ${Api.Player.GetMoney()}");
Print($"Position: {Api.Player.GetPlayerPosition()}");
Print($"Facing: {Api.Player.GetPlayerFacing()}");
Print($"Movement Locked: {Api.Player.IsPlayerMovementLocked()}");
Print("========================");
```

### Example: Test Battle Outcome

```csharp
// Simulate winning a battle
var playerName = Api.Player.GetPlayerName();
int reward = 500;

Api.Player.GiveMoney(reward);
Api.GameState.SetFlag("defeated_rival", true);
Api.GameState.SetVariable("last_battle_result", "win");
Api.Dialogue.ShowMessage($"{playerName} won the battle!", "System");

Print($"Battle won! Reward: ${reward}");
```

### Example: Spawn Test NPCs

```csharp
// Example if your NPC API supports it
// for (int i = 0; i < 5; i++)
// {
//     var pos = new Point(i * 2, 5);
//     Api.Npc.SpawnNpc($"test_npc_{i}", pos);
//     Print($"Spawned NPC at ({pos.X}, {pos.Y})");
// }
```

---

## Advanced Patterns

### Store API Results in Variables

```csharp
// Store commonly used values
var playerMoney = Api.Player.GetMoney();
var playerPos = Api.Player.GetPlayerPosition();
var hasFlag = Api.GameState.GetFlag("important_flag");

// Use them in calculations
if (playerMoney >= 1000 && hasFlag)
{
    Print("Player meets criteria");
}
```

### Loop Through Flags

```csharp
var gymBadges = new[] { "boulder_badge", "cascade_badge", "thunder_badge" };
int badgeCount = 0;

foreach (var badge in gymBadges)
{
    if (Api.GameState.GetFlag(badge))
    {
        badgeCount++;
        Print($"✓ {badge}");
    }
    else
    {
        Print($"✗ {badge}");
    }
}

Print($"Total badges: {badgeCount}/{gymBadges.Length}");
```

### Create Helper Functions

```csharp
// Define once
void GiveReward(int money, string message)
{
    Api.Player.GiveMoney(money);
    Api.Dialogue.ShowMessage(message);
    Print($"Gave reward: ${money}");
}

// Use multiple times
GiveReward(100, "You found some money!");
GiveReward(500, "Congratulations on winning!");
```

---

## Tips & Best Practices

### 1. Use `Print()` for Debugging
```csharp
Print($"Before: ${Api.Player.GetMoney()}");
Api.Player.GiveMoney(100);
Print($"After: ${Api.Player.GetMoney()}");
```

### 2. Check Results
```csharp
bool success = Api.Player.TakeMoney(1000);
if (success)
{
    Print("Payment successful");
}
else
{
    Print("Not enough money!");
}
```

### 3. Use Meaningful Flag Names
```csharp
// Good
Api.GameState.SetFlag("defeated_gym_leader_brock", true);

// Less clear
Api.GameState.SetFlag("flag1", true);
```

### 4. Combine with Helper Methods
```csharp
// Use both API and helper methods
var player = GetPlayer();
Api.Player.GiveMoney(1000);
Inspect(player.Value);
```

### 5. Test Edge Cases
```csharp
// Test with no money
Print($"Money: {Api.Player.GetMoney()}");
bool result = Api.Player.TakeMoney(9999999);
Print($"Can take huge amount: {result}");
```

---

## Common Use Cases

### Reset Player State
```csharp
Api.Player.GiveMoney(-Api.Player.GetMoney()); // Set to 0 (or use TakeMoney)
Api.Player.SetPlayerMovementLocked(false);
Api.GameState.SetFlag("test_flag", false);
Print("Player state reset");
```

### Grant All Gym Badges
```csharp
var badges = new[] {
    "boulder_badge", "cascade_badge", "thunder_badge", "rainbow_badge",
    "soul_badge", "marsh_badge", "volcano_badge", "earth_badge"
};

foreach (var badge in badges)
{
    Api.GameState.SetFlag(badge, true);
}
Print($"Granted {badges.Length} gym badges");
```

### Test Dialogue System
```csharp
Api.Dialogue.ShowMessage("Line 1", "NPC");
Print($"Dialogue active: {Api.Dialogue.IsDialogueActive}");
Api.Dialogue.ClearMessages();
Print($"Dialogue active: {Api.Dialogue.IsDialogueActive}");
```

---

## Troubleshooting

### API Returns Null
```csharp
var name = Api.Player.GetPlayerName();
if (string.IsNullOrEmpty(name))
{
    Print("ERROR: Player name not found");
}
```

### Method Not Found
Make sure you're using the correct API:
- `Api.Player` for player methods
- `Api.GameState` for flags/variables
- `Api.Dialogue` for messages

### State Not Persisting
Remember that console state resets when you use the `reset` command. Store important values in game state flags/variables:

```csharp
// Instead of console variables
var myValue = 123;  // Lost on 'reset'

// Use game state
Api.GameState.SetVariable("my_value", "123");  // Persists
```

---

## See Also

- [Console Examples](CONSOLE_EXAMPLES.md) - General console usage
- [Console Implementation](QUAKE_CONSOLE_IMPLEMENTATION.md) - Technical details
- [Console Troubleshooting](CONSOLE_TROUBLESHOOTING.md) - Common issues

