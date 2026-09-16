using System;

namespace Codyssey.Gameplay.Entities
{
    [Serializable]
    public readonly struct EntityId : IEquatable<EntityId>
    {
        public readonly int Value;

        public EntityId(int value) => Value = value;

        public bool Equals(EntityId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EntityId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
        public static implicit operator int(EntityId id) => id.Value;
    }
}
