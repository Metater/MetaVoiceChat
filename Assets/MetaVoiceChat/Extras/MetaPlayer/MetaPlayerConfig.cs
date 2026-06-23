using UnityEngine;

namespace Metater
{
    public class MetaPlayerConfig : MonoBehaviour
    {
        [Header("Simulation")]
        public float terminalVelocity = 53;
        public float staticVelocity = 1;
        public float fallVelocityMaxDiscrepancy = 4f;
        public float fallVelocityDiscrepancyDecay = 0.5f;

        [Header("Speeds")]
        public float walkSpeed = 5;
        public float sprintSpeed = 10;
        public float airborneSpeedBoost = 2.5f;
        public MetaRangeFloat additionalSpeedMultiplierRange = new(0.25f, 4f);

        [Header("Movement")]
        public float acceleration = 5f;
        public float nonGroundedAcceleration = 3f;
        public float decelerationFactor = 0.9f;
        public float jumpHeight = 1.5f; // 1.4f meters for a while
        public float coyoteTime = 0.1f;
        public float jumpInstantHorizontalVelocityDelta = 1f;

        [Header("Gravity")]
        public float gravity = -15f;

        [Header("Look")]
        public float lookSensitivity = 1;
        public float lookBottomClamp = -90;
        public float lookTopClamp = 90;
        public bool yInverted = true;
        public float xScale = 0.05f;
        public float yScale = 0.05f;

        [Header("Timers")]
        public float timeBetweenJumps = 0.3f;
        public float timeToFall = 0.15f;
        public float timeToBeThoroughlyGrounded = 0.1f;

        [Header("Ground Check")]
        public float groundCheckOffset = -0.3f;
        public float groundCheckRadius = 0.475f;
        public LayerMask invertedGroundLayers;

        [Header("Character Controller Settings")]
        [Range(0f, 180f)] public float slopeLimit = 45f;
        public float stepOffset = 0.25f;
        public float skinWidth = 0.0625f;
        public float minMoveDistance = 0f;
        public Vector3 center = new(0f, 1.0625f, 0f);
        public float radius = 0.5f;
        public float height = 2f;

        public LayerMask GroundLayers => ~invertedGroundLayers;
    }
}