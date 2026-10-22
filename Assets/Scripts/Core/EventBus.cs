using System;
using System.Collections.Generic;

namespace FracturedFrequencies.Core
{
    /// <summary>Marker interface for all events sent through the <see cref="EventBus{T}"/>.</summary>
    public interface IGameEvent { }

    /// <summary>
    /// Type-safe, allocation-free publish/subscribe bus. Events are structs.
    /// <code>
    /// EventBus&lt;PlayerDiedEvent&gt;.Subscribe(OnPlayerDied);   // in OnEnable
    /// EventBus&lt;PlayerDiedEvent&gt;.Unsubscribe(OnPlayerDied); // in OnDisable
    /// EventBus&lt;PlayerDiedEvent&gt;.Raise(new PlayerDiedEvent { ... });
    /// </code>
    /// </summary>
    public static class EventBus<T> where T : struct, IGameEvent
    {
        private static readonly List<Action<T>> Handlers = new();

        static EventBus()
        {
            EventBusRegistry.Register(Clear);
        }

        public static int HandlerCount => Handlers.Count;

        public static void Subscribe(Action<T> handler)
        {
            if (handler != null && !Handlers.Contains(handler))
                Handlers.Add(handler);
        }

        public static void Unsubscribe(Action<T> handler) => Handlers.Remove(handler);

        public static void Raise(T gameEvent)
        {
            // Iterate backwards so handlers may unsubscribe themselves while being invoked.
            for (int i = Handlers.Count - 1; i >= 0; i--)
            {
                if (i >= Handlers.Count) continue;

                try
                {
                    Handlers[i].Invoke(gameEvent);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }

        public static void Clear() => Handlers.Clear();
    }

    /// <summary>Keeps track of every EventBus type in use so all of them can be cleared at once.</summary>
    public static class EventBusRegistry
    {
        private static readonly List<Action> ClearActions = new();

        internal static void Register(Action clearAction) => ClearActions.Add(clearAction);

        public static void ClearAll()
        {
            foreach (var clear in ClearActions)
                clear();
        }
    }
}
