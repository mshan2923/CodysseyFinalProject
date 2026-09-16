using System;
using System.Collections.Generic;

namespace Codyssey.Core.Events
{
    /// <summary>
    /// Small, instance-based event bus. The owner of the bus controls its lifetime;
    /// subscribers receive an IDisposable so Unity lifecycle cleanup is explicit.
    /// </summary>
    public sealed class GameEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> subscribers = new();

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IGameEvent
        {
            if (!subscribers.TryGetValue(typeof(TEvent), out var handlers))
            {
                handlers = new List<Delegate>();
                subscribers.Add(typeof(TEvent), handlers);
            }

            handlers.Add(handler);
            return new Subscription(() => Unsubscribe(handler));
        }

        public void Publish<TEvent>(TEvent gameEvent) where TEvent : struct, IGameEvent
        {
            if (!subscribers.TryGetValue(typeof(TEvent), out var handlers))
                return;

            // Copy so a subscriber may safely unsubscribe while handling an event.
            var snapshot = handlers.ToArray();
            foreach (var handler in snapshot)
                ((Action<TEvent>)handler)(gameEvent);
        }

        public int GetSubscriberCount<TEvent>() where TEvent : struct, IGameEvent
        {
            return subscribers.TryGetValue(typeof(TEvent), out var handlers)
                ? handlers.Count
                : 0;
        }

        private void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IGameEvent
        {
            if (!subscribers.TryGetValue(typeof(TEvent), out var handlers))
                return;

            handlers.Remove(handler);
            if (handlers.Count == 0)
                subscribers.Remove(typeof(TEvent));
        }

        private sealed class Subscription : IDisposable
        {
            private Action dispose;

            public Subscription(Action dispose) => this.dispose = dispose;

            public void Dispose()
            {
                dispose?.Invoke();
                dispose = null;
            }
        }
    }
}
