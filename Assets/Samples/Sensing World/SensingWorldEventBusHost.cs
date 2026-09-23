using Codyssey.Core.Events;
using UnityEngine;

namespace SensingWorld
{
    public sealed class SensingWorldEventBusHost : MonoBehaviour
    {
        public GameEventBus EventBus { get; private set; }

        private void Awake()
        {
            EventBus = new GameEventBus();
        }

        public void Publish(SensingWorldEvent gameEvent)
        {
            
            // Debug.Log($"{gameEvent.Actor} -> {gameEvent.Target}");

            EventBus?.Publish(gameEvent);
        }
    }
}
