using Codyssey.Core.Events;
using SensingWorld;
using UnityEngine;

public class CatEventHost : MonoBehaviour
{       
        public GameEventBus EventBus { get; private set; }

        private void Awake()
        {
            EventBus = new GameEventBus();
        }

        public void Publish(SensingWorldEvent gameEvent)
        {
            EventBus?.Publish(gameEvent);
        }
}
