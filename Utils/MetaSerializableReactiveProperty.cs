using System;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat.Utils
{
    /// <summary>
    /// Limitation: The event is not triggered when the value is changed in the inspector.
    /// </summary>
    [Serializable]
    public class MetaSerializableReactiveProperty<T> where T : IEquatable<T>
    {
        [SerializeField] private T value;

        public T Value
        {
            get => value;
            set
            {
                if (value == null)
                {
                    if (this.value != null)
                    {
                        OnValueChanged?.Invoke(value);
                    }
                }
                else if (!value.Equals(this.value))
                {
                    OnValueChanged?.Invoke(value);
                }

                this.value = value;
            }
        }

        public event Action<T> OnValueChanged;

        public static implicit operator T(MetaSerializableReactiveProperty<T> value) => value.Value;
    }
}