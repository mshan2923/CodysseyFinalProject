using System;
using Codyssey.Core.Events;
using Codyssey.Gameplay.Systems;
using UnityEngine;

namespace Codyssey.Presentation.Damage
{
    /// <summary>
    /// Presentation-side example. It observes gameplay results without referencing
    /// CombatSystem or changing gameplay state.
    /// </summary>
    public sealed class DamageAppliedPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject eventIndicator;

        private IDisposable damageSubscription;

        public void Initialize(GameEventBus eventBus)
        {
            damageSubscription?.Dispose();
            damageSubscription = eventBus.Subscribe<DamageApplied>(OnDamageApplied);

            Debug.Log("Initialize");
        }

        private void OnDamageApplied(DamageApplied gameEvent)
        {
            Debug.Log($"[Presentation] Damage feedback for entity {gameEvent.TargetId}: " +
                      $"-{gameEvent.Amount} HP");

            if (eventIndicator != null)
                eventIndicator.SetActive(true);
        }

        private void OnDestroy()
        {
            damageSubscription?.Dispose();
        }
    }
}
