# CRITICAL BUG FIX: Event Pooling Capture Pattern

## 🐛 Critical Issue Found & Fixed

**Reported by:** User  
**Severity:** CRITICAL  
**Impact:** Cancellation mechanism completely broken

---

## The Problem

The original lambda-based `PublishPooled` pattern **cannot work** for cancellable events because:

1. Lambda executes **BEFORE** handlers run
2. Values captured inside lambda reflect **initial state**, not handler modifications
3. Cancellation checks always see `IsCancelled = false`

### Broken Code (Original Design)

```csharp
bool wasCancelled = false;

_eventBus.PublishPooled<MovementStartedEvent>(evt =>
{
    evt.Entity = entity;
    // ❌ BUG: Handlers haven't run yet!
    wasCancelled = evt.IsCancelled;  // Always false
});

if (wasCancelled)  // Never executes!
{
    // This code is unreachable
}
```

### Execution Flow (Why It Fails)

```
1. RentEvent from pool
2. Execute configure lambda      ← Values captured HERE (before handlers!)
   - evt.Entity = entity
   - wasCancelled = evt.IsCancelled  ← Always false at this point!
3. Publish to handlers           ← Handlers modify evt.IsCancelled HERE
4. Return to pool
5. Check wasCancelled            ← Still false (handlers ran too late!)
```

**Result:** Cancellation mechanism completely broken. Handlers can't control flow.

---

## The Solution: Two Patterns

### Pattern 1: Cancellable Events (Use RentEvent/ReturnEvent)

For events where handlers modify state you need to check:

```csharp
var evt = _eventBus.RentEvent<MovementStartedEvent>();
try
{
    evt.Entity = entity;
    evt.Direction = Direction.North;
    
    _eventBus.Publish(evt);
    
    // ✅ Check AFTER handlers run (before returning to pool)
    if (evt.IsCancelled)
    {
        HandleCancellation(evt.CancellationReason);
        return;
    }
}
finally
{
    _eventBus.ReturnEvent(evt);  // Always return
}
```

**Execution Flow (Correct):**
```
1. RentEvent from pool
2. Configure event properties
3. Publish to handlers           ← Handlers modify evt.IsCancelled
4. Check evt.IsCancelled         ← Now reflects handler modifications!
5. Return to pool (finally)
```

### Pattern 2: Notification Events (Use PublishPooled)

For events where handlers only read (no modifications to check):

```csharp
// Simple one-liner for notification events
_eventBus.PublishPooled<MovementCompletedEvent>(evt =>
{
    evt.Entity = entity;
    evt.OldPosition = oldPos;
    evt.NewPosition = newPos;
});
```

This works fine because we don't need to read the event after handlers run.

---

## Events by Pattern

### Use RentEvent/ReturnEvent (Cancellable)
- ✅ `MovementStartedEvent` - Handlers can cancel movement
- ✅ `CollisionCheckEvent` - Handlers can block collision

### Use PublishPooled (Notification)
- ✅ `MovementCompletedEvent` - Handlers only observe
- ✅ `MovementBlockedEvent` - Handlers only observe
- ✅ `CollisionDetectedEvent` - Handlers only observe
- ✅ `CollisionResolvedEvent` - Handlers only observe

---

## Code Fixed

### MovementSystem.cs

**Before (Broken):**
```csharp
bool wasCancelled = false;
_eventBus.PublishPooled<MovementStartedEvent>(evt => {
    evt.Entity = entity;
    wasCancelled = evt.IsCancelled;  // ❌ Always false
});
if (wasCancelled) { /* never executes */ }
```

**After (Fixed):**
```csharp
var startEvent = _eventBus.RentEvent<MovementStartedEvent>();
try
{
    startEvent.Entity = entity;
    _eventBus.Publish(startEvent);
    
    // ✅ Correctly checks after handlers run
    if (startEvent.IsCancelled)
    {
        HandleCancellation();
        return;
    }
}
finally
{
    _eventBus.ReturnEvent(startEvent);
}
```

### CollisionSystem.cs

**Before (Broken):**
```csharp
bool wasBlocked = false;
_eventBus.PublishPooled<CollisionCheckEvent>(evt => {
    evt.MapId = mapId;
    wasBlocked = evt.IsBlocked;  // ❌ Always false
});
if (wasBlocked) { /* never executes */ }
```

**After (Fixed):**
```csharp
var checkEvent = _eventBus.RentEvent<CollisionCheckEvent>();
try
{
    checkEvent.MapId = mapId;
    _eventBus.Publish(checkEvent);
    
    // ✅ Correctly checks after handlers run
    if (checkEvent.IsBlocked)
    {
        return false;
    }
}
finally
{
    _eventBus.ReturnEvent(checkEvent);
}
```

---

## API Changes

### IEventBus Interface

**Added Methods:**
```csharp
TEvent RentEvent<TEvent>() where TEvent : class, IPoolableEvent, new();
void ReturnEvent<TEvent>(TEvent evt) where TEvent : class, IPoolableEvent, new();
```

**Updated Documentation:**
- `PublishPooled()` - Now documented for notification events only
- Added examples showing both patterns

---

## Verification

✅ **Build Status:** All projects compile  
✅ **Linter:** No errors  
✅ **Pattern:** Correct for both event types  
✅ **Documentation:** Updated with correct examples  

---

## Summary

The lambda-based `PublishPooled()` is **only safe for notification events** where you don't need to check handler modifications. For **cancellable events**, you **must** use the manual `RentEvent`/`ReturnEvent` pattern to read event state after handlers execute.

**Critical lesson:** Convenience APIs can't always support all use cases. Sometimes explicit is better than implicit.

