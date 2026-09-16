using System.Collections.Generic;
using Codyssey.Core.Commands;
using Codyssey.Core.Events;
using Codyssey.Gameplay.Entities;
using Codyssey.Gameplay.Systems;
using Codyssey.Infrastructure.Debugging;
using Codyssey.Infrastructure.Input;
using Codyssey.Presentation.Audio;
using Codyssey.Presentation.UI;
using Codyssey.Presentation.VFX;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;
using GameplayEntityId = Codyssey.Gameplay.Entities.EntityId;

namespace Codyssey.Infrastructure
{
    /// <summary>
    /// Small composition root for the example. Add this component to a scene object
    /// and press Space in Play Mode to observe Request -> Logic -> Event -> Debug.
    /// </summary>
    public sealed class GameCompositionRoot : MonoBehaviour
    {
        [SerializeField] private int startingHealth = 100;
        [SerializeField] private float damagePerRequest = 10f;

        private EventDebugLogger debugLogger;
        private DamageUI damageUI;
        private HealthUI healthUI;
        private DamageVFX damageVFX;
        private DamageAudio damageAudio;

        public Slider slider;

        private void Awake()
        {
            var eventBus = new GameEventBus();
            var dispatcher = new RequestDispatcher();
            var entities = new Dictionary<GameplayEntityId, GameEntity>();

            var attackerObject = new GameObject("ExampleAttacker");
            attackerObject.transform.SetParent(transform);
            var attacker = attackerObject.AddComponent<GameEntity>();
            attacker.Initialize(startingHealth);
            entities.Add(attacker.Id, attacker);

            var targetObject = new GameObject("ExampleTarget");
            targetObject.transform.SetParent(transform);
            var target = targetObject.AddComponent<GameEntity>();
            target.Initialize(startingHealth);
            entities.Add(target.Id, target);

            var attackerId = attacker.Id;
            var combatSystem = new CombatSystem(eventBus, entities);
            dispatcher.Register<AttackRequest>(combatSystem);
            debugLogger = new EventDebugLogger(eventBus);

            damageUI = gameObject.GetComponent<DamageUI>() ?? gameObject.AddComponent<DamageUI>();
            damageUI.Initialize(eventBus);
            healthUI = gameObject.GetComponent<HealthUI>() ?? gameObject.AddComponent<HealthUI>();
            healthUI.Initialize(eventBus, target.Id, startingHealth, slider);
            damageVFX = gameObject.GetComponent<DamageVFX>() ?? gameObject.AddComponent<DamageVFX>();
            damageVFX.Initialize(eventBus);
            damageAudio = gameObject.GetComponent<DamageAudio>() ?? gameObject.AddComponent<DamageAudio>();
            damageAudio.Initialize(eventBus);
            Debug.Log($"[Composition] DamageApplied subscribers: " +
                      $"{eventBus.GetSubscriberCount<DamageApplied>()}");

            var input = gameObject.GetComponent<DemoInputController>() ??
                        gameObject.AddComponent<DemoInputController>();
            input.Initialize(dispatcher, attackerId, target.Id, damagePerRequest);

        }

        private void OnDestroy()
        {
            debugLogger?.Dispose();
        }
    }
}
