using UnityEngine;

namespace Assets.Metater.MetaVoiceChat.Output
{
    public abstract class VcAudioOutput : MonoBehaviour
    {
        public MetaVc metaVc;
        [Tooltip("The first audio output filter in the pipeline. This can be null.")]
        public VcOutputFilter firstOutputFilter;

        protected abstract void ReceiveFrame(int index, float[] samples, float targetLatency);

        public void ReceiveAndFilterFrame(int index, float[] samples, float targetLatency)
        {
            if (firstOutputFilter != null)
            {
                firstOutputFilter.FilterRecursively(index, samples, targetLatency);
            }

            ReceiveFrame(index, samples, targetLatency);
        }
    }
}