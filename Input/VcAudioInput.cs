using System;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat.Input
{
    public abstract class VcAudioInput : MonoBehaviour
    {
        public MetaVc metaVc;
        public VcInputFilter inputFilter;

        public event Action<int, float[]> OnFrameReady;

        protected void SendFrame(int frameIndex, float[] samples)
        {
            inputFilter.Filter(ref samples);
            OnFrameReady?.Invoke(frameIndex, samples);
        }
    }
}