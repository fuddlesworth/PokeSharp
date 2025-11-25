# Watch System - Quick Reference Card

## 🎹 Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+1` | Switch to Console tab |
| `Ctrl+2` | Switch to Watch tab |
| `Ctrl+3` | Switch to Logs tab |
| `Ctrl+4` | Switch to Variables tab |

## 📝 Basic Commands

```bash
# View watches
watch                              # Show watch panel

# Add/remove watches
watch add <name> <expression>      # Add new watch (max 50)
watch remove <name>                # Remove specific watch
watch clear                        # Remove all watches

# Control updates
watch toggle                       # Turn auto-update on/off
watch interval <ms>                # Set update speed (100-60000ms)
```

## ⭐ Advanced Commands

```bash
# Pin important watches to top
watch pin <name>                   # Pin watch
watch unpin <name>                 # Unpin watch

# Adjust update frequency
watch interval 100                 # Fast (100ms)
watch interval 500                 # Default (500ms)
watch interval 1000                # Slow (1 second)
watch interval 5000                # Very slow (5 seconds)
```

## 💡 Examples

### Setup Critical Watches
```bash
# Add and pin health monitoring
watch add hp Player.GetHP()
watch pin hp

# Add battle state
watch add state Game.GetBattleState()
watch pin state

# Add position tracking
watch add pos Player.GetPosition()
```

### Optimize Performance
```bash
# For expensive expressions, slow down updates
watch interval 2000     # Update every 2 seconds

# For fast-changing values, speed up
watch interval 200      # Update 5x per second
```

### Organize Your Watches
```bash
# Pin critical metrics
watch pin hp
watch pin mp
watch pin state

# Keep detailed metrics unpinned
watch add money Player.GetMoney()
watch add xp Player.GetXP()
```

## 📊 Watch Panel Display

```
═══════════════════════════════════════════════════════════════
  WATCH EXPRESSIONS (7/50) [43 remaining]
  AUTO-UPDATE ON | Interval: 500ms | 2 pinned
═══════════════════════════════════════════════════════════════

  [1] hp [PINNED]                    <- Pinned watches first
      Expression: Player.GetHP()
      Value:      85  (was: 100)     <- Change detection
      Updated:    just now (42 times)

  [2] state [PINNED]
      Expression: Game.GetBattleState()
      Value:      "InBattle"
      Updated:    0.5s ago (40 times)

  [3] money                          <- Regular watches
      Expression: Player.GetMoney()
      Value:      1500
      Updated:    just now (42 times)

─────────────────────────────────────────────────────────────
Total: 3 watch(es) | All OK
```

## 🎯 Best Practices

### ✅ DO

- **Pin critical values** that need constant monitoring
- **Adjust interval** based on expression cost
- **Use descriptive names** for easy identification
- **Remove unused watches** to stay under the 50 limit
- **Toggle auto-update OFF** when not actively watching

### ❌ DON'T

- **Don't add 50 watches** - you'll hit the limit
- **Don't use very fast intervals** (< 200ms) for expensive expressions
- **Don't leave auto-update ON** when console is minimized
- **Don't pin too many watches** - defeats the purpose

## 🚀 Power User Tips

### 1. Session Workflow
```bash
# Start of debugging session
watch add hp Player.GetHP()
watch add mp Player.GetMP()
watch pin hp

# During investigation
watch add suspect SomeClass.SuspiciousValue()
watch interval 200     # Watch closely

# After finding issue
watch remove suspect
watch interval 500     # Back to normal
```

### 2. Performance Debugging
```bash
# Add performance metrics
watch add fps Game.GetFPS()
watch add drawCalls Renderer.GetDrawCallCount()
watch add entities World.GetEntityCount()

# Pin the bottleneck
watch pin drawCalls

# Slow down updates to reduce overhead
watch interval 1000
```

### 3. State Monitoring
```bash
# Track game state transitions
watch add state Game.GetState()
watch add scene Scene.GetCurrentScene()
watch add loading Loading.GetProgress()

# Pin state for visibility
watch pin state
```

## 🔧 Troubleshooting

### "Watch limit reached"
- **Problem**: Already have 50 watches
- **Solution**: Remove unused watches with `watch remove <name>` or `watch clear`

### "Invalid interval"
- **Problem**: Interval outside valid range
- **Solution**: Use 100-60000 milliseconds (0.1s - 60s)

### Watches updating too fast/slow
- **Problem**: Default interval not optimal
- **Solution**: Adjust with `watch interval <ms>`

### Can't find specific watch
- **Problem**: Too many watches in list
- **Solution**: Pin important ones or remove unused watches

## 📐 Technical Limits

| Limit | Value |
|-------|-------|
| Max watches | 50 |
| Min interval | 100ms (0.1s) |
| Max interval | 60000ms (60s) |
| Default interval | 500ms (0.5s) |
| Warning threshold | 40 watches (80%) |

## 🎓 Related Commands

```bash
log                    # View system logs
log filter Warning     # Filter logs by level
alias                  # Manage command aliases
script                 # Manage script files
bookmark               # Quick-execute commands with F-keys
```

---

**Tip**: Press `Ctrl+2` anytime to quickly view your watches, then `Ctrl+1` to return to the console!

