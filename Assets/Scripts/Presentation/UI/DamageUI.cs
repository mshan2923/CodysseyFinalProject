using System;
using Codyssey.Core.Events;
using Codyssey.Gameplay.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace Codyssey.Presentation.UI
{
    public sealed class DamageUI : MonoBehaviour
    {
        [SerializeField] private float damage;
        public float Damage => damage;

        private IDisposable subscription;

        public void Initialize(GameEventBus eventBus)
        {
            subscription?.Dispose();
            subscription = eventBus.Subscribe<DamageApplied>(OnDamageApplied);
        }

        private void OnDamageApplied(DamageApplied gameEvent)
        {

            damage = gameEvent.Amount;

            Debug.Log($"[UI] Damage text updated for {gameEvent.TargetId}");
        }

        private void OnDestroy() => subscription?.Dispose();
    }
}
