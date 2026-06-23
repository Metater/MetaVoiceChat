using UnityEngine;

namespace Metater
{
    public class PlayerAnimTilt : MonoBehaviour
    {
        [Header("General")]
        public Transform playerTransform;
        public Transform offsetTransform;
        public float maxTiltDegrees;
        public PlayerAnimStrength forwardBackwardStrength;
        public PlayerAnimStrength leftRightStrength;

        [Header("Smooth Damp")]
        public float smoothTime;
        public float maxSpeed;
        private Vector2 currentRotation = Vector2.zero;
        private Vector2 rotationCurrentVelocity = Vector2.zero;

        private bool isInit = false;
        private Vector3 lastPosition;

        private void Update()
        {
            if (!MetaPlayerController.Instance)
            {
                return;
            }

            if (!isInit)
            {
                isInit = true;
                lastPosition = transform.position;
            }

            var velocity = (transform.position - lastPosition) / Time.deltaTime;

            lastPosition = transform.position;

            var controller = MetaPlayerController.Instance;
            float maxHorizontalSpeed = controller.NominalMaxHorizontalSpeed;

            Vector2 movement = new(velocity.x, velocity.z);
            Vector2 direction = movement.normalized;

            float speedT = movement.magnitude / maxHorizontalSpeed;
            float tiltDegrees = maxTiltDegrees * speedT;

            tiltDegrees = Mathf.Clamp(tiltDegrees, 0f, 60f);

            direction = RotateVector(direction, playerTransform.localEulerAngles.y);
            Vector2 targetRotation = new(direction.y * tiltDegrees, -direction.x * tiltDegrees);

            targetRotation.x *= forwardBackwardStrength switch
            {
                PlayerAnimStrength.Weak => 0.5f,
                PlayerAnimStrength.Strong => 1f,
                _ => 0,
            };

            targetRotation.y *= leftRightStrength switch
            {
                PlayerAnimStrength.Weak => 0.5f,
                PlayerAnimStrength.Strong => 1f,
                _ => 0,
            };

            currentRotation = Vector2.SmoothDamp(currentRotation, targetRotation, ref rotationCurrentVelocity, smoothTime, maxSpeed);

            offsetTransform.transform.localRotation = Quaternion.Euler(currentRotation.x, 0, currentRotation.y);
        }

        private Vector2 RotateVector(Vector2 vector, float degrees)
        {
            float angle = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            float x = vector.x * cos - vector.y * sin;
            float y = vector.x * sin + vector.y * cos;
            return new Vector2(x, y);
        }
    }
}