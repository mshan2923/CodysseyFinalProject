using System;
using Codyssey.Core.Events;
using Codyssey.Gameplay.Systems;
using UnityEngine;

namespace Codyssey.Presentation.VFX
{
    public sealed class DamageVFX : MonoBehaviour
    {
        [SerializeField] private GameObject hitEffect;
        private IDisposable subscription;

        public void Initialize(GameEventBus eventBus)
        {
            subscription?.Dispose();
            subscription = eventBus.Subscribe<DamageApplied>(OnDamageApplied);
        }

        private void OnDamageApplied(DamageApplied gameEvent)
        {
            if (hitEffect != null)
                hitEffect.SetActive(true);

            Debug.Log($"[VFX] Damage effect requested for {gameEvent.TargetId}");
        }

        private void OnDestroy() => subscription?.Dispose();
    }
}
