using System.Collections.Concurrent;
using System.Diagnostics;
using PokeSharp.Engine.Core.Events;

namespace PokeSharp.Engine.UI.Debug.Core;

/// <summary>
///     Tracks performance metrics for event bus operations.
///     Only collects data when the Event Inspector is active to minimize overhead.
/// </summary>
public class EventMetrics : IEventMetrics
{
    private readonly ConcurrentDictionary<string, EventTypeMetrics> _eventMetrics = new();
    private readonly ConcurrentDictionary<string, SubscriptionMetrics> _subscriptionMetrics = new();
    private bool _isEnabled;

    // OPTIMIZATION: Cached results to avoid LINQ allocations on every frame
    private List<EventTypeMetrics>? _cachedEventMetrics;
    private int _cachedEventMetricsVersion;
    private int _eventMetricsVersion;

    /// <summary>
    ///     Gets or sets whether metrics collection is enabled.
    ///     When disabled, all tracking calls are no-ops for minimal performance impact.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    /// <summary>
    ///     Records a publish operation for an event type.
    /// </summary>
    /// <param name="eventTypeName">Name of the event type.</param>
    /// <param name="elapsedNanoseconds">Time taken in nanoseconds (from Stopwatch).</param>
    public void RecordPublish(string eventTypeName, long elapsedNanoseconds)
    {
        if (!_isEnabled) return;

        var metrics = _eventMetrics.GetOrAdd(eventTypeName, _ =>
        {
            Interlocked.Increment(ref _eventMetricsVersion); // Invalidate cache
            return new EventTypeMetrics(eventTypeName);
        });
        // Convert nanoseconds to milliseconds for consistent display
        double elapsedMs = elapsedNanoseconds / 1_000_000.0;
        metrics.RecordPublish(elapsedMs);
    }

    /// <summary>
    ///     Records a handler invocation.
    /// </summary>
    /// <param name="eventTypeName">Name of the event type.</param>
    /// <param name="handlerId">Handler identifier.</param>
    /// <param name="elapsedNanoseconds">Time taken in nanoseconds (from Stopwatch).</param>
    public void RecordHandlerInvoke(string eventTypeName, int handlerId, long elapsedNanoseconds)
    {
        if (!_isEnabled) return;

        var metrics = _eventMetrics.GetOrAdd(eventTypeName, _ => new EventTypeMetrics(eventTypeName));
        // Convert nanoseconds to milliseconds for consistent display
        double elapsedMs = elapsedNanoseconds / 1_000_000.0;
        metrics.RecordHandlerInvoke(elapsedMs);

        string key = $"{eventTypeName}:{handlerId}";
        var subMetrics = _subscriptionMetrics.GetOrAdd(key, _ => new SubscriptionMetrics(eventTypeName, handlerId));
        subMetrics.RecordInvoke(elapsedMs);
    }

    /// <summary>
    ///     Records a subscription being added.
    ///     NOTE: Always tracks subscriber count regardless of IsEnabled,
    ///     since subscriptions happen at startup before the inspector is opened.
    /// </summary>
    public void RecordSubscription(string eventTypeName, int handlerId, string? source = null, int priority = 0)
    {
        // Always track subscriber count (happens at startup before inspector is enabled)
        var metrics = _eventMetrics.GetOrAdd(eventTypeName, _ =>
        {
            Interlocked.Increment(ref _eventMetricsVersion); // Invalidate cache
            return new EventTypeMetrics(eventTypeName);
        });
        metrics.IncrementSubscriberCount();

        string key = $"{eventTypeName}:{handlerId}";
        var subMetrics = _subscriptionMetrics.GetOrAdd(key, _ => new SubscriptionMetrics(eventTypeName, handlerId));
        subMetrics.Source = source;
        subMetrics.Priority = priority;
    }

    /// <summary>
    ///     Records a subscription being removed.
    ///     NOTE: Always tracks subscriber count regardless of IsEnabled.
    /// </summary>
    public void RecordUnsubscription(string eventTypeName, int handlerId)
    {
        // Always track subscriber count
        if (_eventMetrics.TryGetValue(eventTypeName, out var metrics))
        {
            metrics.DecrementSubscriberCount();
        }

        string key = $"{eventTypeName}:{handlerId}";
        _subscriptionMetrics.TryRemove(key, out _);
    }

    /// <summary>
    ///     Gets all event type metrics.
    /// </summary>
    public IReadOnlyCollection<EventTypeMetrics> GetAllEventMetrics()
    {
        // OPTIMIZATION: Cache results to avoid ToList() allocation on every frame
        // Only rebuild cache when metrics have changed
        if (_cachedEventMetrics == null || _cachedEventMetricsVersion != _eventMetricsVersion)
        {
            _cachedEventMetrics = _eventMetrics.Values.ToList();
            _cachedEventMetricsVersion = _eventMetricsVersion;
        }
        return _cachedEventMetrics;
    }

