using System;

namespace Metater
{
    [Serializable]
    public struct MetaRangeInt
    {
        public int minInclusive;
        public int maxInclusive;

        public readonly int Random => UnityEngine.Random.Range(minInclusive, maxInclusive + 1);
        public readonly int LerpUnclamped(int numerator, int denominator) =>
            minInclusive + (((maxInclusive - minInclusive) * numerator) / denominator);

        public MetaRangeInt(int minInclusive, int maxInclusive)
        {
            this.minInclusive = minInclusive;
            this.maxInclusive = maxInclusive;
        }
    }
}
