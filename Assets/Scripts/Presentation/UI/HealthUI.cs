using System;
using Codyssey.Core.Events;
using Codyssey.Gameplay.Entities;
using Codyssey.Gameplay.Systems;
using UnityEngine;
using UnityEngine.UI;
using GameplayEntityId = Codyssey.Gameplay.Entities.EntityId;

namespace Codyssey.Presentation.UI
{
    public sealed class HealthUI : MonoBehaviour
    {
        [SerializeField] private Slider healthSlider;
        [SerializeField] private float health;
        public float Health => health;

        private GameplayEntityId targetId;
        private IDisposable subscription;

        public void Initialize(GameEventBus eventBus, GameplayEntityId targetId, float maxHealth, Slider healthSlider)
        {
            subscription?.Dispose();
            this.targetId = targetId;
            health = maxHealth;

            if (healthSlider != null)
                this.healthSlider = healthSlider;
            if (healthSlider != null)
            {
                healthSlider.minValue = 0f;
                healthSlider.maxValue = maxHealth;
                healthSlider.value = maxHealth;
            }

            subscription = eventBus.Subscribe<DamageApplied>(OnDamageApplied);
        }

        private void OnDamageApplied(DamageApplied gameEvent)
        {
            if (gameEvent.TargetId != targetId)
                return;
            
            health = gameEvent.RemainingHealth;

            if (healthSlider != null)
                healthSlider.value = health;

            Debug.Log($"[UI] Health slider updated for {gameEvent.TargetId}: " +
                      $"{gameEvent.RemainingHealth:0}");
        }

        private void OnDestroy() => subscription?.Dispose();
    }
}
