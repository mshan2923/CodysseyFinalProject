using UnityEngine;

namespace SensingWorld
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class GrabbableObject : MonoBehaviour
    {
        [Header("SensingWorld Events")]
        [SerializeField] private SensingWorldEventBusHost eventBusHost;

        [Header("Outline")]
        [SerializeField] private Renderer outlineRenderer;
        [SerializeField] private int outlineMaterialIndex;
        [SerializeField] private float outlineVisibleAlpha = 1f;

        public Rigidbody Body { get; private set; }

        public bool IsHeld { get; private set; }

        private Material outlineMaterial;
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();

            if (eventBusHost == null)
                eventBusHost = FindFirstObjectByType<SensingWorldEventBusHost>();

            outlineRenderer = GetComponent<MeshRenderer>();

            if (outlineRenderer != null)
            {
                Material[] materials = outlineRenderer.materials;
                if (outlineMaterialIndex >= 0 && outlineMaterialIndex < materials.Length)
                    outlineMaterial = materials[outlineMaterialIndex];
            }

            SetOutlineVisible(false);
        }

        private void OnCollisionEnter(Collision collision)
        {
            SensingWorldEntity targetEntity =
                collision.gameObject.GetComponentInParent<SensingWorldEntity>();

            if (targetEntity == null ||
                targetEntity.Kind != SensingWorldEntityKind.NPC ||
                eventBusHost == null)
                return;

            Vector3 contactPosition = collision.contactCount > 0
                ? collision.GetContact(0).point
                : targetEntity.transform.position;

            eventBusHost.Publish(new SensingWorldEvent(
                SensingWorldEventType.Hit,
                gameObject,
                targetEntity.gameObject,
                Time.time,
                contactPosition,
                targetEntity.gameObject.tag,
                Vector3.Distance(transform.position, targetEntity.transform.position),
                0f,
                0f));
        }

        internal void SetHeld(bool held)
        {
            IsHeld = held;
        }

        public void SetOutlineVisible(bool visible)
        {
            if (outlineMaterial == null || !outlineMaterial.HasProperty(ColorProperty))
                return;

                        

            Color color = outlineMaterial.GetColor(ColorProperty);
            color.a = visible || IsHeld? outlineVisibleAlpha : 0f;
            outlineMaterial.SetColor(ColorProperty, color);
        }
    }
}
