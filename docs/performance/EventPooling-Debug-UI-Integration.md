# Event Pool Statistics - Debug UI Integration

## Overview

Event pool statistics are now integrated into the **Stats** tab (F3 or ` key → Stats tab) of the debug console, providing real-time monitoring of event pooling performance.

---

## How to Access

1. **Open Debug Console**: Press ` (backtick) or F3
2. **Navigate to Stats Tab**: Press `8` or type `tab stats`
3. **Scroll Down**: Event pool statistics appear after the ECS World section

---

## Displayed Metrics

### Event Pool Section

```
═══════════════════════════════════════════════════════════
Event Pools                                     95% reuse ✓
───────────────────────────────────────────────────────────
Event Types: 6                              1,234 allocs
Published:   50,234                       -48,000 allocs ✓
In Flight:   2                              
Hot Event:   MovementStarted               12,345x
═══════════════════════════════════════════════════════════
```

### Metrics Explained

| Metric | Description | Good Value |
|--------|-------------|------------|
| **Event Types** | Number of different event types with pools | Varies |
| **Reuse %** | `(Rented - Created) / Rented * 100` | **>95%** (green) |
| **Published** | Total events published (lifetime) | - |
| **Allocs** | Total new instances created | **<5%** of published |
| **-X allocs** | Allocations saved by pooling | **High** (green) |
| **In Flight** | Events not yet returned to pool | **<10** (green) |
| **Hot Event** | Most frequently published event | - |

---

## Color Coding

### Reuse Rate
- 🟢 **Green (≥95%)**: Excellent pooling efficiency
- 🟡 **Yellow (80-94%)**: Good, but could improve
- 🔴 **Red (<80%)**: Pool not warming up or Reset() allocating

### In Flight
- 🟢 **Green (<10)**: Normal, events being returned promptly
- 🟡 **Yellow (10-49)**: Moderate lag, check for slow handlers
- 🔴 **Red (≥50)**: **Potential memory leak!** Events not being returned

### Allocation Savings
- 🟢 **Green**: Positive savings (pooling working)
- Shows: `-X allocs` where X = Rented - Created

---

## What to Look For

### ✅ Healthy Pool Stats

```
Event Pools                                     96% reuse ✓
───────────────────────────────────────────────────────────
Event Types: 6                                  234 allocs
Published:   12,345                          -12,111 allocs ✓
In Flight:   3                              
Hot Event:   MovementCompleted               5,234x
```

**Indicators:**
- Reuse rate >95% (green)
- Allocations < 5% of published
- In Flight < 10
- Large negative allocation savings

### ⚠️ Warming Up

```
Event Pools                                     75% reuse
───────────────────────────────────────────────────────────
Event Types: 6                                  150 allocs
Published:   600                                -450 allocs
In Flight:   5                              
```

**Indicators:**
- Reuse rate 70-85% (yellow)
- Early in session (pool filling)
- Should improve over time

### ❌ Problem: Memory Leak

```
Event Pools                                     92% reuse ✓
───────────────────────────────────────────────────────────
Event Types: 6                                1,234 allocs
Published:   15,000                         -13,766 allocs ✓
In Flight:   67                             Check for leaks!
```

**Indicators:**
- ⚠️ In Flight >50 (red warning)
- Events not being returned
- **Action**: Check for missing `finally` blocks or exception handling

### ❌ Problem: Reset() Allocating

```
Event Pools                                     45% reuse
───────────────────────────────────────────────────────────
Event Types: 6                               55,000 allocs
Published:   100,000                        -45,000 allocs
In Flight:   4                              
```

**Indicators:**
- Very low reuse rate (<80%)
- Created count >> expected
- **Action**: Check `Reset()` methods for `Guid.NewGuid()` or other allocations

---

## Interpreting the Hot Event

**Purpose**: Shows which event type is published most frequently, helping identify optimization targets.

**Example:**
```
Hot Event:   MovementCompleted               5,234x
```

- `MovementCompleted` was published 5,234 times
- This is the most frequent event in your game
- Optimization focus should be on this event type

---

## Performance Targets

### For 100 NPCs @ 60 FPS

| Metric | Target | Reason |
|--------|--------|--------|
| Reuse Rate | **>95%** | Pool is effectively reusing instances |
| Published/sec | **500-1,000** | Typical for 100 wandering NPCs |
| Allocations/sec | **<50** | Only when pool grows or new event types |
| In Flight | **<10** | Events returned within 1-2 frames |

### Expected Stats

After 60 seconds of play with 100 NPCs:
```
Event Pools                                     98% reuse ✓
───────────────────────────────────────────────────────────
Event Types: 6                                1,200 allocs
Published:   60,000                         -58,800 allocs ✓
In Flight:   5                              
Hot Event:   MovementCompleted               20,000x
```

---

## Debugging with Event Pool Stats

### Problem: Low FPS + Low Reuse Rate

**Symptom:**
- FPS: 30-40 (should be 60)
- Reuse Rate: 65% (red)
- Gen0 GC: High

**Diagnosis:** `Reset()` is allocating!

**Solution:** Check `NotificationEventBase.Reset()` - ensure no `Guid.NewGuid()` or `DateTime.UtcNow`

### Problem: High In Flight Count

**Symptom:**
- In Flight: 85 (red)
- Warning: "Check for leaks!"

**Diagnosis:** Events not being returned

**Solution:**
1. Check for missing `finally` blocks in pooled event usage
2. Ensure exceptions don't skip `ReturnEvent()`
3. Look for `RentEvent()` without matching `ReturnEvent()`

### Problem: High Reuse But Still Low FPS

**Symptom:**
- Reuse Rate: 97% (green)
- Published: 2,000,000 (very high)
- FPS: 40

**Diagnosis:** Too many events being published

**Solution:**
- Event system overhead (handlers, not pooling)
- Reduce event frequency or batch events
- Profile event handlers for slow code

---

## Advanced: Comparing Before/After Pooling

### Before Event Pooling

```
Performance Stats                          Frame: 3,600
───────────────────────────────────────────────────────────
FPS:        45.2                            Fair
Frame Time: 22.14ms
Memory:     456.3 MB
GC:         G0: 180  G1: 8  G2: 2          +30/2/0/s
```

**Gen0 collections:** 180 total, +30/sec = Every 2 seconds

### After Event Pooling (Optimized)

```
Performance Stats                          Frame: 3,600
───────────────────────────────────────────────────────────
FPS:        60.0                            Excellent ✓
Frame Time: 16.67ms
Memory:     248.1 MB
GC:         G0: 15  G1: 1  G2: 0           +0/0/0/s ✓

Event Pools                                     98% reuse ✓
───────────────────────────────────────────────────────────
Event Types: 6                                  234 allocs
Published:   30,000                         -29,766 allocs ✓
In Flight:   3                              
Hot Event:   MovementCompleted               12,345x
```

**Improvements:**
- **2x FPS** (45 → 60)
- **-45% Memory** (456 MB → 248 MB)
- **Gen0 Collections: -92%** (180 → 15)
- **29,766 allocations saved**

---

## Quick Reference

### Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `` ` `` or `F3` | Toggle debug console |
| `8` | Jump to Stats tab |
| `PageUp/PageDown` | Scroll stats |
| `Home/End` | Jump to top/bottom |

### Console Commands

```bash
# Navigate to Stats tab
tab stats
tab 8

# Export stats
export stats              # Copy to clipboard
export stats csv          # Copy as CSV

# Refresh manually
stats refresh

# Set update rate
stats interval 2          # Update every 2 frames (~30fps)
```

---

## Troubleshooting

### Event Pool Section Not Showing

**Cause:** No event pools initialized yet

**Solution:**
1. Wait for events to be published (move around, interact with NPCs)
2. Ensure `EventPool<T>.Shared` is being used
3. Check that `IEventBus.GetPoolStatistics()` returns data

### Stats Show Zero

**Cause:** Reflection failed to get `IEventBus`

**Solution:**
1. Ensure `IEventBus` is registered in DI
2. Check that `EventBus` implements `GetPoolStatistics()`
3. Verify pools are actually being used (not manual `new Event()`)

### Reuse Rate Stuck at 0%

**Cause:** Pool never warming up (always creating new instances)

**Solution:**
1. Pool size too large (events never reused before GC)
2. Check `Return()` is actually being called
3. Verify `In Flight` count - should fluctuate, not always increase

---

## Summary

Event pool statistics in the Stats tab provide real-time insight into:

- ✅ **Pooling efficiency** (reuse rate)
- ✅ **Memory savings** (allocations prevented)
- ✅ **Potential leaks** (in-flight warnings)
- ✅ **Hot paths** (most-used event types)

**Target:** 95%+ reuse rate with <10 in-flight events for optimal performance!

