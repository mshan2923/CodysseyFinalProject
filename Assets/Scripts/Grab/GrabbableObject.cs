using System.Collections.Generic;
using System;
using UnityEngine;

namespace SensingWorld
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class GrabbableObject : MonoBehaviour
    {
        [Header("SensingWorld Events")]
        [SerializeField] private SensingWorldEventBusHost eventBusHost;


        public float TouchMaxPower = 1f;

        public Rigidbody Body { get; private set; }

        public bool IsHeld { get; private set; }

        internal event Action<Collision> CollisionEntered;

        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private readonly Dictionary<GameObject, int> collisionCounts = new();

        private void OnDisable()
        {
            collisionCounts.Clear();
        }

        private uint outlineBit;
        private Renderer[] renderers;

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            if (eventBusHost == null)
                eventBusHost = FindFirstObjectByType<SensingWorldEventBusHost>();

            outlineBit = RenderingLayerMask.GetMask("Outline");

            renderers = GetComponentsInChildren<Renderer>();
            SetOutlineVisible(false);
        }

        public void SetOutlineVisible(bool visible)
        {
            bool on = visible || IsHeld;
            foreach (Renderer r in renderers)
            {
                if (on) r.renderingLayerMask |= outlineBit;
                else r.renderingLayerMask &= ~outlineBit;
            }
        }


        private void OnCollisionEnter(Collision collision)
        {
            CollisionEntered?.Invoke(collision);

            SensingWorldEntity targetEntity =
                collision.gameObject.GetComponentInParent<SensingWorldEntity>();

            // Rigidbody body = collision.collider.attachedRigidbody;

            if (targetEntity == null ||
                targetEntity.Kind != SensingWorldEntityKind.NPC)
                return;

            GameObject targetObject = targetEntity.gameObject;
            collisionCounts.TryGetValue(targetObject, out int collisionCount);
            collisionCounts[targetObject] = collisionCount + 1;

            if (collisionCount > 0 || eventBusHost == null)
                return;

            Vector3 contactPosition = collision.contactCount > 0
                ? collision.GetContact(0).point
                : targetEntity.transform.position;

            // float velocity = body != null ? body.linearVelocity.magnitude : 0f;
            float velocity = collision.relativeVelocity.magnitude;

            eventBusHost.Publish(new SensingWorldEvent(
                velocity > TouchMaxPower ? SensingWorldEventType.Hit : SensingWorldEventType.Touch,
                gameObject,
                targetEntity.gameObject,
                Time.time,
                contactPosition,
                targetEntity.gameObject.tag,
                Vector3.Distance(transform.position, targetEntity.transform.position),
                0f,
                0f));
        }

        private void OnCollisionExit(Collision collision)
        {
            SensingWorldEntity targetEntity =
                collision.gameObject.GetComponentInParent<SensingWorldEntity>();

            if (targetEntity == null ||
                targetEntity.Kind != SensingWorldEntityKind.NPC)
                return;

            GameObject targetObject = targetEntity.gameObject;
            if (!collisionCounts.TryGetValue(targetObject, out int collisionCount))
                return;

            if (collisionCount <= 1)
                collisionCounts.Remove(targetObject);
            else
                collisionCounts[targetObject] = collisionCount - 1;
        }

        internal void SetHeld(bool held)
        {
            IsHeld = held;
        }

    }
}
