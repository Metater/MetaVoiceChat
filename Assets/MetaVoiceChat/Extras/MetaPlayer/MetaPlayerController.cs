using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Metater
{
    [RequireComponent(typeof(PlayerInput), typeof(MetaPlayerConfig))]
    public class MetaPlayerController : MonoBehaviour
    {
        private const float LookAccelerationSpeed = 20f; // tried 5 and 10, 7
        private const float GamepadLookScale = 360f;
        private const float GroundedVelocityEpsilon = 0.01f;

        public static MetaPlayerController Instance { get; set; }

        public readonly MetaPlayerBlocker blocker = new();

        [Header("References")]
        public Transform polarTransform;
        public Transform cameraTransform;
        public MeshRenderer[] shadowsOnlyWhenEnabled;

        [Header("Debug Blocker")]
        public bool debugBlockInput;
        public bool debugBlockSprint;
        public bool debugBlockJump;
        public bool debugBlockSimulation;
        public bool debugBlockMovement;

        [Header("Events")]
        public UnityEvent onJumped;
        // fall height, impact speed
        public UnityEvent<float, float> onLanded;

        [Header("Runtime")]
        public bool isMoving;
        public bool isSprinting;
        public bool isJumping;
        public Vector2 movementVector;
        public float velocityY;
        public bool isGrounded;
        public CollisionFlags collisionFlags;
        public float lookY;

        private MetaPlayerConfig config;
        private PlayerInput playerInput;
        private CharacterController characterController;

        private float jumpTimer; // Counts down
        private float fallTimer; // Counts down
        private float thoroughlyGroundedTimer; // Counts up

        private MetaInstant groundedInstant;
        private Vector3 lastHorizontalVelocity;

        private Vector2 look = Vector2.zero;
        private Vector2 gamepadLook = Vector2.zero;

        private readonly Queue<Vector3> additionalVelocityQueue = new();
        private readonly Queue<float> additionalSpeedQueue = new();
        private readonly List<MetaPlayerKnockbackEnvelope> knockbackEnvelopes = new();

        public bool IsActuallySprinting => isSprinting && blocker.CanSprint;
        public bool IsFalling => fallTimer <= 0;
        public bool IsAtTerminalVelocity => -velocityY >= config.terminalVelocity;
        public bool IsThoroughlyGrounded => thoroughlyGroundedTimer > config.timeToBeThoroughlyGrounded;
        public Vector3 CurrentHorizontalVelocity => new(characterController.velocity.x, 0.0f, characterController.velocity.z);
        public float CurrentHorizontalSpeed => CurrentHorizontalVelocity.magnitude;
        public float NominalMaxHorizontalSpeed => config.sprintSpeed + config.airborneSpeedBoost;
        public CharacterController CharacterController => characterController;
        public MetaPlayerConfig Config => config;


        private bool IsUsingKeyboardMouse => playerInput == null || playerInput.currentControlScheme == "Keyboard&Mouse";

        public void OnMove(InputValue value)
        {
            movementVector = value.Get<Vector2>();
        }

        public void OnSprint(InputValue value)
        {
            isSprinting = value.isPressed;
        }

        public void OnJump(InputValue value)
        {
            isJumping = value.isPressed;
        }

        public void OnLook(InputValue value)
        {
            look = value.Get<Vector2>();
            if (IsUsingKeyboardMouse)
            {
                Look(look * new Vector2(config.xScale, config.yScale));
            }
        }

        private void Awake()
        {
            config = GetComponent<MetaPlayerConfig>();
            playerInput = GetComponent<PlayerInput>();
        }

        private void OnEnable()
        {
            if (!TryGetComponent(out characterController))
            {
                characterController = gameObject.AddComponent<CharacterController>();
            }

            characterController.slopeLimit = config.slopeLimit;
            characterController.stepOffset = config.stepOffset;
            characterController.skinWidth = config.skinWidth;
            characterController.minMoveDistance = config.minMoveDistance;
            characterController.center = config.center;
            characterController.radius = config.radius;
            characterController.height = config.height;

            ResetState();

            if (cameraTransform != null && MetaCamera.Instance != null)
            {
                MetaCamera.Instance.transform.SetParent(cameraTransform, false);
                MetaCamera.Instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            foreach (var renderer in shadowsOnlyWhenEnabled)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }

            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"Multiple active {nameof(MetaPlayerController)} instances detected. There should only be one. The singleton initialization is being skipped.");
            }
            else
            {
                Instance = this;
            }
        }

        private void OnDisable()
        {
            if (TryGetComponent<CharacterController>(out var characterController))
            {
                Destroy(characterController);
            }

            ResetState();

            if (MetaCamera.Instance != null)
            {
                MetaCamera.Instance.transform.SetParent(null, true);
                MetaCamera.Instance.transform.localRotation = Quaternion.identity;
            }

            foreach (var renderer in shadowsOnlyWhenEnabled)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            {
                if (debugBlockInput)
                {
                    blocker.InputBlockers.Add(this);
                }
                else
                {
                    blocker.InputBlockers.Remove(this);
                }

                if (debugBlockSprint)
                {
                    blocker.SprintBlockers.Add(this);
                }
                else
                {
                    blocker.SprintBlockers.Remove(this);
                }

                if (debugBlockJump)
                {
                    blocker.JumpBlockers.Add(this);
                }
                else
                {
                    blocker.JumpBlockers.Remove(this);
                }

                if (debugBlockSimulation)
                {
                    blocker.SimulationBlockers.Add(this);
                }
                else
                {
                    blocker.SimulationBlockers.Remove(this);
                }

                if (debugBlockMovement)
                {
                    blocker.MoveBlockers.Add(this);
                }
                else
                {
                    blocker.MoveBlockers.Remove(this);
                }
            }

            if (!blocker.CanSimulate)
            {
                ResetState();
                return;
            }

            EnqueueAdditionalSpeed(1f);

            if (!IsUsingKeyboardMouse)
            {
                Vector2 scaledLook = look * GamepadLookScale;

                gamepadLook = Vector2.Lerp(gamepadLook, scaledLook, LookAccelerationSpeed * Time.deltaTime);

                Vector2 l = gamepadLook;

                Look(l);
            }
            else
            {
                gamepadLook = look;
            }

            ApplyKnockback();

            isGrounded = GroundCheck();
            if (isGrounded)
            {
                groundedInstant = MetaInstant.Time;
            }

            Gravity();
            bool jumpedThisFrame = Jump();
            Simulate();
            Move(jumpedThisFrame);
        }

        private void FixedUpdate()
        {
            if (!blocker.CanSimulate)
            {
                return;
            }

            if ((collisionFlags & CollisionFlags.Below) != 0)
            {
                float actualVelocityY = characterController.velocity.y;
                float simulatedVelocityY = velocityY;

                if (simulatedVelocityY < -config.staticVelocity && actualVelocityY < 0)
                {
                    float error = actualVelocityY - simulatedVelocityY;

                    if (error > config.fallVelocityMaxDiscrepancy)
                    {
                        velocityY *= config.fallVelocityDiscrepancyDecay;
                    }
                }
            }
        }

        public float GetTargetSpeed()
        {
            var targetSpeed = IsActuallySprinting ? config.sprintSpeed : config.walkSpeed;

            if (!isGrounded || isJumping)
            {
                targetSpeed += config.airborneSpeedBoost;
            }

            if (movementVector == Vector2.zero || !blocker.CanTakeInput || !blocker.CanMove)
            {
                targetSpeed = 0.0f;
            }

            return targetSpeed;
        }

        private Vector3 IntegrateAdditionalVelocity()
        {
            if (!blocker.CanSimulate)
            {
                return Vector3.zero;
            }

            Vector3 sum = Vector3.zero;
            while (additionalVelocityQueue.TryDequeue(out var velocity))
            {
                sum += velocity;
            }

            return sum * Time.deltaTime;
        }

        private float ProductAdditionalSpeed()
        {
            if (!blocker.CanSimulate)
            {
                return 1f;
            }

            float product = 1f;
            while (additionalSpeedQueue.TryDequeue(out var speedMultiplier))
            {
                product *= speedMultiplier;
            }

            // to allow for inverted controls, we allow the product to be negative
            if (Mathf.Abs(product) < config.additionalSpeedMultiplierRange.min)
            {
                product = Mathf.Sign(product) * config.additionalSpeedMultiplierRange.min;
            }

            return Mathf.Clamp(product, -config.additionalSpeedMultiplierRange.max, config.additionalSpeedMultiplierRange.max);
        }

        private void ApplyKnockback()
        {
            for (int i = knockbackEnvelopes.Count - 1; i >= 0; i--)
            {
                var envelope = knockbackEnvelopes[i];
                if (envelope.IsComplete)
                {
                    knockbackEnvelopes.RemoveAt(i);
                }
                else
                {
                    Vector3 velocity = envelope.GetVelocity();
                    additionalVelocityQueue.Enqueue(velocity);
                }
            }
        }

        public void ResetState()
        {
            isMoving = false;
            isSprinting = false;
            isJumping = false;
            movementVector = Vector2.zero;
            velocityY = 0.0f;
            isGrounded = false;
            collisionFlags = CollisionFlags.None;
            lookY = 0.0f;

            jumpTimer = config.timeBetweenJumps;
            fallTimer = config.timeToFall;
            thoroughlyGroundedTimer = 0;

            groundedInstant = MetaInstant.Time.Offset(-1);
            lastHorizontalVelocity = Vector3.zero;

            look = Vector2.zero;
            gamepadLook = Vector2.zero;

            ClearAdditionalVelocity();
            ClearAdditionalSpeed();
            ClearKnockback();

            blocker.ClearAllBlockers();
        }

        public void ResetJumpTimer()
        {
            jumpTimer = config.timeBetweenJumps;
        }

        public void ClearAdditionalVelocity()
        {
            additionalVelocityQueue.Clear();
        }

        public void ClearAdditionalSpeed()
        {
            additionalSpeedQueue.Clear();
        }

        public void ClearKnockback()
        {
            knockbackEnvelopes.Clear();
        }

        public void EnqueueAdditionalVelocity(Vector3 velocity)
        {
            additionalVelocityQueue.Enqueue(velocity);
        }

        public void EnqueueAdditionalSpeed(float speedMultiplier)
        {
            additionalSpeedQueue.Enqueue(speedMultiplier);
        }

        public void AddKnockback(MetaPlayerKnockbackEnvelope envelope)
        {
            knockbackEnvelopes.Add(envelope);
        }

        public float GetVelocityToReachHeight(float height)
        {
            return GetVelocityToReachHeight(height, config.gravity);
        }

        public static float GetVelocityToReachHeight(float height, float gravity)
        {
            return Mathf.Sqrt(height * -2f * gravity);
        }

        private void Gravity()
        {
            velocityY += config.gravity * Time.deltaTime;

            if (IsAtTerminalVelocity)
            {
                velocityY = -config.terminalVelocity;
            }
        }

        private bool Jump()
        {
            if (!isJumping || !blocker.CanTakeInput || !blocker.CanJump)
            {
                return false;
            }

            if ((isGrounded || groundedInstant.ElapsedSecondsF < config.coyoteTime) && jumpTimer <= 0)
            {
                velocityY = GetVelocityToReachHeight(config.jumpHeight);
                ResetJumpTimer();
                onJumped?.Invoke();

                return true;
            }

            return false;
        }

        private void Simulate()
        {
            if (isGrounded)
            {
                if (IsFalling)
                {
                    float fallHeight = velocityY * velocityY / (-2f * config.gravity);
                    onLanded?.Invoke(fallHeight, Mathf.Abs(velocityY));
                }

                if (velocityY < -config.staticVelocity && IsThoroughlyGrounded)
                {
                    velocityY = -config.staticVelocity;
                }

                fallTimer = config.timeToFall;

                thoroughlyGroundedTimer += Time.deltaTime;
            }
            else
            {
                if (fallTimer >= 0.0f)
                {
                    fallTimer -= Time.deltaTime;
                }

                thoroughlyGroundedTimer = 0;
            }

            if (jumpTimer >= 0.0f)
            {
                jumpTimer -= Time.deltaTime;
            }
        }

        private void Move(bool jumpedThisFrame)
        {
            Vector3 translationVector = Vector3.zero;

            bool isMoving = movementVector != Vector2.zero;
            if (!blocker.CanMove)
            {
                isMoving = false;
            }
            if (isMoving)
            {
                translationVector = polarTransform.right * movementVector.x + polarTransform.forward * movementVector.y;
            }

            Vector3 targetHorizontalVelocity = Vector3.zero;

            float speedMultiplier = ProductAdditionalSpeed();

            if (!blocker.CanTakeInput)
            {
                isMoving = false;
            }
            else
            {
                float targetSpeed = GetTargetSpeed();
                targetSpeed *= speedMultiplier;
                targetHorizontalVelocity = translationVector.normalized * targetSpeed;
            }

            this.isMoving = isMoving;

            Vector3 currentHorizontalVelocity = lastHorizontalVelocity; // CurrentHorizontalVelocity;
            Vector3 horizontalVelocityError = targetHorizontalVelocity - currentHorizontalVelocity;
            float acceleration = isGrounded ? config.acceleration : config.nonGroundedAcceleration;
            // if decelerating, apply deceleration factor
            if (Vector3.Dot(horizontalVelocityError, currentHorizontalVelocity) < 0)
            {
                acceleration *= config.decelerationFactor;
            }
            Vector3 horizontalVelocity = currentHorizontalVelocity + (acceleration * Time.deltaTime * horizontalVelocityError);
            if (jumpedThisFrame)
            {
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetHorizontalVelocity, config.jumpInstantHorizontalVelocityDelta);
            }

            Vector3 deltaXZ = horizontalVelocity * Time.deltaTime;

            Vector3 deltaY = new Vector3(0.0f, velocityY, 0.0f) * Time.deltaTime;
            Vector3 additionalDelta = IntegrateAdditionalVelocity();
            Vector3 motion = deltaXZ + deltaY + additionalDelta;
            collisionFlags = characterController.Move(motion);

            if (velocityY > 0 && characterController.velocity.y <= 0)
            {
                velocityY = 0;
            }

            lastHorizontalVelocity = horizontalVelocity;
        }

        private bool GroundCheck()
        {
            Vector3 position = new(transform.position.x, transform.position.y - config.groundCheckOffset, transform.position.z);
            bool groundCheck = Physics.CheckSphere(position, config.groundCheckRadius, config.GroundLayers, QueryTriggerInteraction.Ignore);

            if (!groundCheck)
            {
                bool bigGroundCheck = Physics.CheckSphere(position, config.groundCheckRadius * 1.25f, config.GroundLayers, QueryTriggerInteraction.Ignore);
                return bigGroundCheck && velocityY <= -config.staticVelocity && Mathf.Abs(characterController.velocity.y) < GroundedVelocityEpsilon;
            }

            return true;
        }

        public void Teleport(Vector3 position)
        {
            ResetState();

            characterController.enabled = false;
            characterController.transform.position = position;
            characterController.enabled = true;
        }

        private void Look(Vector2 look)
        {
            if (!blocker.CanTakeInput || config == null)
            {
                return;
            }

            if (config.yInverted)
            {
                look.y = -look.y;
            }

            // Don't multiply mouse inputs by Time.deltaTime
            float deltaTimeMultiplier = IsUsingKeyboardMouse ? 1.0f : Time.deltaTime;

            IncrementLookY(look.y * config.lookSensitivity * deltaTimeMultiplier);
            float lookDeltaX = look.x * config.lookSensitivity * deltaTimeMultiplier;

            polarTransform.Rotate(Vector3.up * lookDeltaX, Space.Self);
        }

        public void IncrementLookY(float increment)
        {
            float lookY = this.lookY;

            lookY += increment;

            lookY = ClampAngle(lookY, config.lookBottomClamp, config.lookTopClamp);
            cameraTransform.localRotation = Quaternion.Euler(lookY, 0, 0);

            this.lookY = lookY;
        }

        private static float ClampAngle(float degrees, float minDegrees, float maxDegrees)
        {
            if (degrees < -360f) degrees += 360f;
            if (degrees > 360f) degrees -= 360f;
            return Mathf.Clamp(degrees, minDegrees, maxDegrees);
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Color transparentGreen = new(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new(1.0f, 0.0f, 0.0f, 0.35f);

            Gizmos.color = isGrounded ? transparentGreen : transparentRed;

            Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - config.groundCheckOffset, transform.position.z), config.groundCheckRadius);
        }
    }
}