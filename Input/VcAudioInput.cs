using System;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat.Input
{
    public abstract class VcAudioInput : MonoBehaviour
    {
        public MetaVc metaVc;
        public VcInputFilter inputFilter;

        public event Action<int, float[]> OnFrameReady;

        public abstract void StartLocalPlayer();

        protected void SendFrame(int frameIndex, float[] samples)
        {
            inputFilter.Filter(ref samples);
            OnFrameReady?.Invoke(frameIndex, samples);
        }
    }
}