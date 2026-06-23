using System;
using UnityEngine;

namespace Metater
{
    public readonly struct CameraShakeEnvelope
    {
        public readonly float lifetime;
        public readonly float maximumAmplitude;
        public readonly float attackTime;
        public readonly float decayRate;
        public readonly Func<float> coefficientFunction;

        public readonly MetaInstant instant;

        public CameraShakeEnvelope(float lifetime, float maximumAmplitude, float attackTime, float decayRate, Func<float> coefficientFunction)
        {
            this.lifetime = lifetime;
            this.maximumAmplitude = maximumAmplitude;
            this.attackTime = attackTime;
            this.decayRate = decayRate;
            this.coefficientFunction = coefficientFunction;

            instant = MetaInstant.Time;
        }

        // https://www.desmos.com/calculator/8ge9pombtg
        public readonly float Evaluate(double time)
        {
            float t = (float)(time - instant.TimeSeconds);

            float ratio = t / attackTime;

            float exponentA = 1f - ratio;
            float exponentB = -decayRate * (t - attackTime);

            float value = maximumAmplitude * ratio * Mathf.Exp(exponentA) * Mathf.Exp(exponentB);
            if (coefficientFunction != null)
            {
                value *= coefficientFunction();
            }
            return value;
        }
    }
}
