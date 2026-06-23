using UnityEngine;

namespace Metater
{
    public class AudioVolumeCurveTool : MonoBehaviour
    {
        public float minVolume = 0f;
        public float maxVolume = 1f;
        public float minDistance = 1f;
        public float smallDeltaDistance = 1f;
        public float largeDeltaDistance = 50f;
        public float deltaDistanceThreshold = 10f;
        [Range(0f, 10f)]
        public float coefficient = 1f;

        public float power = 1f;

        private void OnValidate()
        {
            Calculate();
        }

        [ContextMenu("Calculate")]
        public void Calculate()
        {
            if (!TryGetComponent<AudioSource>(out var audioSource))
            {
                return;
            }

            float maxDistance = audioSource.maxDistance;

            float GetDeltaDistance(float d)
            {
                return d < deltaDistanceThreshold ? smallDeltaDistance : largeDeltaDistance;
            }

            AnimationCurve curve = new();
            for (float d = 0; d <= maxDistance; d += GetDeltaDistance(d))
            {
                float value = coefficient * Mathf.Pow(d, -power);
                value = Mathf.Clamp(value, minVolume, maxVolume);

                if (d < minDistance)
                {
                    value = maxVolume;
                }

                curve.AddKey(d, value);
            }

            if (curve.keys.Length > 0)
            {
                var last = curve.keys[curve.keys.Length - 1];
                last.value = minVolume;
                curve.MoveKey(curve.keys.Length - 1, last);
            }

            audioSource.minDistance = 0;

            audioSource.rolloffMode = AudioRolloffMode.Custom;
            audioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curve);
        }
    }
}
