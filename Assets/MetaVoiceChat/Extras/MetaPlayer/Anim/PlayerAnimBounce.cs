using UnityEngine;

namespace Metater
{
    public class PlayerAnimBounce : MonoBehaviour
    {
        [Header("General")]
        public float minYOffset;
        public float maxYOffset;
        public Transform offsetTransform;

        [Header("Smooth Damp")]
        public float smoothTime;
        public float maxSpeed;
        private float currentVelocity = 0;

        private bool isInit = false;
        private Vector3 lastPosition;

        private void Update()
        {
            if (!isInit)
            {
                isInit = true;
                lastPosition = transform.position;
            }

            var velocity = (transform.position - lastPosition) / Time.deltaTime;

            lastPosition = transform.position;

            float currentYOffset = offsetTransform.transform.localPosition.y;
            float targetYOffset = GetYOffset(velocity.y);
            float yOffset = Mathf.SmoothDamp(currentYOffset, targetYOffset, ref currentVelocity, smoothTime, maxSpeed);
            offsetTransform.transform.localPosition = new(0, yOffset, 0);
        }

        private float GetYOffset(float velocity)
        {
            float t = Mathf.InverseLerp(6f, -6f, velocity);
            return Mathf.Lerp(minYOffset, maxYOffset, t);
        }
    }
}