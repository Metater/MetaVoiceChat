#if MIRROR
using System;
using System.Buffers;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat.NetProviders.Mirror
{
    [RequireComponent(typeof(MetaVc))]
    public class MirrorNetProvider : NetworkBehaviour, INetProvider
    {
        #region Singleton
        public static MirrorNetProvider LocalPlayerInstance { get; private set; }
        private readonly static List<MirrorNetProvider> instances = new();
        public static IReadOnlyList<MirrorNetProvider> Instances => instances;
        #endregion

        public MetaVc MetaVc { get; set; }

        bool INetProvider.IsLocalPlayerDeafened => LocalPlayerInstance.MetaVc.IsDeafened;

        private void Awake()
        {
            MetaVc = GetComponent<MetaVc>();
        }

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
                bytes -= sizeof(ushort); // Array length
                return bytes;
            }

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
            var array = ArrayPool<byte>.Shared.Rent(data.Length);
            data.CopyTo(array);
            var arraySegment = new ArraySegment<byte>(array, 0, data.Length);

            MirrorFrame frame = new(index, timestamp, arraySegment);
            CmdRelayFrame(frame);

            ArrayPool<byte>.Shared.Return(array);
        }

        [Command(channel = Channels.Unreliable)]
        private void CmdRelayFrame(MirrorFrame frame)
        {
            RpcReceiveFrame(frame);
        }

        // A possible optimization is to use target RPCs and only send filled arrays to clients that are within audible range, and empty arrays to others.
        // Audible range would be determined by the distance between the reciever's position and the sender's audio source position.
        [ClientRpc(channel = Channels.Unreliable, includeOwner = false)]
        private void RpcReceiveFrame(MirrorFrame frame)
        {
            MetaVc.ReceiveFrame(frame.index, frame.timestamp, frame.data);
        }
    }
}
#endif
