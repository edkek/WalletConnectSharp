namespace WalletConnectSharp.Common.Events;

/// <summary>
/// A mapping of eventIds to EventHandler objects. This uses a Dictionary as the backing datastore
/// </summary>
/// <typeparam name="TEventArgs">The type of EventHandler's argument to store</typeparam>
public class EventHandlerMap<TEventArgs> : IDisposable
{
    private readonly Dictionary<string, EventHandler<TEventArgs>> _mapping = new();

    private readonly object _mappingLock = new();

    private readonly EventHandler<TEventArgs> _beforeEventExecuted;

    /// <summary>
    /// Create a new EventHandlerMap with an initial EventHandler to append onto
    /// </summary>
    /// <param name="callbackBeforeExecuted">The initial EventHandler to use as the EventHandler.</param>
    public EventHandlerMap(EventHandler<TEventArgs> callbackBeforeExecuted = null)
    {
        if (callbackBeforeExecuted == null)
        {
            callbackBeforeExecuted = CallbackBeforeExecuted;
        }

        _beforeEventExecuted = callbackBeforeExecuted;
    }

    private static void CallbackBeforeExecuted(object sender, TEventArgs e)
    {
        // Default event handler used when no specific callback is provided. 
        // Currently, it doesn't perform any action when an event is triggered.
        // This is necessary to avoid null reference exceptions when no event handler is provided.
    }

    /// <summary>
    /// Get an EventHandler by its eventId. If the provided eventId does not exist, then the
    /// initial EventHandler is returned and tracking begins
    /// </summary>
    /// <param name="eventId">The eventId of the EventHandler</param>
    public EventHandler<TEventArgs> this[string eventId]
    {
        get
        {
            lock (_mappingLock)
            {
                _mapping.TryAdd(eventId, _beforeEventExecuted);

                return _mapping[eventId];
            }
        }
        set
        {
            lock (_mappingLock)
            {
                _mapping.Remove(eventId);
                _mapping.Add(eventId, value);
            }
        }
    }

    public void ListenOnce(string eventId, EventHandler<TEventArgs> eventHandler)
    {
        EventHandler<TEventArgs> internalHandler = null;
        internalHandler = (src, args) =>
        {
            this[eventId] -= internalHandler;
            eventHandler(src, args);
        };
        this[eventId] += internalHandler;
    }

    public bool TryGetValue(string eventName, out EventHandler<TEventArgs> handler)
    {
        lock (_mappingLock)
        {
            return _mapping.TryGetValue(eventName, out handler);
        }
    }

    /// <summary>
    /// Check if a given eventId has any EventHandlers registered yet.
    /// </summary>
    /// <param name="eventId">The eventId to check for</param>
    /// <returns>true if the eventId has any EventHandlers, false otherwise</returns>
    public bool Contains(string eventId)
    {
        lock (_mappingLock)
        {
            return _mapping.ContainsKey(eventId);
        }
    }

    /// <summary>
    /// Clear an eventId from the mapping
    /// </summary>
    /// <param name="eventId">The eventId to clear</param>
    public void Clear(string eventId)
    {
        lock (_mappingLock)
        {
            _mapping.Remove(eventId);
        }
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        lock (_mappingLock)
        {
            _mapping.Clear();
        }
    }
}
