#if MIRROR
using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat.VcImpls.Mirror
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
                bytes -= sizeof(bool); // Is speaking
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

            MetaVc.StopClient(isLocalPlayer);
        }

        void INetProvider.RelayFrame(int index, double timestamp, ReadOnlySpan<byte> data)
        {
            if (MetaVc.IsEchoEnabled)
            {
                MetaVc.ReceiveFrame(index, timestamp, data);
            }

            CmdRelayFrame(index, timestamp);
        }

        [Command(channel = Channels.Unreliable)]
        private void CmdRelayFrame(int index, double timestamp, NetworkConnectionToClient sender = null)
        {
            RpcReceiveFrame(index, timestamp);
        }

        // A possible optimization is to use target RPCs and only send filled arrays to clients that are within audible range, and empty to others.
        // Audible range would be determined by the distance between the reciever's position and the sender's audio source position.
        [ClientRpc(channel = Channels.Unreliable, includeOwner = false)]
        private void RpcReceiveFrame(int index, double timestamp)
        {
            MetaVc.ReceiveFrame(index, timestamp, ReadOnlySpan<byte>.Empty);
        }
    }
}
#endif