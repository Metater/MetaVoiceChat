using System;

namespace Metater
{
    [Serializable]
    public struct MetaRangeFloat
    {
        public float min;
        public float max;

        public readonly float Random => UnityEngine.Random.Range(min, max);
        public readonly float Lerp(float t) => UnityEngine.Mathf.Lerp(min, max, t);
        public readonly float LerpUnclamped(float t) => UnityEngine.Mathf.LerpUnclamped(min, max, t);
        public readonly float InverseLerp(float value) => UnityEngine.Mathf.InverseLerp(min, max, value);

        public MetaRangeFloat(float min, float max)
        {
            this.min = min;
            this.max = max;
        }
    }
}
