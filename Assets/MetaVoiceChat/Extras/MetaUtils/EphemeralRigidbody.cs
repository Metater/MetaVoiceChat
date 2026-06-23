using System;
using System.Collections.Generic;
using UnityEngine;

namespace Metater
{
    public class EphemeralRigidbody : MonoBehaviour
    {
        [Header("Special")]
        public bool enableCollidersOnAwake = true;

        [Header("Rigidbody")]
        public float mass = 1;
        public float drag = 0;
        public float angularDrag = 0.05f;
        public Vector3 centerOfMass = Vector3.zero;
        public bool automaticCenterOfMass = true;
        public bool automaticTensor = true;
        public bool useGravity = true;
        public RigidbodyInterpolation interpolate = RigidbodyInterpolation.Interpolate;
        public CollisionDetectionMode collisionDetection = CollisionDetectionMode.Continuous;
        public WrappedRigidbodyConstraints constraints = WrappedRigidbodyConstraints.None;
        public LayerMask includeLayers;
        public LayerMask excludeLayers;

        private readonly List<Collider> colliders = new();

        public Rigidbody Rigidbody { get; private set; }

        private bool isEnabled = false;

        public bool IsEnabled
        {
            get
            {
                return isEnabled;
            }
            set
            {
                if (value == isEnabled)
                {
                    return;
                }

                isEnabled = value;

                if (isEnabled)
                {
                    if (gameObject.TryGetComponent<Rigidbody>(out var existingRigidbody))
                    {
                        Destroy(existingRigidbody);
                    }

                    Rigidbody = gameObject.AddComponent<Rigidbody>();
                    Rigidbody.mass = mass;
                    Rigidbody.linearDamping = drag;
                    Rigidbody.angularDamping = angularDrag;
                    Rigidbody.centerOfMass = centerOfMass;
                    Rigidbody.automaticCenterOfMass = automaticCenterOfMass;
                    Rigidbody.automaticInertiaTensor = automaticTensor;
                    Rigidbody.useGravity = useGravity;
                    Rigidbody.isKinematic = false;
                    Rigidbody.interpolation = interpolate;
                    Rigidbody.collisionDetectionMode = collisionDetection;
                    Rigidbody.constraints = (RigidbodyConstraints)constraints;
                    Rigidbody.includeLayers = includeLayers;
                    Rigidbody.excludeLayers = excludeLayers;

                    AreCollidersEnabled = true;
                }
                else
                {
                    Destroy(Rigidbody);
                    Rigidbody = null;
                }
            }
        }

        private bool areCollidersEnabled = false;

        public bool AreCollidersEnabled
        {
            get
            {
                return areCollidersEnabled;
            }
            set
            {
                if (value == areCollidersEnabled)
                {
                    return;
                }

                areCollidersEnabled = value;

                foreach (var collider in colliders)
                {
                    collider.enabled = areCollidersEnabled;
                }
            }
        }

        private void Awake()
        {
            if (gameObject.TryGetComponent<Collider>(out var collider))
            {
                colliders.Add(collider);
            }

            gameObject.GetComponentsInChildren(colliders);

            AreCollidersEnabled = enableCollidersOnAwake;
        }

        [Flags]
        public enum WrappedRigidbodyConstraints
        {
            None = 0,
            FreezePositionX = 2,
            FreezePositionY = 4,
            FreezePositionZ = 8,
            FreezeRotationX = 16,
            FreezeRotationY = 32,
            FreezeRotationZ = 64,
            FreezePosition = 14,
            FreezeRotation = 112,
            FreezeAll = 126
        }

        public static RigidbodyConstraints ConvertWrappedRigidbodyConstraints(WrappedRigidbodyConstraints constraints)
        {
            return (RigidbodyConstraints)constraints;
        }
    }
}
