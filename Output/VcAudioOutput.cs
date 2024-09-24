using UnityEngine;

namespace Assets.Metater.MetaVoiceChat.Output
{
    public abstract class VcAudioOutput : MonoBehaviour
    {
        public MetaVc metaVc;

        public abstract void ReceiveFrame(int index, float[] samples, float targetLatency);
    }
}