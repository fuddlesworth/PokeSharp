using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace PokeSharp.Engine.Core.Events.Modding;

/// <summary>
///     Thread-safe event bus for mod event handling with error isolation.
/// </summary>
/// <remarks>
///     <para>
///         DESIGN: Permissive by default - mods get full event access.
///         Safety comes from error isolation, not restriction.
///     </para>
///     <para>
///         SAFETY FEATURES:
///         - Exception isolation: One handler's error doesn't affect others
///         - Timeout warnings: Long handlers are logged (not killed)
///         - Stats tracking: Monitor handler performance
///         - Per-mod cleanup: Unload clears only that mod's subscriptions
///     </para>
/// </remarks>
public sealed class ModEventBus : IModEventBus
{
    private readonly ConcurrentDictionary<Type, List<HandlerEntry>> _handlers = new();
    private readonly object _lock = new();
    private readonly ILogger<ModEventBus> _logger;
    private readonly ConcurrentDictionary<string, List<IDisposable>> _modSubscriptions = new();

    // Configuration
    private readonly long _slowHandlerThresholdUs;
    private long _handlerTimeSamples;
    private long _totalErrorsIsolated;

    // Stats
    private long _totalEventsPublished;
    private long _totalHandlersInvoked;
    private long _totalHandlerTimeUs;

