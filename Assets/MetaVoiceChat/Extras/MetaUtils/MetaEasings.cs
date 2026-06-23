using UnityEngine;

namespace Metater
{
    // https://easings.net/
    public static class MetaEasings
    {
        public static float EaseOutElastic(float x)
        {
            const float c4 = (2 * Mathf.PI) / 3;
            if (x == 0) return 0;
            if (x == 1) return 1;
            return Mathf.Pow(2, -10 * x) * Mathf.Sin((x * 10 - 0.75f) * c4) + 1;
        }

        public static float EaseInBounce(float x)
        {
            return 1 - EaseOutBounce(1 - x);
        }

        public static float EaseOutBounce(float x)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (x < 1 / d1)
            {
                return n1 * x * x;
            }
            else if (x < 2 / d1)
            {
                return n1 * (x -= 1.5f / d1) * x + 0.75f;
            }
            else if (x < 2.5 / d1)
            {
                return n1 * (x -= 2.25f / d1) * x + 0.9375f;
            }
            else
            {
                return n1 * (x -= 2.625f / d1) * x + 0.984375f;
            }
        }

        public static float EaseInOutBounce(float x)
        {
            return x < 0.5f ? (1 - EaseOutBounce(1 - 2 * x)) / 2
                : (1 + EaseOutBounce(2 * x - 1)) / 2;
        }
    }
}