#if METAVC_PURRNET
using System;

namespace MetaVoiceChat.NetProviders.PurrNet
{
    [Serializable]
    public struct PurrNetFrame
    {
        public int index;
        public double timestamp;
        public float additionalLatency;
        public byte[] data;

        public PurrNetFrame(int index, double timestamp, float additionalLatency, byte[] data)
        {
            this.index = index;
            this.timestamp = timestamp;
            this.additionalLatency = additionalLatency;
            this.data = data;
        }
    }
}
#endif