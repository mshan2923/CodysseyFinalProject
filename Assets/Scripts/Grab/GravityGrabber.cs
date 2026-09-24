using UnityEngine;
using UnityEngine.InputSystem;

namespace SensingWorld
{
    public sealed class GravityGrabber : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform holdPoint;
        [SerializeField] private SensingWorldEventBusHost eventBusHost;

        [Header("Grab")]
        [SerializeField] private float maxGrabDistance = 4f;
        [SerializeField] private float holdDistance = 2.5f;
        [SerializeField] private LayerMask grabbableLayers = ~0;
        [SerializeField] private Key grabKey = Key.E;

        [Header("Physics Follow")]
        [SerializeField] private float followStrength = 35f;
        [SerializeField] private float followDamping = 8f;
        [SerializeField] private float maxFollowSpeed = 15f;

        private GrabbableObject heldObject;
        private GrabbableObject pointedObject;
        private GameObject lookedTarget;

        private bool restoreGravity;
        private CollisionDetectionMode restoreCollisionDetectionMode;
        private RigidbodyInterpolation restoreInterpolation;

        private void Awake()
        {
            if (viewCamera == null)
                viewCamera = GetComponentInChildren<Camera>();

            if (holdPoint == null && viewCamera != null)
                holdPoint = viewCamera.transform;

            if (eventBusHost == null)
                eventBusHost = FindFirstObjectByType<SensingWorldEventBusHost>();
        }

        private void Update()
        {
            UpdateLookEvent();
            UpdatePointedOutline();

            if (Keyboard.current != null &&
                Keyboard.current[grabKey].wasPressedThisFrame)
            {
                if (heldObject == null)
                    TryGrab();
                else
                    Release();
            }
        }

        private void FixedUpdate()
        {
            if (heldObject == null)
                return;

            Rigidbody body = heldObject.Body;

            if (body == null)
                return;

            Vector3 targetPosition;

            if (holdPoint != null)
            {
                targetPosition = holdPoint.position;
            }
            else if (viewCamera != null)
            {
                targetPosition =
                    viewCamera.transform.position +
                    viewCamera.transform.forward * holdDistance;
            }
            else
            {
                return;
            }

            Vector3 positionError = targetPosition - body.position;

            Vector3 acceleration =
                positionError * followStrength -
                body.linearVelocity * followDamping;

            body.AddForce(acceleration, ForceMode.Acceleration);

            float maxSpeedSqr = maxFollowSpeed * maxFollowSpeed;

            if (body.linearVelocity.sqrMagnitude > maxSpeedSqr)
            {
                body.linearVelocity = Vector3.zero;
            }
        }

        private void TryGrab()
        {
            GrabbableObject grabbable = FindGrabbableInView();

            if (grabbable == null || grabbable.Body == null)
                return;

            heldObject = grabbable;

            Rigidbody body = heldObject.Body;

            restoreGravity = body.useGravity;
            restoreCollisionDetectionMode = body.collisionDetectionMode;
            restoreInterpolation = body.interpolation;

            body.useGravity = false;
            body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;

            body.interpolation =
                RigidbodyInterpolation.Interpolate;

            body.WakeUp();

            heldObject.SetHeld(true);
            heldObject.SetOutlineVisible(true);

            PublishEvent(
                SensingWorldEventType.ObjectPickedUp,
                heldObject.gameObject);
        }

        private GrabbableObject FindGrabbableInView()
        {
            if (viewCamera == null)
                return null;

            Ray ray = new Ray(
                viewCamera.transform.position,
                viewCamera.transform.forward);

            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    maxGrabDistance,
                    grabbableLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return null;
            }

            return hit.collider.GetComponentInParent<GrabbableObject>();
        }

        private GameObject FindLookTargetInView()
        {
            if (viewCamera == null)
                return null;

            Ray ray = new Ray(
                viewCamera.transform.position,
                viewCamera.transform.forward);

            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    maxGrabDistance,
                    grabbableLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return null;
            }

            SensingWorldEntity entity =
                hit.collider.GetComponentInParent<SensingWorldEntity>();

            if (entity != null)
                return entity.gameObject;

            GrabbableObject grabbable =
                hit.collider.GetComponentInParent<GrabbableObject>();

            if (grabbable != null)
                return grabbable.gameObject;

            Transform current = hit.collider.transform;

            while (current != null)
            {
                if (current.CompareTag("Interaction") ||
                    current.CompareTag("NPC"))
                {
                    return current.gameObject;
                }

                current = current.parent;
            }

            return null;
        }

        private void UpdateLookEvent()
        {
            GameObject nextLookTarget = FindLookTargetInView();

            if (nextLookTarget == lookedTarget)
                return;

            if (lookedTarget != null)
                PublishEvent(
                    SensingWorldEventType.TargetLookEnded,
                    lookedTarget);

            lookedTarget = nextLookTarget;

            if (lookedTarget != null)
                PublishEvent(
                    SensingWorldEventType.TargetLookStarted,
                    lookedTarget);
        }

        private void UpdatePointedOutline()
        {
            GrabbableObject nextPointedObject =
                FindGrabbableInView();

            if (nextPointedObject == pointedObject)
                return;

            if (pointedObject != null)
            {
                pointedObject.SetOutlineVisible(
                    pointedObject == heldObject);
            }

            pointedObject = nextPointedObject;

            if (pointedObject != null)
                pointedObject.SetOutlineVisible(true);
        }

        private void Release()
        {
            if (heldObject == null)
                return;

            Rigidbody body = heldObject.Body;

            body.useGravity = restoreGravity;
            body.collisionDetectionMode =
                restoreCollisionDetectionMode;
            body.interpolation = restoreInterpolation;

            body.WakeUp();

            heldObject.SetHeld(false);
            heldObject.SetOutlineVisible(
                heldObject == pointedObject);

            PublishEvent(
                SensingWorldEventType.ObjectDropped,
                heldObject.gameObject);

            heldObject = null;
        }

        private void PublishEvent(
            SensingWorldEventType type,
            GameObject target)
        {
            if (eventBusHost == null)
                return;

            Vector3 position =
                target != null
                    ? target.transform.position
                    : transform.position;

            string targetTag =
                target != null
                    ? target.tag
                    : string.Empty;

            eventBusHost.Publish(
                new SensingWorldEvent(
                    type,
                    gameObject,
                    target,
                    Time.time,
                    position,
                    targetTag,
                    0f,
                    0f,
                    0f));
        }

        private void OnDisable()
        {
            if (lookedTarget != null)
            {
                PublishEvent(
                    SensingWorldEventType.TargetLookEnded,
                    lookedTarget);

                lookedTarget = null;
            }

            if (pointedObject != null)
                pointedObject.SetOutlineVisible(false);

            Release();
        }

        private void OnDrawGizmosSelected()
        {
            if (viewCamera == null)
                return;

            Gizmos.color = Color.cyan;

            Gizmos.DrawRay(
                viewCamera.transform.position,
                viewCamera.transform.forward * maxGrabDistance);
        }
    }
}