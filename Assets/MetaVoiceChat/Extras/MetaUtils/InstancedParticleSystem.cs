using UnityEngine;

namespace Metater
{
    public class InstancedParticleSystem : MonoBehaviour
    {
        public float lifetime = 0f;
        public float destroyDelay = 0f;
        public ParticleSystem vfx;

        private float timer = 0f;
        private float destroyTimer = 0f;

        private void Update()
        {
            if (lifetime > 0.01f)
            {
                timer += Time.deltaTime;

                if (timer > lifetime)
                {
                    vfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);

                    destroyTimer += Time.deltaTime;
                }

                if (destroyTimer > destroyDelay)
                {
                    Destroy(gameObject);
                }
            }
            else
            {
                if (!vfx.isPlaying)
                {
                    Destroy(gameObject);
                }
            }
        }

        public InstancedParticleSystem Create(Vector3 position, Quaternion? rotation = null, Transform transform = null)
        {
            var instance = Instantiate(this, position, rotation == null ? Quaternion.identity : rotation.Value, transform);
            return instance;
        }
    }
}