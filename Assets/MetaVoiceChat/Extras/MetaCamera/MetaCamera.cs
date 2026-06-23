using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace Metater
{
    [DefaultExecutionOrder(50)]
    public class MetaCamera : MonoBehaviour
    {
        [Header("General")]
        public CinemachineCamera cinemachineCamera;
        public CinemachineBasicMultiChannelPerlin perlin;
        public float fovSpeed = 90f;

        [Header("Debugging")]
        public bool shouldDebugTargetFov = false;
        public float debugTargetFov = 69f;

        private static MetaCamera instance;
        public static MetaCamera Instance => instance;

        public float TargetFov { get; set; }

        private readonly List<CameraShakeEnvelope> envelopes = new();
        private readonly List<int> indiciesCache = new();

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                instance = this;
            }
        }

        private void Start()
        {
            if (cinemachineCamera != null)
            {
                TargetFov = cinemachineCamera.Lens.FieldOfView;
            }
        }

        private void Update()
        {
            if (cinemachineCamera == null || perlin == null) return;

            if (shouldDebugTargetFov)
            {
                TargetFov = debugTargetFov;
            }

            float currentFov = cinemachineCamera.Lens.FieldOfView;
            if (!Mathf.Approximately(currentFov, TargetFov))
            {
                float newFov = Mathf.MoveTowards(currentFov, TargetFov, fovSpeed * Time.deltaTime);
                cinemachineCamera.Lens.FieldOfView = newFov;
            }

            float value = IntegrateEnvelopes();
            perlin.AmplitudeGain = value;
        }

        public void AddEnvelope(CameraShakeEnvelope envelope)
        {
            envelopes.Add(envelope);
        }

        private float IntegrateEnvelopes()
        {
            indiciesCache.Clear();

            float sum = 0f;
            for (int i = 0; i < envelopes.Count; i++)
            {
                var envelope = envelopes[i];
                if (envelope.instant.IsOutsideCooldown(envelope.lifetime))
                {
                    indiciesCache.Add(i);
                    continue;
                }

                sum += envelopes[i].Evaluate(Time.time);
            }

            for (int i = indiciesCache.Count - 1; i >= 0; i--)
            {
                envelopes.RemoveAt(indiciesCache[i]);
            }

            return sum;
        }
    }
}