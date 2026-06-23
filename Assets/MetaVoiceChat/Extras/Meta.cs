using System;
using System.Collections;
using UnityEngine;

namespace Metater
{
    public static class Meta
    {
        public static double Time => UnityEngine.Time.timeAsDouble;
        public static double Realtime => UnityEngine.Time.unscaledTimeAsDouble;
        public static double FixedTime => UnityEngine.Time.fixedTimeAsDouble;
        public static double FixedRealtime => UnityEngine.Time.fixedUnscaledTimeAsDouble;

        public static readonly WaitForFixedUpdate WaitForFixedUpdate = new();
        public static readonly WaitForEndOfFrame WaitForEndOfFrame = new();
        // If you need more, do a WaitForSeconds dictionary cache
        public static readonly WaitForSeconds WaitForTenthSecond = new(0.1f);
        public static readonly WaitForSeconds WaitForHalfSecond = new(0.5f);
        public static readonly WaitForSeconds WaitForOneSecond = new(1f);

        /// <summary>
        /// UNITY_ASSERTIONS is the preprocessor directive equivalent of this
        /// Enabled in the editor and in development builds and disabled other times
        /// </summary>
        public static bool IsDebugBuild => Debug.isDebugBuild;

        public static float GetSinT(float periodSeconds, MetaInstant instant)
        {
            return (GetSin(periodSeconds, instant) + 1.0f) / 2.0f;
        }

        public static float GetSin(float periodSeconds, MetaInstant instant)
        {
            return (float)Math.Sin(2.0f * Math.PI * instant.TimeSeconds / periodSeconds);
        }

        public static float GetCosT(float periodSeconds, MetaInstant instant)
        {
            return (GetCos(periodSeconds, instant) + 1.0f) / 2.0f;
        }

        public static float GetCos(float periodSeconds, MetaInstant instant)
        {
            return (float)Math.Cos(2.0f * Math.PI * instant.TimeSeconds / periodSeconds);
        }

        //public static float GetNetworkedPerlinNoise1D(float offset, float frequency, float amplitude)
        //{
        //    double time = (NetworkTime.time + offset) * frequency;
        //    float t = (Mathf.PerlinNoise1D((float)time) * 2f) - 1f;
        //    return t * amplitude;
        //}

        public static int GetLayerIndex(LayerMask layerMask)
        {
            return (int)Math.Log(layerMask.value, 2);
        }

        public static IEnumerator CoDelay(Action action, int frames)
        {
            while (frames > 0)
            {
                frames--;
                yield return null;
            }

            action();
        }

        //public static IEnumerator CoPoll(Func<bool> isReady, Action onReady, int timeoutFrames = 2)
        //{
        //    while (timeoutFrames > 0)
        //    {
        //        timeoutFrames--;

        //        if (isReady())
        //        {
        //            onReady();
        //            yield break;
        //        }

        //        yield return null;
        //    }
        //}
    }
}