    public ModEventBus(ILogger<ModEventBus>? logger = null, long slowHandlerThresholdMs = 16)
    {
        _logger = logger ?? NullLogger<ModEventBus>.Instance;
        _slowHandlerThresholdUs = slowHandlerThresholdMs * 1000; // Convert to microseconds
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(
        Action<TEvent> handler,
        ModEventPriority priority = ModEventPriority.Normal
    )
        where TEvent : struct
    {
        return SubscribeInternal<TEvent>(evt => handler(evt), priority, null);
    }

    /// <inheritdoc />
    public IDisposable SubscribeCancellable<TEvent>(
        ModCancellableHandler<TEvent> handler,
        ModEventPriority priority = ModEventPriority.Normal
    )
        where TEvent : struct, IModCancellableEvent
    {
        return SubscribeCancellableInternal(handler, priority, null);
    }

    /// <inheritdoc />
    public void Publish<TEvent>(TEvent evt)
        where TEvent : struct
    {
        Interlocked.Increment(ref _totalEventsPublished);

        var eventType = typeof(TEvent);
        if (!_handlers.TryGetValue(eventType, out var handlers))
            return;

        List<HandlerEntry> snapshot;
        lock (_lock)
        {
            snapshot = handlers.ToList();
        }

        // Sort by priority (lower value = higher priority = execute first)
        snapshot.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        foreach (var entry in snapshot)
            InvokeHandler(entry, evt);
    }

    /// <inheritdoc />
    public bool PublishCancellable<TEvent>(ref TEvent evt)
        where TEvent : struct, IModCancellableEvent
    {
        Interlocked.Increment(ref _totalEventsPublished);

        var eventType = typeof(TEvent);
        if (!_handlers.TryGetValue(eventType, out var handlers))
            return true; // No handlers = not cancelled

        List<HandlerEntry> snapshot;
        lock (_lock)
        {
            snapshot = handlers.ToList();
        }

        snapshot.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        foreach (var entry in snapshot)
        {
            InvokeCancellableHandler(entry, ref evt);

            if (evt.IsCancelled)
            {
                _logger.LogDebug(
                    "[MOD] Event {EventType} cancelled by {ModId}: {Reason}",
                    eventType.Name,
                    evt.CancelledBy ?? "unknown",
                    evt.CancelReason ?? "no reason"
                );
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public ModEventStats GetStats()
    {
        var avgTime =
            _handlerTimeSamples > 0 ? (double)_totalHandlerTimeUs / _handlerTimeSamples : 0;

        var activeCount = 0;
        foreach (var kvp in _handlers)
            lock (_lock)
            {
                activeCount += kvp.Value.Count;
            }

        return new ModEventStats
        {
            TotalEventsPublished = Interlocked.Read(ref _totalEventsPublished),
            TotalHandlersInvoked = Interlocked.Read(ref _totalHandlersInvoked),
            TotalErrorsIsolated = Interlocked.Read(ref _totalErrorsIsolated),
            AverageHandlerTimeMicroseconds = avgTime,
            ActiveSubscriptions = activeCount,
        };
    }

    /// <inheritdoc />
    public void ClearModSubscriptions(string modId)
    {
        if (_modSubscriptions.TryRemove(modId, out var subscriptions))
        {
            foreach (var sub in subscriptions)
                sub.Dispose();

            _logger.LogInformation(
                "[MOD] Cleared {Count} event subscriptions for mod {ModId}",
                subscriptions.Count,
                modId
            );
        }
    }

    /// <summary>
    ///     Subscribe with mod tracking for cleanup during unload.
    /// </summary>
    public IDisposable Subscribe<TEvent>(
        Action<TEvent> handler,
        string modId,
        ModEventPriority priority = ModEventPriority.Normal
    )
        where TEvent : struct
    {
        var subscription = SubscribeInternal<TEvent>(evt => handler(evt), priority, modId);

        // Track for mod cleanup
        var modSubs = _modSubscriptions.GetOrAdd(modId, _ => new List<IDisposable>());
        lock (modSubs)
        {
            modSubs.Add(subscription);
        }

        return subscription;
    }

    /// <summary>
    ///     Subscribe cancellable handler with mod tracking.
    /// </summary>
    public IDisposable SubscribeCancellable<TEvent>(
        ModCancellableHandler<TEvent> handler,
        string modId,
        ModEventPriority priority = ModEventPriority.Normal
    )
        where TEvent : struct, IModCancellableEvent
    {
        var subscription = SubscribeCancellableInternal(handler, priority, modId);

        var modSubs = _modSubscriptions.GetOrAdd(modId, _ => new List<IDisposable>());
        lock (modSubs)
        {
            modSubs.Add(subscription);
        }

        return subscription;
    }

    private IDisposable SubscribeInternal<TEvent>(
        Action<TEvent> handler,
        ModEventPriority priority,
        string? modId
    )
        where TEvent : struct
    {
        var eventType = typeof(TEvent);
        var handlers = _handlers.GetOrAdd(eventType, _ => new List<HandlerEntry>());

        var entry = new HandlerEntry
        {
            Handler = evt => handler((TEvent)evt),
            Priority = (int)priority,
            ModId = modId,
            EventType = eventType,
        };

        lock (_lock)
        {
            handlers.Add(entry);
        }

        _logger.LogDebug(
            "[MOD] Subscribed to {EventType} with priority {Priority}{ModInfo}",
            eventType.Name,
            priority,
            modId != null ? $" (mod: {modId})" : ""
        );

        return new Subscription(this, entry);
    }

    private IDisposable SubscribeCancellableInternal<TEvent>(
        ModCancellableHandler<TEvent> handler,
        ModEventPriority priority,
        string? modId
    )
        where TEvent : struct, IModCancellableEvent
    {
        var eventType = typeof(TEvent);
        var handlers = _handlers.GetOrAdd(eventType, _ => new List<HandlerEntry>());

        var entry = new HandlerEntry
        {
            CancellableHandler = (ref object evt) =>
            {
                var typed = (TEvent)evt;
                handler(ref typed);
                evt = typed;
            },
            Priority = (int)priority,
            ModId = modId,
            EventType = eventType,
            IsCancellable = true,
        };

        lock (_lock)
        {
            handlers.Add(entry);
        }

        return new Subscription(this, entry);
    }

    private void InvokeHandler<TEvent>(HandlerEntry entry, TEvent evt)
        where TEvent : struct
    {
        Interlocked.Increment(ref _totalHandlersInvoked);

        var sw = Stopwatch.StartNew();
        try
        {
            entry.Handler?.Invoke(evt);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _totalErrorsIsolated);
            _logger.LogError(
                ex,
                "[MOD] Handler error for {EventType}{ModInfo}: {Message}",
                typeof(TEvent).Name,
                entry.ModId != null ? $" (mod: {entry.ModId})" : "",
                ex.Message
            );
        }
        finally
        {
            sw.Stop();
            var elapsedUs = sw.ElapsedTicks * 1_000_000 / Stopwatch.Frequency;

            Interlocked.Add(ref _totalHandlerTimeUs, elapsedUs);
            Interlocked.Increment(ref _handlerTimeSamples);

            if (elapsedUs > _slowHandlerThresholdUs)
                _logger.LogWarning(
                    "[MOD] Slow handler for {EventType}{ModInfo}: {ElapsedMs:F2}ms",
                    typeof(TEvent).Name,
                    entry.ModId != null ? $" (mod: {entry.ModId})" : "",
                    elapsedUs / 1000.0
                );
        }
    }

    private void InvokeCancellableHandler<TEvent>(HandlerEntry entry, ref TEvent evt)
        where TEvent : struct, IModCancellableEvent
    {
        Interlocked.Increment(ref _totalHandlersInvoked);

        var sw = Stopwatch.StartNew();
        try
        {
            if (entry.CancellableHandler != null)
            {
                object boxed = evt;
                entry.CancellableHandler(ref boxed);
                evt = (TEvent)boxed;

                // Track who cancelled
                if (evt.IsCancelled && evt.CancelledBy == null && entry.ModId != null)
                    evt.CancelledBy = entry.ModId;
            }
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _totalErrorsIsolated);
            _logger.LogError(
                ex,
                "[MOD] Cancellable handler error for {EventType}{ModInfo}: {Message}",
                typeof(TEvent).Name,
                entry.ModId != null ? $" (mod: {entry.ModId})" : "",
                ex.Message
            );
        }
        finally
        {
            sw.Stop();
            var elapsedUs = sw.ElapsedTicks * 1_000_000 / Stopwatch.Frequency;

            Interlocked.Add(ref _totalHandlerTimeUs, elapsedUs);
            Interlocked.Increment(ref _handlerTimeSamples);

            if (elapsedUs > _slowHandlerThresholdUs)
                _logger.LogWarning(
                    "[MOD] Slow cancellable handler for {EventType}{ModInfo}: {ElapsedMs:F2}ms",
                    typeof(TEvent).Name,
                    entry.ModId != null ? $" (mod: {entry.ModId})" : "",
                    elapsedUs / 1000.0
                );
        }
    }

    private void Unsubscribe(HandlerEntry entry)
    {
        if (_handlers.TryGetValue(entry.EventType, out var handlers))
            lock (_lock)
            {
                handlers.Remove(entry);
            }
    }

    private sealed class HandlerEntry
    {
        public Action<object>? Handler { get; init; }
        public CancellableHandlerDelegate? CancellableHandler { get; init; }
        public int Priority { get; init; }
        public string? ModId { get; init; }
        public Type EventType { get; init; } = null!;
        public bool IsCancellable { get; init; }
    }

    private delegate void CancellableHandlerDelegate(ref object evt);

    private sealed class Subscription : IDisposable
    {
        private readonly ModEventBus _bus;
        private readonly HandlerEntry _entry;
        private bool _disposed;

        public Subscription(ModEventBus bus, HandlerEntry entry)
        {
            _bus = bus;
            _entry = entry;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _bus.Unsubscribe(_entry);
                _disposed = true;
            }
        }
    }
}
