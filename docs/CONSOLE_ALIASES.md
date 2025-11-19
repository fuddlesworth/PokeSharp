# Debug Console - Aliases & Macros

## Overview

The debug console includes a powerful **alias/macro system** that allows you to create custom shortcuts for frequently used commands. Aliases can be simple replacements or parameterized macros with `$1`, `$2`, etc. for dynamic arguments.

---

## Quick Start

### Basic Alias
```bash
# Define an alias
alias gm=Player.GiveMoney(1000)

# Use it
gm
```

### Parameterized Alias
```bash
# Define a macro with parameters
alias tp=Map.TransitionToMap(Map.GetCurrentMapId(), $1, $2)

# Use it with arguments
tp 10 20
```

---

## Commands

### `alias <name>=<command>`
Define a new alias or update an existing one.

**Syntax:**
```bash
alias <name>=<command>
```

**Examples:**
```bash
# Simple alias (no parameters)
alias money=Player.GetMoney()
alias fullhealth=Player.SetHealth(100)

# Parameterized macro
alias tp=Map.TransitionToMap(Map.GetCurrentMapId(), $1, $2)
alias gm=Player.GiveMoney($1)
alias flag=GameState.SetFlag($1, true)
alias warp=Map.TransitionToMap($1, $2, $3)
```

**Naming Rules:**
- Must start with a letter or underscore
- Can contain letters, numbers, and underscores
- No spaces or special characters (except underscore)

### `aliases`
List all defined aliases.

**Example Output:**
```
=== Defined Aliases ===
  gm = Player.GiveMoney($1)
  money = Player.GetMoney()
  tp = Map.TransitionToMap(Map.GetCurrentMapId(), $1, $2)
  warp = Map.TransitionToMap($1, $2, $3)

Total: 4 alias(es)
```

### `unalias <name>`
Remove an alias.

**Example:**
```bash
unalias tp
```

---

## Parameterized Macros

Aliases support **up to 9 parameters** (`$1` through `$9`) for dynamic values.

### How Parameters Work

When you define:
```bash
alias tp=Map.TransitionToMap(Map.GetCurrentMapId(), $1, $2)
```

And call:
```bash
tp 10 20
```

The console expands it to:
```csharp
Map.TransitionToMap(Map.GetCurrentMapId(), 10, 20)
```

### Parameter Substitution Rules

