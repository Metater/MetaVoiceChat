using System;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat.Input
{
    public abstract class VcAudioInput : MonoBehaviour
    {
        public MetaVc metaVc;
        [Tooltip("The first audio input filter in the pipeline. This can be null.")]
        public VcInputFilter firstInputFilter;

        public event Action<int, float[]> OnFrameReady;

        public abstract void StartLocalPlayer();

        protected void SendAndFilterFrame(int index, float[] samples)
        {
            if (firstInputFilter != null)
            {
                firstInputFilter.FilterRecursively(index, ref samples);
            }

            OnFrameReady?.Invoke(index, samples);
        }
    }
}