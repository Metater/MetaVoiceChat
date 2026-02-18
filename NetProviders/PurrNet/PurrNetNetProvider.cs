#if METAVC_PURRNET
using UnityEngine;
using PurrNet;

namespace MetaVoiceChat.NetProviders.PurrNet
{
    [RequireComponent(typeof(MetaVc))]
    public class PurrNetNetProvider : NetworkBehaviour, INetProvider
    {
        private MetaVc MetaVc { get; set; }

        private const int DefaultMaxDataBytesPerPacket = 1000;

        protected override void OnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer)
        {
            base.OnOwnerChanged(oldOwner, newOwner, asServer);
        
            MetaVc = GetComponent<MetaVc>();
            MetaVc.StartClient(this, isOwner, DefaultMaxDataBytesPerPacket);
        }
        
        protected override void OnDespawned()
        {
            base.OnDespawned();
            MetaVc.StopClient();
        }

        public bool IsLocalPlayerDeafened => false;

        public void RelayFrame(int index, double timestamp, System.ReadOnlySpan<byte> data)
        {
            byte[] dataToSend = data.ToArray(); 

            float additionalLatency = Time.deltaTime;

            var frame = new PurrNetFrame(index, timestamp, additionalLatency, dataToSend);

            if (isServer)
            {
                ReceiveFrameObserversRpc(frame);
            }
            else
            {
                RelayFrameServerRpc(frame);
            }
        }

        [ServerRpc]
        private void RelayFrameServerRpc(PurrNetFrame frame, RPCInfo info = default)
        {
            frame.additionalLatency += Time.deltaTime;
            ReceiveFrameObserversRpc(frame);
        }

        [ObserversRpc (excludeOwner:true)]
        private void ReceiveFrameObserversRpc(PurrNetFrame frame, RPCInfo info = default)
        {
            float latency = frame.additionalLatency;

            if (isServer)
            {
                latency -= Time.deltaTime;
            }

            MetaVc.ReceiveFrame(frame.index, frame.timestamp, latency, frame.data);
        }
    }
}
#endif