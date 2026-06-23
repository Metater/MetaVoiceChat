using UnityEngine;
using UnityEngine.InputSystem;
#if MIRROR
using Mirror;
#endif

namespace Metater
{
#if MIRROR
    public class MetaPlayer : NetworkBehaviour
#else
    public class MetaPlayer : MonoBehaviour
#endif
    {
        [Header("References")]
        public MetaPlayerController controller;
        public MetaPlayerFeetAudio feetAudio;

#if MIRROR
        [SyncVar]
#endif
        public float footstepsProgress = 0;

        [Header("Walk & Run Settings")]
        public float walkPeriod;
        public float runPeriod;
        public float immediateNoiseCooldown;

        [Header("Land Camera Shake Settings")]
        public bool landCameraShakeEnabled = true;
        public MetaRangeFloat landCameraShakeImpactSpeedRange = new(7f, 49f);
        public MetaRangeFloat landCameraShakeAmplitudeRange = new(0.5f, 2.5f);

        private float localTimer = 0;
        private bool localWasMovingOnGroundLastFrame = false;
        private MetaInstant localLastImmediateNoiseInstant;

#if MIRROR
        public override void OnStartLocalPlayer()
#else
        private void OnEnable()
#endif
        {
            if (TryGetComponent<PlayerInput>(out var input))
            {
                input.enabled = true;
            }

            if (controller == null)
            {
                return;
            }

            controller.enabled = true;
            controller.onJumped.AddListener(Jump);
            controller.onLanded.AddListener(Land);
        }

#if MIRROR
        public override void OnStopLocalPlayer()
#else
        private void OnDisable()
#endif  
        {
            if (TryGetComponent<PlayerInput>(out var input))
            {
                input.enabled = false;
            }

            if (controller == null)
            {
                return;
            }

            controller.enabled = false;
            controller.onJumped.RemoveListener(Jump);
            controller.onLanded.RemoveListener(Land);
        }

        private void Update()
        {
#if MIRROR
            if (!isLocalPlayer || controller == null || feetAudio == null)
            {
                return;
            }
#else
            if (controller == null || feetAudio == null)
            {
                return;
            }
#endif

            if (!controller.isGrounded || !controller.isMoving || !controller.enabled)
            {
                localTimer = 0;
                footstepsProgress = 0;
                localWasMovingOnGroundLastFrame = false;
                return;
            }

            float speed = controller.CurrentHorizontalSpeed;
            float t = speed / controller.GetTargetSpeed();
            var range = controller.Config.additionalSpeedMultiplierRange;
            t = Mathf.Clamp(t, range.min, range.max);

            float scaledDeltaTime = t * Time.deltaTime;
            localTimer += scaledDeltaTime;

            bool isWalking = !controller.IsActuallySprinting;
            if (isWalking)
            {
                if (localTimer > walkPeriod)
                {
                    localTimer -= walkPeriod;
                    Walk();
                }
                else if (!localWasMovingOnGroundLastFrame && localLastImmediateNoiseInstant.IsOutsideCooldown(immediateNoiseCooldown))
                {
                    localLastImmediateNoiseInstant = MetaInstant.Time;

                    Walk();
                }

                footstepsProgress = Mathf.Clamp01(localTimer / walkPeriod);
            }
            else
            {
                if (localTimer > runPeriod)
                {
                    localTimer -= runPeriod;
                    Run();
                }
                else if (!localWasMovingOnGroundLastFrame && localLastImmediateNoiseInstant.IsOutsideCooldown(immediateNoiseCooldown))
                {
                    localLastImmediateNoiseInstant = MetaInstant.Time;

                    Run();
                }

                footstepsProgress = Mathf.Clamp01(localTimer / runPeriod);
            }

            localWasMovingOnGroundLastFrame = true;
        }

        #region Walk
        private void Walk()
        {
            CmdWalk();
            SharedWalk();
        }

#if MIRROR
        [Command]
#endif
        private void CmdWalk()
        {
            RpcWalk();
        }

#if MIRROR
        [ClientRpc(includeOwner = false)]
#endif
        private void RpcWalk()
        {
            SharedWalk();
        }

        private void SharedWalk()
        {
            if (feetAudio != null)
                feetAudio.WalkSfx();
        }
        #endregion

        #region Run
        private void Run()
        {
            CmdRun();
            SharedRun();
        }

#if MIRROR
        [Command]
#endif
        private void CmdRun()
        {
            RpcRun();
        }

#if MIRROR
        [ClientRpc(includeOwner = false)]
#endif
        private void RpcRun()
        {
            SharedRun();
        }

        private void SharedRun()
        {
            if (feetAudio != null)
                feetAudio.RunSfx();
        }
        #endregion

        #region Jump
        private void Jump()
        {
            Quaternion rotation = controller.polarTransform.rotation;

            CmdJump(rotation);
            SharedJump(rotation);
        }

#if MIRROR
        [Command]
#endif
        private void CmdJump(Quaternion rotation)
        {
            RpcJump(rotation);
        }

#if MIRROR
        [ClientRpc(includeOwner = false)]
#endif
        private void RpcJump(Quaternion rotation)
        {
            SharedJump(rotation);
        }

        private void SharedJump(Quaternion rotation)
        {
            if (feetAudio != null)
                feetAudio.JumpSfx();
        }
        #endregion

        #region Land
        private void Land(float fallHeight, float impactSpeed)
        {
            if (MetaCamera.Instance && landCameraShakeEnabled)
            {
                float t = landCameraShakeImpactSpeedRange.InverseLerp(impactSpeed);
                float amplitude = landCameraShakeAmplitudeRange.Lerp(t);
                MetaCamera.Instance.AddEnvelope(new CameraShakeEnvelope(10f, amplitude, 0.1f, 0.1f, () => 1f));
            }

            CmdLand(impactSpeed);
            SharedLand(impactSpeed);
        }

#if MIRROR
        [Command]
#endif
        private void CmdLand(float speed)
        {
            RpcLand(speed);
        }

#if MIRROR
        [ClientRpc(includeOwner = false)]
#endif
        private void RpcLand(float speed)
        {
            SharedLand(speed);
        }

        private void SharedLand(float impactSpeed)
        {
            if (feetAudio != null)
                feetAudio.LandSfx(impactSpeed);
        }
        #endregion
    }
}
