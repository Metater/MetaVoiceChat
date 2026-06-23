using UnityEngine;

namespace Metater
{
    public class PlayerAnimSquashAndStretch : MonoBehaviour
    {
        private const float CylinderVolume = Mathf.PI * 0.5f * 0.5f * 2f; // pi * r^2 * h

        [Header("General")]
        public MetaPlayer player;
        public float minHeight;
        public float maxHeight;
        public Transform offsetTransform;
        public float footstepsMultiplier;

        [Header("Smooth Damp")]
        public float smoothTime;
        public float maxSpeed;
        private float currentVelocity = 0;

        private bool isInit = false;
        private Vector3 lastPosition;

        public float HeightBias { get; set; } = 0f;

        private void LateUpdate()
        {
            if (!isInit)
            {
                isInit = true;
                lastPosition = transform.position;
            }

            var velocity = (transform.position - lastPosition) / Time.deltaTime;

            lastPosition = transform.position;

            float currentHeight = offsetTransform.transform.localScale.y * 2f;
            float targetHeight = GetHeight(velocity.y);
            float height = Mathf.SmoothDamp(currentHeight, targetHeight, ref currentVelocity, smoothTime, maxSpeed);
            height += HeightBias;

            height = Mathf.Clamp(height, .5f, 3f);

            if (float.IsNaN(height))
            {
                height = 2f; // Fallback to a default value if NaN occurs
            }

            float yScale = height / 2f;
            float radius = GetRadius(height);
            float xzScale = radius * 2f;
            offsetTransform.transform.localScale = new(xzScale, yScale, xzScale);
        }

        private float GetHeight(float velocity)
        {
            float footstepsT = player.footstepsProgress;
            if (footstepsT > 0.5f)
            {
                footstepsT -= 1f;
            }

            float t = Mathf.InverseLerp(6f, -6f, velocity);
            t += footstepsT * footstepsMultiplier;

            return Mathf.Lerp(minHeight, maxHeight, t);
        }

        private float GetRadius(float height)
        {
            return Mathf.Sqrt(CylinderVolume / (Mathf.PI * height));
        }
    }
}