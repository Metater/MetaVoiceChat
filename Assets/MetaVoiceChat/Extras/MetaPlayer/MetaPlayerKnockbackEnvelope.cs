using UnityEngine;

namespace Metater
{
    public readonly struct MetaPlayerKnockbackEnvelope
    {
        public readonly Vector3 vector;
        public readonly float force;
        public readonly float duration;
        public readonly float power;

        private readonly MetaTimer timer;

        public readonly bool IsComplete => !timer.IsRunning;

        public MetaPlayerKnockbackEnvelope(Vector3 vector, float force, float duration, float power = 0.5f)
        {
            this.vector = vector;
            this.force = force;
            this.duration = duration;
            this.power = power;

            timer = MetaTimer.SetForTime(duration);
        }

        public Vector3 GetVelocity()
        {
            if (IsComplete)
            {
                return Vector3.zero;
            }

            float forceMultiplier = GetForceMultiplier(timer.Progress);
            Vector3 velocity = force * forceMultiplier * vector;
            return velocity;
        }

        private float GetForceMultiplier(float t)
        {
            return -Mathf.Pow(t, power) + 1f;
        }
    }
}