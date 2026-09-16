using UnityEngine;
using GameplayEntityId = Codyssey.Gameplay.Entities.EntityId;

namespace Codyssey.Gameplay.Entities
{
    /// <summary>
    /// Scene-facing entity adapter. Rules stay in systems; this component owns only
    /// identity and the state that must be visible to Unity.
    /// </summary>
    public sealed class GameEntity : MonoBehaviour
    {
        [SerializeField] private int entityId;
        [SerializeField] private int startingHealth = 100;

        public GameplayEntityId Id => new(entityId);

        [SerializeField] private float health;
        public float Health { get => health; private set => health = value; }

        public void Initialize(int id, int health)
        {
            entityId = id;
            startingHealth = health;
            Health = health;
        }
        [System.Obsolete("Can't Use SaveData , NetworkSyncID")]
        public void Initialize(int health)
        {
            entityId = gameObject.GetInstanceID();
            startingHealth = health;
            Health = health;
        }

        public float ApplyDamage(float amount)
        {
            var applied = Mathf.Clamp(amount, 0, Health);
            Health -= applied;
            return applied;
        }

        private void Awake()
        {
            if (Health == 0)
                Health = startingHealth;
        }
    }
}
