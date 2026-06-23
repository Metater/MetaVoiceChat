using UnityEngine;

namespace Metater
{
    public class MetaPlayerRepulsion : MonoBehaviour
    {
        [Header("References")]
        public Collider capsuleCollider;
        public Transform bottomTransform;
        public Transform topTransform;
        public LayerMask playerLayerMask;

        [Header("Repulsion Settings")]
        [Tooltip("Higher values = stronger falloff with distance")]
        [Range(0, 4)]
        public float repulsionExponent = 2f;

        [Tooltip("Base strength of repulsion force")]
        [Range(0, 10)]
        public float coefficient = 0.1f;

        [Tooltip("Maximum force multiplier to prevent extreme values")]
        [Range(1, 50)]
        public float maxMultiplier = 16f;

        [Tooltip("Minimum distance to prevent division by zero")]
        [Range(0.01f, 1f)]
        public float minDistance = 0.1f;

        [Tooltip("Radius for capsule overlap detection")]
        [Range(0.1f, 2f)]
        public float detectionRadius = 0.5f;

        private readonly Collider[] colliders = new Collider[100];
        private MetaPlayerController localPlayerController;
        private MetaPlayerController parentPlayerController;

        private void Awake()
        {
            // Validate required references
            if (capsuleCollider == null)
            {
                Debug.LogError($"[MetaPlayerRepulsion] capsuleCollider is not assigned on {gameObject.name}!", this);
                enabled = false;
                return;
            }

            if (bottomTransform == null || topTransform == null)
            {
                Debug.LogError($"[MetaPlayerRepulsion] bottomTransform or topTransform is not assigned on {gameObject.name}!", this);
                enabled = false;
                return;
            }

            // Get parent player controller once
            parentPlayerController = transform.parent.GetComponent<MetaPlayerController>();
            if (parentPlayerController == null)
            {
                Debug.LogError($"[MetaPlayerRepulsion] Parent does not have MetaPlayerController on {gameObject.name}!", this);
                enabled = false;
                return;
            }
        }

        private void Update()
        {
            // Lazy initialization of local player controller reference
            if (localPlayerController == null)
            {
                if (!MetaPlayerController.Instance)
                {
                    return;
                }

                localPlayerController = MetaPlayerController.Instance;
            }

            // CRITICAL FIX: Only apply repulsion if this is the local player
            // This prevents both players from applying opposing forces
            if (localPlayerController != parentPlayerController)
            {
                return;
            }

            // Don't apply repulsion if simulation is blocked
            if (!localPlayerController.blocker.CanSimulate)
            {
                return;
            }

            ApplyRepulsion();
        }

        private void ApplyRepulsion()
        {
            Vector3 selfCenter = capsuleCollider.bounds.center;

            // Detect overlapping player colliders
            int overlapCount = Physics.OverlapCapsuleNonAlloc(
                bottomTransform.position,
                topTransform.position,
                detectionRadius,
                colliders,
                playerLayerMask,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < overlapCount; i++)
            {
                Collider otherCollider = colliders[i];

                // Skip self and local player's character controller
                if (otherCollider == capsuleCollider ||
                    otherCollider == localPlayerController.CharacterController)
                {
                    continue;
                }

                Vector3 otherCenter = otherCollider.bounds.center;
                Vector3 direction = selfCenter - otherCenter; // FIXED: Push away from other player
                float distance = direction.magnitude;

                // Enforce minimum distance to prevent extreme forces
                if (distance < minDistance)
                {
                    distance = minDistance;
                }

                // Calculate repulsion direction
                Vector3 repulsionDirection;
                if (distance > 0.001f)
                {
                    repulsionDirection = direction.normalized;
                }
                else
                {
                    // If players are exactly overlapping, push in a random horizontal direction
                    repulsionDirection = new Vector3(
                        Random.Range(-1f, 1f),
                        0f,
                        Random.Range(-1f, 1f)
                    ).normalized;
                }

                // Calculate force multiplier based on distance
                float multiplier = CalculateRepulsionMultiplier(distance);

                // Apply force only to the local player
                Vector3 velocity = coefficient * multiplier * repulsionDirection;
                localPlayerController.EnqueueAdditionalVelocity(velocity);

                //print($"[MetaPlayerRepulsion] Applied repulsion to {localPlayerController.gameObject.name} from {otherCollider.gameObject.name}: " +
                //$"Distance={distance:F2}, Multiplier={multiplier:F2}, Velocity={velocity}");
            }
        }

        private float CalculateRepulsionMultiplier(float distance)
        {
            // Inverse power law: force = 1 / distance^exponent
            float multiplier = 1f / Mathf.Pow(distance, repulsionExponent);

            // Clamp to maximum to prevent extreme forces
            return Mathf.Min(multiplier, maxMultiplier);
        }
    }
}
