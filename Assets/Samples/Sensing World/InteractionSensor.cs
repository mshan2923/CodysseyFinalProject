using System.Collections.Generic;
using UnityEngine;

namespace SensingWorld
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class InteractionSensor : MonoBehaviour
    {
        [Header("Target Tags")]
        [SerializeField] private List<string> targetTags = new() { "Interaction", "NPC" };

        [Header("Detection")]
        [SerializeField] private float detectionRadius = 5f;
        [SerializeField, Range(0f, 1f)] private float distanceWeight = 0.5f;
        [SerializeField, Range(0f, 1f)] private float facingWeight = 0.5f;
        [SerializeField, Range(-1f, 1f)] private float minimumFacingDot;
        [SerializeField] private SensingWorldEventBusHost eventBusHost;

        private readonly Dictionary<GameObject, int> colliderCounts = new();
        private readonly Dictionary<GameObject, float> currentScores = new();
        private SphereCollider sensorCollider;

        private void Awake()
        {
            sensorCollider = GetComponent<SphereCollider>();
            sensorCollider.isTrigger = true;
            sensorCollider.radius = detectionRadius;

            if (eventBusHost == null)
                eventBusHost = FindFirstObjectByType<SensingWorldEventBusHost>();
        }

        private void OnTriggerEnter(Collider other)
        {
            GameObject target = FindTaggedTarget(other);
            if (target == null)
                return;


            if (colliderCounts.TryGetValue(target, out int count))
            {
                colliderCounts[target] = count + 1;
                return;
            }

            colliderCounts.Add(target, 1);
            float score = CalculateScore(target, out float distance, out float facingDot);
            currentScores[target] = score;
            PublishEvent(SensingWorldEventType.TargetVisible, target, distance, facingDot, score);
        }

        private void OnTriggerExit(Collider other)
        {
            GameObject target = FindTaggedTarget(other);
            if (target == null || !colliderCounts.TryGetValue(target, out int count))
                return;

            if (count > 1)
            {
                colliderCounts[target] = count - 1;
                return;
            }

            colliderCounts.Remove(target);
            currentScores.Remove(target);
            PublishEvent(SensingWorldEventType.TargetLost, target, 0f, 0f, 0f);
        }

        public bool TryGetScore(GameObject target, out float score)
        {
            return TryGetCurrentData(target, out _, out _, out score);
        }

        public bool TryGetCurrentData(
            GameObject target,
            out float distance,
            out float facingDot,
            out float score)
        {
            distance = 0f;
            facingDot = 0f;
            score = 0f;

            if (target == null || !currentScores.ContainsKey(target))
                return false;

            score = CalculateScore(target, out distance, out facingDot);
            currentScores[target] = score;
            return true;
        }

        private GameObject FindTaggedTarget(Collider other)
        {
            GameObject target = FindTaggedTargetInHierarchy(other.transform);
            if (target != null)
                return target;

            Rigidbody attachedRigidbody = other.attachedRigidbody;
            if (attachedRigidbody != null && attachedRigidbody.transform != other.transform)
            {
                target = FindTaggedTargetInHierarchy(attachedRigidbody.transform);
                if (target != null)
                    return target;
            }

            return null;
        }

        private GameObject FindTaggedTargetInHierarchy(Transform start)
        {
            Transform current = start;
            while (current != null)
            {
                if (targetTags.Contains(current.tag))
                    return current.gameObject;

                current = current.parent;
            }

            return null;
        }

        private float CalculateScore(GameObject target, out float distance, out float facingDot)
        {
            Vector3 toTarget = target.transform.position - transform.position;
            distance = toTarget.magnitude;
            Vector3 direction = distance > 0.001f ? toTarget / distance : transform.forward;
            facingDot = Vector3.Dot(transform.forward, direction);

            float distanceScore = 1f - Mathf.Clamp01(distance / detectionRadius);
            float facingScore = Mathf.InverseLerp(minimumFacingDot, 1f, facingDot);
            float totalWeight = distanceWeight + facingWeight;

            return totalWeight > 0f
                ? (distanceScore * distanceWeight + facingScore * facingWeight) / totalWeight
                : 0f;
        }

        private void PublishEvent(
            SensingWorldEventType type,
            GameObject target,
            float distance,
            float facingDot,
            float score)
        {
            if (eventBusHost == null)
                return;

            eventBusHost.Publish(new SensingWorldEvent(
                type,
                gameObject,
                target,
                Time.time,
                target.transform.position,
                target.tag,
                distance,
                facingDot,
                score));
        }
    }
}
