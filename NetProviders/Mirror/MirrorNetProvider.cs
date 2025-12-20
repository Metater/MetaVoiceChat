#if (MIRROR || UNITY_SERVER) && !FISHNET
using System;
using System.Collections.Generic;
using MetaVoiceChat.Utils;
using Mirror;
using UnityEngine;

// A possible optimization is to handle all of the networking in one manager class and batch frames with a single timestamp.
// However, this is complex and benefits are negligible.

namespace MetaVoiceChat.NetProviders.Mirror
{
    [RequireComponent(typeof(MetaVc))]
    public class MirrorNetProvider : NetworkBehaviour, INetProvider
    {
        #region Singleton
        public static MirrorNetProvider LocalPlayerInstance { get; private set; }
        private readonly static List<MirrorNetProvider> instances = new();
        public static IReadOnlyList<MirrorNetProvider> Instances => instances;
        #endregion

        bool INetProvider.IsLocalPlayerDeafened => LocalPlayerInstance.MetaVc.isDeafened;

        public MetaVc MetaVc { get; private set; }

        public override void OnStartClient()
        {
            #region Singleton
            if (isLocalPlayer)
            {
                LocalPlayerInstance = this;
            }

            instances.Add(this);
            #endregion

            static int GetMaxDataBytesPerPacket()
            {
                int bytes = NetworkMessages.MaxMessageSize(Channels.Unreliable) - 13;
                bytes -= sizeof(int); // Index
                bytes -= sizeof(double); // Timestamp
                bytes -= sizeof(byte); // Additional latency
                bytes -= sizeof(ushort); // Array length
                return bytes;
            }

            MetaVc = GetComponent<MetaVc>();
            MetaVc.StartClient(this, isLocalPlayer, GetMaxDataBytesPerPacket());
        }

        public override void OnStopClient()
        {
            #region Singleton
            if (isLocalPlayer)
            {
                LocalPlayerInstance = null;
            }

            instances.Remove(this);
            #endregion

            MetaVc.StopClient();
        }

        void INetProvider.RelayFrame(int index, double timestamp, ReadOnlySpan<byte> data)
        {
            var array = FixedLengthArrayPool<byte>.Rent(data.Length);
            data.CopyTo(array);

            float additionalLatency = Time.deltaTime;
            MirrorFrame frame = new(index, timestamp, additionalLatency, array);

            if (isServer)
            {
                RpcReceiveFrame(frame);
            }
            else
            {
                CmdRelayFrame(frame);
            }

            FixedLengthArrayPool<byte>.Return(array);
        }

        [Command(channel = Channels.Unreliable)]
        private void CmdRelayFrame(MirrorFrame frame)
        {
            float additionalLatency = frame.additionalLatency + Time.deltaTime;
            frame = new(frame.index, frame.timestamp, additionalLatency, frame.data);
            RpcReceiveFrame(frame);
        }

        // A possible optimization is to use target RPCs and only send filled arrays to clients that are within audible range, and empty arrays to others.
        // Audible range would be determined by the distance between the reciever's position and the sender's audio source position.
        [ClientRpc(channel = Channels.Unreliable, includeOwner = false)]
        private void RpcReceiveFrame(MirrorFrame frame)
        {
            if (MetaVc == null)
                return;
        
            if (isServer)
            {
                // Don't apply server Time.deltaTime to additionalLatency -- this frame did not go over the network again.
                float additionalLatency = frame.additionalLatency - Time.deltaTime;
                MetaVc.ReceiveFrame(frame.index, frame.timestamp, additionalLatency, frame.data);
            }
            else
            {
                MetaVc.ReceiveFrame(frame.index, frame.timestamp, frame.additionalLatency, frame.data);
            }
        }
    }
}
#endif
