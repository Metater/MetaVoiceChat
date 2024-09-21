#if MIRROR
using System;
using Assets.Metater.MetaVoiceChat.General;
using Mirror;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat.VcImpls.Mirror
{
    [RequireComponent(typeof(MetaVc))]
    public class MirrorVcImpl : NetworkBehaviour, IVcImpl
    {
        public MetaVc MetaVc { get; set; }

        private void Awake()
        {
            MetaVc = GetComponent<MetaVc>();
        }

        public override void OnStartClient()
        {
            static int GetMaxDataBytesPerPacket()
            {
                int bytes = NetworkMessages.MaxMessageSize(Channels.Unreliable) - 13;
                bytes -= sizeof(bool); // Is speaking
                bytes -= sizeof(double); // Timestamp
                bytes -= sizeof(ushort); // Array length
                return bytes;
            }

            MetaVc.StartClient(this, isLocalPlayer, GetMaxDataBytesPerPacket());
        }

        public override void OnStopClient()
        {
            MetaVc.StopClient(isLocalPlayer);
        }

        VcFrame IVcImpl<VcFrame>.MakeEncodedFrame(double timestamp, ReadOnlySpan<byte> data)
        {
            return new VcFrame(timestamp, data);
        }

        VcFrame IVcImpl<VcFrame>.MakeDecodedFrame(double timestamp, ReadOnlySpan<short> samples)
        {
            return new VcFrame(timestamp, samples);
        }
    }
}
#endif