1. **`$1`** = First argument after alias name
2. **`$2`** = Second argument
3. **`$3`** = Third argument (and so on)
4. Arguments are separated by spaces
5. Missing parameters remain as `$N` (will cause C# compilation error)

### Example: Teleport Macro

```bash
# Define
alias tp=Map.TransitionToMap(Map.GetCurrentMapId(), $1, $2)

# Use
tp 10 20

# Expands to:
# Map.TransitionToMap(Map.GetCurrentMapId(), 10, 20)
```

### Example: Give Money Macro

```bash
# Define
alias gm=Player.GiveMoney($1)

# Use
gm 1000

# Expands to:
# Player.GiveMoney(1000)
```

### Example: Multi-Parameter Warp

```bash
# Define (map ID, x, y)
alias warp=Map.TransitionToMap($1, $2, $3)

# Use
warp 2 15 25

# Expands to:
# Map.TransitionToMap(2, 15, 25)
```

---

## Persistence

Aliases are **automatically saved** to disk when you:
- Define a new alias (`alias`)
- Remove an alias (`unalias`)

**File Location:**
```
PokeSharp.Game/bin/Debug/net9.0/Scripts/aliases.txt
```

**Format:**
```
tp=Map.TransitionToMap(Map.GetCurrentMapId(), $1, $2)
gm=Player.GiveMoney($1)
money=Player.GetMoney()
```

Aliases are **automatically loaded** when the console initializes.

---

## Common Patterns

### 1. Simple API Shortcuts

```bash
# Quick status checks
alias money=Player.GetMoney()
alias pos=Player.GetPlayerPosition()
alias map=Map.GetCurrentMapId()

# Quick actions
alias heal=Player.SetHealth(100)
alias speed=Player.SetSpeed(2.0f)
```

### 2. Development Teleports

```bash
# Teleport to common locations
alias home=Map.TransitionToMap(1, 10, 10)
alias town=Map.TransitionToMap(2, 5, 5)
alias cave=Map.TransitionToMap(3, 0, 0)

# Parameterized teleport (same map)
alias tp=Map.TransitionToMap(Map.GetCurrentMapId(), $1, $2)
```

### 3. Testing Utilities

```bash
# Give resources
alias gm=Player.GiveMoney($1)
alias giveitem=Player.GiveItem($1, $2)

# Set flags
alias flag=GameState.SetFlag($1, true)
alias unflag=GameState.SetFlag($1, false)
```

### 4. Debug Helpers

```bash
# Quick info dumps
alias debug=Print(Player.GetPlayerName() + " at " + Player.GetPlayerPosition())
alias entities=Print("Entities: " + CountEntities())

# Toggle features
alias godmode=Player.SetInvincible(true)
alias nogod=Player.SetInvincible(false)
```

### 5. Script Shortcuts

```bash
# Load commonly used scripts
alias info=load debug-info.csx
alias test=load quick-test.csx

# Parameterized script loading
alias teleport=load teleport $1 $2
alias give=load give-money $1
```

---

## Execution Flow

When you type a command, the console:

1. **Checks for alias** - Is the first word an alias?
2. **Expands parameters** - Replace `$1`, `$2`, etc. with arguments
3. **Displays expansion** - Shows both original and expanded command
4. **Executes** - Runs the expanded command as C# code

### Example Flow

**Input:**
```bash
tp 10 20
```

**Console Output:**
```
> tp 10 20 [alias]
  → Map.TransitionToMap(Map.GetCurrentMapId(), 10, 20)
```

**Execution:**
```csharp
// The console then executes:
Map.TransitionToMap(Map.GetCurrentMapId(), 10, 20)
```

---

## Best Practices

### ✅ DO

- **Use descriptive names** - `tp` instead of `t`, `gm` instead of `g`
- **Document complex macros** - Add comments in `aliases.txt`
- **Test with parameters** - Verify `$1`, `$2` substitution works
- **Keep it simple** - Aliases are for shortcuts, not complex logic
- **Use with scripts** - Combine aliases with `.csx` scripts for power

### ❌ DON'T

- **Overwrite built-in commands** - Don't alias `help`, `clear`, `load`, etc.
- **Use spaces in names** - `my alias` won't work, use `my_alias`
- **Make them too complex** - Use scripts for multi-line logic
- **Forget parameter counts** - Missing `$N` will cause errors

---

## Advanced Examples

### Multi-Step Setup Macro

```bash
# Define a "dev mode" shortcut
alias devmode=Player.GiveMoney(10000); Player.SetHealth(999); Player.SetSpeed(3.0f)
```

### Conditional Teleport

```bash
# Teleport only if player has money
alias safewarp=if (Player.GetMoney() >= 100) Map.TransitionToMap($1, $2, $3)
```

### Logging Helper

```bash
# Print with timestamp
alias log=Print("[" + DateTime.Now.ToString("HH:mm:ss") + "] $1")
```

### Script Chain

```bash
# Load multiple scripts
alias fulltest=load debug-info.csx; load quick-test.csx
```

---

## Integration with Other Features

### With Scripts
```bash
# Define alias to load parameterized script
alias teleport=load teleport $1 $2

# Use it
teleport 10 20
```

### With Startup Script
Add aliases to `startup.csx` for auto-load:

```csharp
// startup.csx
Print("Defining aliases...");

// Note: You can't use the "alias" command in scripts
// Instead, define C# functions (which we already do!)
void TP(int x, int y) {
    Map.TransitionToMap(Map.GetCurrentMapId(), x, y);
}
```

### With Command History
- Aliases expand **before** execution
- History stores the **original alias call** (e.g., `tp 10 20`)
- Press Up/Down to recall alias commands

---

## Troubleshooting

### "Alias not found: X"
- The alias doesn't exist. Use `aliases` to list all defined aliases.
- Did you define it? Use `alias X=<command>` to create it.

### "Failed to define alias: X"
- The alias name is invalid (contains spaces or special characters).
- Use only letters, numbers, and underscores.

### "Compilation error" when using alias
- Check parameter count: `$1`, `$2`, etc. must match arguments
- Verify C# syntax in the command
- Test the expanded command directly first

### Alias doesn't have expected parameters
- Did you provide enough arguments? `tp 10` needs two args if macro has `$1` and `$2`
- Check parameter order: `$1` is first, `$2` is second, etc.

---

## Command Reference

| Command | Description | Example |
|---------|-------------|---------|
| `alias <name>=<cmd>` | Define/update alias | `alias tp=Map.TransitionToMap(...)` |
| `unalias <name>` | Remove alias | `unalias tp` |
| `aliases` | List all aliases | `aliases` |

---

## File Format (`aliases.txt`)

The `aliases.txt` file is a simple text format:

```
# Comments start with #
# Each line is: name=command

# Simple aliases
money=Player.GetMoney()
heal=Player.SetHealth(100)

# Parameterized macros
tp=Map.TransitionToMap(Map.GetCurrentMapId(), $1, $2)
gm=Player.GiveMoney($1)
warp=Map.TransitionToMap($1, $2, $3)
```

**Note:** You can manually edit this file, but the console will auto-save on every `alias`/`unalias` command.

---

## See Also

- **[Script Loading/Saving](CONSOLE_SCRIPT_LOADING.md)** - For reusable `.csx` scripts
- **[API Reference](../Scripts/API_REFERENCE.md)** - For available methods
- **[Console Features](CONSOLE_FEATURES.md)** - For all console capabilities

---

**Happy aliasing! 🚀**

