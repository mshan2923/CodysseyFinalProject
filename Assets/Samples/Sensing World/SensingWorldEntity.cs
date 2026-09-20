using System;
using System.Collections.Generic;
using UnityEngine;

namespace SensingWorld
{
    public enum SensingWorldEntityKind
    {
        Unknown,
        Player,
        NPC,
        Item,
        Environment
    }

    public enum SensingWorldEntityState
    {
        Unknown,
        Idle,
        Moving,
        Held,
        Attached,
        Interacting,
        Disabled
    }

    public sealed class SensingWorldEntity : MonoBehaviour
    {
        [SerializeField] private SensingWorldEntityKind kind = SensingWorldEntityKind.Unknown;
        [SerializeField] private SensingWorldEntityState currentState = SensingWorldEntityState.Unknown;
        [SerializeField] private List<SensingWorldAttribute> attributes = new();

        public string DisplayName = "Object";
        public SensingWorldEntityKind Kind => kind;
        public SensingWorldEntityState CurrentState => currentState;
        public IReadOnlyList<SensingWorldAttribute> Attributes => attributes;

        public void SetState(SensingWorldEntityState state)
        {
            currentState = state;
        }

        public void SetState(string state)
        {
            currentState = Enum.TryParse(state, true, out SensingWorldEntityState parsedState)
                ? parsedState
                : SensingWorldEntityState.Unknown;
        }
    }

    [Serializable]
    public sealed class SensingWorldAttribute
    {
        public string key;
        public string value;
    }
}
