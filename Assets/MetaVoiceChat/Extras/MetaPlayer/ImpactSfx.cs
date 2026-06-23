using UnityEngine;
#if MIRROR
using Mirror;
#endif

namespace Metater
{
#if MIRROR
public class ImpactSfx : NetworkBehaviour
#else
    public class ImpactSfx : MonoBehaviour
#endif
    {
        private const float PlayerImpactCooldown = 0.2f;

        [Range(0f, 1f)]
        public float masterVolume = 1f;
        public MetaRangeFloat impactSpeedRange = new(2f, 20f);
        public AudioClipPlayer clipPlayer;

        private double lastPlayerImpactTime = 0;

        private void OnCollisionEnter(Collision collision)
        {
#if MIRROR
        if (!isServer)
        {
            return;
        }
#endif

            float speed = collision.relativeVelocity.magnitude;
            ServerImpact(speed);
        }

#if MIRROR
    [Server]
#endif
        public void ServerPlayerImpact(float speed)
        {
            if (speed < impactSpeedRange.min)
            {
                return;
            }

            double time = Meta.Time;
            if (time - lastPlayerImpactTime < PlayerImpactCooldown)
            {
                return;
            }

            ServerImpact(speed);
            lastPlayerImpactTime = time;
        }

#if MIRROR
    [Server]
#endif
        public void ServerImpact(float speed)
        {
            if (speed < impactSpeedRange.min)
            {
                return;
            }

            float t = Mathf.InverseLerp(impactSpeedRange.min, impactSpeedRange.max, speed);
            RpcPlayImpactSfx(t * masterVolume);
        }

#if MIRROR
    [ClientRpc]
#endif
        private void RpcPlayImpactSfx(float volume)
        {
            clipPlayer.PlayRandom(volume);
        }
    }
}