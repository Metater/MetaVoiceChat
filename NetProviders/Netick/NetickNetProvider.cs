#if NETICK
using System;
using System.Collections.Generic;
using Netick.Unity;
using UnityEngine;

namespace MetaVoiceChat.NetProviders.Netick
{
    [RequireComponent(typeof(MetaVc))]
    public class NetickNetProvider : NetworkBehaviour, INetProvider
    {
        #region Singleton
        public static NetickNetProvider LocalPlayerInstance { get; private set; }
        private readonly static List<NetickNetProvider> instances = new();
        public static IReadOnlyList<NetickNetProvider> Instances => instances;
        #endregion

        bool INetProvider.IsLocalPlayerDeafened => LocalPlayerInstance != null && LocalPlayerInstance.MetaVc.isDeafened;

        public MetaVc MetaVc { get; private set; }

        private MetaVoiceChatNetick VoiceDataTransmitter;
        private int playerID;

        public override void NetworkStart()
        {
            #region Singleton
            if (IsInputSource)
            {
                LocalPlayerInstance = this;
            }

            instances.Add(this);
            #endregion
            
            if (Sandbox.TryGetComponent<MetaVoiceChatNetick>(out VoiceDataTransmitter))
            {
                if (Sandbox.IsServer)
                {
                    playerID = InputSource.PlayerId;
                    VoiceDataTransmitter.ConnectionIdToPlayerObjectID.Add(playerID, Object.Id);
                }
            }
            else
                Debug.LogError("Your Sandbox Prefab doesnt have the MetaVoiceChatNetick component");

            static int GetMaxDataBytesPerPacket()
            {
                int bytes = 1000;           //safe number
                bytes -= sizeof(int);       // Index
                bytes -= sizeof(double);    // Timestamp
                bytes -= MetaVoiceChatNetick.additionalLatencySize;
                bytes -= sizeof(int);       // Player id
                //bytes -= sizeof(ushort);    // Array length (not needed?)
                return bytes;
            }

            MetaVc = GetComponent<MetaVc>();
            MetaVc.StartClient(this, IsInputSource, GetMaxDataBytesPerPacket());
        }

        public override void NetworkDestroy()
        {
            #region Singleton
            if (IsInputSource)
            {
                LocalPlayerInstance = null;
            }

            instances.Remove(this);
            #endregion

            MetaVc.StopClient();

            if (Sandbox.IsServer && VoiceDataTransmitter != null)
                VoiceDataTransmitter.ConnectionIdToPlayerObjectID.Remove(playerID);
        }

        void INetProvider.RelayFrame(int index, double timestamp, ReadOnlySpan<byte> data)
        {
            if (VoiceDataTransmitter == null)
            {
                Debug.LogError("[MetaVoiceChat] MetaVoiceChatNetick component is missing from the Sandbox prefab.");
                return;
            }

            float additionalLatency = GetAdditionalLatency();

            if (Sandbox.IsServer)
            {
                //send the data to all clients
                VoiceDataTransmitter.SendServerVoiceToClients(index, timestamp, additionalLatency, data, playerID);
            }
            else
            {
                //send the data from client to server
                VoiceDataTransmitter.SendVoiceDataToServer(index, timestamp, additionalLatency, data);
            }
        }

        public static float GetAdditionalLatency()
        {
            return Time.deltaTime;
        }

        public void ReceiveFrame(int index, double timestamp, float additionalLatency, ReadOnlySpan<byte> data)
        {
            MetaVc.ReceiveFrame(index, timestamp, additionalLatency, data);
        }
    }
}
#endif