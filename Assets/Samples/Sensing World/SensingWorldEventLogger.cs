using System;
using UnityEngine;

namespace SensingWorld
{
    public sealed class SensingWorldEventLogger : MonoBehaviour
    {
        [SerializeField] private SensingWorldEventBusHost eventBusHost;

        private IDisposable eventSubscription;

        private void Awake()
        {
            if (eventBusHost == null)
                eventBusHost = GetComponent<SensingWorldEventBusHost>();
        }

        private void OnEnable()
        {
            if (eventBusHost?.EventBus == null)
                return;

            eventSubscription = eventBusHost.EventBus.Subscribe<SensingWorldEvent>(OnSensingWorldEvent);
        }

        private static void OnSensingWorldEvent(SensingWorldEvent gameEvent)
        {
            // HPChanged가 추가되면 이 로그에서 Hit와 HP 변경의 시간 순서를 확인한다.
            string actorName =
                $"{gameEvent.ActorName} (InstanceID: {GetInstanceId(gameEvent.Actor)})";
            string targetName = gameEvent.Target != null
                ? $"{gameEvent.TargetName} (InstanceID: {gameEvent.Target.GetInstanceID()})"
                : gameEvent.TargetName;

            string channelName = gameEvent.Channel == SensingWorldEventChannel.Vision
                ? "Vision"
                : "WorldEvent";

            // Debug.Log(
            //     $"[SensingWorld/{channelName}] [{gameEvent.Timestamp:0.00}] {gameEvent.Category}: " +
            //     $"{actorName} -> {gameEvent.Type} -> {targetName}");
        }

        private static int GetInstanceId(GameObject entityObject)
        {
            return entityObject != null ? entityObject.GetInstanceID() : 0;
        }

        private void OnDisable()
        {
            eventSubscription?.Dispose();
            eventSubscription = null;
        }
    }
}
