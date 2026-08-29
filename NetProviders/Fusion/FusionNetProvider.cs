#if FUSION_2_0_OR_NEWER
using System;
using Fusion;

namespace MetaVoiceChat.NetProviders.Fusion
{
    public class FusionNetProvider : NetworkBehaviour, INetProvider
    {
        public static FusionNetProvider LocalPlayerInstance { get; private set; }
        bool INetProvider.IsLocalPlayerDeafened => LocalPlayerInstance != null && LocalPlayerInstance.MetaVc.isDeafened;

        public MetaVc MetaVc;

        #if UNITY_EDITOR
        [UnityEngine.SerializeField] private bool debug = false;
        #endif

        private static int GetMTU() => 1280 - sizeof(int) - sizeof(double) - sizeof(byte) - sizeof(ushort); // estimated mtu - index - timestamp - additionalLatency - length

        public override void Spawned()
        {
            base.Spawned();

            if (Object.HasInputAuthority)
                LocalPlayerInstance = this;

            #if UNITY_EDITOR
            if(debug)
                UnityEngine.Debug.Log("MetaVC Started!");
            #endif
            MetaVc?.StartClient(this, Object.HasInputAuthority, GetMTU());
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            base.Despawned(runner, hasState);

            if (Object.HasInputAuthority)
                LocalPlayerInstance = null;

            MetaVc?.StopClient();
        }

        void INetProvider.RelayFrame(int index, double timestamp, ReadOnlySpan<byte> data)
        {
            #if UNITY_EDITOR
            if(debug)
                UnityEngine.Debug.Log($"Frame received! Length: {data.Length}");
            #endif

            if (data.Length <= 0) return;

            float latency = Runner.DeltaTime;

            byte[] frame = FusionFrame.Serialize(index, timestamp, latency, data);

            if (Object.HasStateAuthority)
                RpcBroadcastFrame(frame);
            else if(Object.HasInputAuthority)
                RpcRelayFrame(frame);
        }

        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Unreliable)]
        private void RpcRelayFrame(byte[] packedFrame, RpcInfo info = default)
        {
            var frame = FusionFrame.Deserialize(packedFrame);
            float additionalLatency = frame.additionalLatency + Runner.DeltaTime;

            if (!Object.HasInputAuthority)
                MetaVc?.ReceiveFrame(frame.index, frame.timestamp, additionalLatency, frame.data.Span);
    
            RpcBroadcastFrame(FusionFrame.Serialize(frame.index, frame.timestamp, additionalLatency, frame.data.Span));
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Unreliable)]
        public void RpcBroadcastFrame(byte[] packedFrame, RpcInfo info = default)
        {
            if (Object.HasInputAuthority) return;

            var frame = FusionFrame.Deserialize(packedFrame);
            
            #if UNITY_EDITOR
            if (debug)
                UnityEngine.Debug.Log($"Frame received! Length: {frame.data.Length}");
            #endif

            MetaVc?.ReceiveFrame(frame.index, frame.timestamp, frame.additionalLatency, frame.data.Span);
        }
    }
}
#endif