    /// <summary>
    ///     Gets metrics for a specific event type.
    /// </summary>
    public EventTypeMetrics? GetEventMetrics(string eventTypeName)
    {
        _eventMetrics.TryGetValue(eventTypeName, out var metrics);
        return metrics;
    }

    /// <summary>
    ///     Gets all subscription metrics for an event type.
    /// </summary>
    public IReadOnlyCollection<SubscriptionMetrics> GetSubscriptionMetrics(string eventTypeName)
    {
        return _subscriptionMetrics.Values
            .Where(s => s.EventTypeName == eventTypeName)
            .OrderByDescending(s => s.Priority)
            .ToList();
    }

    /// <summary>
    ///     Clears all collected metrics.
    /// </summary>
    public void Clear()
    {
        _eventMetrics.Clear();
        _subscriptionMetrics.Clear();
    }

    /// <summary>
    ///     Resets timing statistics while keeping subscriber counts.
    /// </summary>
    public void ResetTimings()
    {
        foreach (var metrics in _eventMetrics.Values)
        {
            metrics.ResetTimings();
        }
        foreach (var metrics in _subscriptionMetrics.Values)
        {
            metrics.ResetTimings();
        }
    }
}

/// <summary>
///     Performance metrics for a specific event type.
///     All times are stored in milliseconds for consistency with other panels.
/// </summary>
public class EventTypeMetrics
{
    private readonly object _lock = new();
    private long _totalPublishCount;
    private long _totalHandlerInvocations;
    private double _totalPublishTimeMs;
    private double _totalHandlerTimeMs;
    private double _maxPublishTimeMs;
    private double _maxHandlerTimeMs;
    private int _subscriberCount;

    public EventTypeMetrics(string eventTypeName)
    {
        EventTypeName = eventTypeName;
    }

    public string EventTypeName { get; }
    public long PublishCount => _totalPublishCount;
    public long HandlerInvocations => _totalHandlerInvocations;
    public int SubscriberCount => _subscriberCount;

    public double AveragePublishTimeMs =>
        _totalPublishCount > 0 ? _totalPublishTimeMs / _totalPublishCount : 0.0;

    public double MaxPublishTimeMs => _maxPublishTimeMs;

    public double AverageHandlerTimeMs =>
        _totalHandlerInvocations > 0 ? _totalHandlerTimeMs / _totalHandlerInvocations : 0.0;

    public double MaxHandlerTimeMs => _maxHandlerTimeMs;

    public void RecordPublish(double elapsedMs)
    {
        lock (_lock)
        {
            _totalPublishCount++;
            _totalPublishTimeMs += elapsedMs;
            if (elapsedMs > _maxPublishTimeMs)
                _maxPublishTimeMs = elapsedMs;
        }
    }

    public void RecordHandlerInvoke(double elapsedMs)
    {
        lock (_lock)
        {
            _totalHandlerInvocations++;
            _totalHandlerTimeMs += elapsedMs;
            if (elapsedMs > _maxHandlerTimeMs)
                _maxHandlerTimeMs = elapsedMs;
        }
    }

    public void IncrementSubscriberCount()
    {
        Interlocked.Increment(ref _subscriberCount);
    }

    public void DecrementSubscriberCount()
    {
        Interlocked.Decrement(ref _subscriberCount);
    }

    public void ResetTimings()
    {
        lock (_lock)
        {
            _totalPublishCount = 0;
            _totalHandlerInvocations = 0;
            _totalPublishTimeMs = 0;
            _totalHandlerTimeMs = 0;
            _maxPublishTimeMs = 0;
            _maxHandlerTimeMs = 0;
        }
    }
}

/// <summary>
///     Performance metrics for a specific subscription.
///     All times are stored in milliseconds for consistency with other panels.
/// </summary>
public class SubscriptionMetrics
{
    private readonly object _lock = new();
    private long _totalInvocations;
    private double _totalTimeMs;
    private double _maxTimeMs;

    public SubscriptionMetrics(string eventTypeName, int handlerId)
    {
        EventTypeName = eventTypeName;
        HandlerId = handlerId;
    }

    public string EventTypeName { get; }
    public int HandlerId { get; }
    public string? Source { get; set; }
    public int Priority { get; set; }

    public long InvocationCount => _totalInvocations;

    public double AverageTimeMs =>
        _totalInvocations > 0 ? _totalTimeMs / _totalInvocations : 0.0;

    public double MaxTimeMs => _maxTimeMs;

    public void RecordInvoke(double elapsedMs)
    {
        lock (_lock)
        {
            _totalInvocations++;
            _totalTimeMs += elapsedMs;
            if (elapsedMs > _maxTimeMs)
                _maxTimeMs = elapsedMs;
        }
    }

    public void ResetTimings()
    {
        lock (_lock)
        {
            _totalInvocations = 0;
            _totalTimeMs = 0;
            _maxTimeMs = 0;
        }
    }
}
