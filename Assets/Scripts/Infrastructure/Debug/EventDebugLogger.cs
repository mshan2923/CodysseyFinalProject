using System;
using Codyssey.Core.Events;
using Codyssey.Gameplay.Systems;
using UnityEngine;

namespace Codyssey.Infrastructure.Debugging
{
    /// <summary>Development-only presentation/debug subscriber.</summary>
    public sealed class EventDebugLogger : IDisposable
    {
        private readonly IDisposable damageSubscription;
        private readonly IDisposable rejectionSubscription;

        public EventDebugLogger(GameEventBus eventBus)
        {
            damageSubscription = eventBus.Subscribe<DamageApplied>(OnDamageApplied);
            rejectionSubscription = eventBus.Subscribe<RequestRejected>(OnRequestRejected);
        }

        private static void OnDamageApplied(DamageApplied gameEvent)
        {
            Debug.Log($"[Event] DamageApplied: {gameEvent.SourceId} -> {gameEvent.TargetId}, " +
                      $"amount={gameEvent.Amount}, remainingHealth={gameEvent.RemainingHealth}");
        }

        private static void OnRequestRejected(RequestRejected gameEvent)
        {
            Debug.LogWarning($"[Event] RequestRejected: {gameEvent.AttackerId} -> " +
                             $"{gameEvent.TargetId}, reason={gameEvent.Reason}");
        }

        public void Dispose()
        {
            damageSubscription.Dispose();
            rejectionSubscription.Dispose();
        }
    }
}
