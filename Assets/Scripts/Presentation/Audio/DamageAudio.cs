using System;
using Codyssey.Core.Events;
using Codyssey.Gameplay.Systems;
using UnityEngine;

namespace Codyssey.Presentation.Audio
{
    public sealed class DamageAudio : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip damageClip;
        private IDisposable subscription;

        public void Initialize(GameEventBus eventBus)
        {
            subscription?.Dispose();
            subscription = eventBus.Subscribe<DamageApplied>(OnDamageApplied);
        }

        private void OnDamageApplied(DamageApplied gameEvent)
        {
            if (audioSource != null && damageClip != null)
                audioSource.PlayOneShot(damageClip);

            Debug.Log($"[Audio] Damage sound requested for {gameEvent.TargetId}");
        }

        private void OnDestroy() => subscription?.Dispose();
    }
}
