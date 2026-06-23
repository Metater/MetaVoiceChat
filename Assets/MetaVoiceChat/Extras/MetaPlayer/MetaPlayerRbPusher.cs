using UnityEngine;

#if MIRROR
using Mirror;
#endif

namespace Metater
{
#if MIRROR
    public class MetaPlayerRbPusher : NetworkBehaviour
#else
    public class MetaPlayerRbPusher : MonoBehaviour
#endif
    {
        [Range(0.5f, 10f)]
        public float strength = 1f;

        [Tooltip("Multiplier for velocity-based pushing. Higher values make faster collisions push harder.")]
        [Range(0f, 2f)]
        public float velocityInfluence = 0.5f;

        [Tooltip("Minimum relative velocity magnitude to apply push force")]
        [Range(0f, 1f)]
        public float minimumPushVelocity = 0.1f;

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Vector3 vector = hit.moveDirection.normalized;

            var pushable = hit.transform.GetComponentInParent<MetaPlayerPushable>();
            if (pushable != null)
            {
                CmdPush(pushable, vector, hit.controller.velocity, hit.point);
            }
        }

#if MIRROR
        [Command]
#endif
        private void CmdPush(MetaPlayerPushable pushable, Vector3 pushDirection, Vector3 playerVelocity, Vector3 hitPoint)
        {
            if (pushable == null || pushable.Rigidbody == null)
            {
                return;
            }

            // Calculate relative velocity
            Vector3 relativeVelocity = playerVelocity - pushable.Rigidbody.linearVelocity;

            // Only push if there's meaningful relative velocity
            if (relativeVelocity.magnitude < minimumPushVelocity)
            {
                return;
            }

            // Project relative velocity onto push direction to get the velocity component in the push direction
            float relativeSpeed = Vector3.Dot(relativeVelocity, pushDirection);

            // Only push if moving towards the object (positive relative speed)
            if (relativeSpeed > 0)
            {
                // Combine base strength with velocity-based force
                float forceMagnitude = strength * (1f + relativeSpeed * velocityInfluence);

                // Apply force at the hit point for more realistic torque
                pushable.Rigidbody.AddForceAtPosition(pushDirection * forceMagnitude, hitPoint, ForceMode.Impulse);
            }

            // Send impact sound with relative velocity magnitude
            if (pushable.TryGetComponent<ImpactSfx>(out var impactSfx))
            {
                impactSfx.ServerPlayerImpact(relativeVelocity.magnitude);
            }
        }
    }
}
