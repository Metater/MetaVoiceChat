#if METAVC_RIPTIDE
using System;
using System.Collections.Generic;
using MetaVoiceChat.Utils;
using UnityEngine;
using Riptide;

namespace MetaVoiceChat.NetProviders.Riptide
{
    [RequireComponent(typeof(MetaVc))]
    public class RiptideNetProvider : MonoBehaviour, INetProvider
    {
        private static RiptideNetProvider _singleton;
        public static RiptideNetProvider Singleton
        {
            get { return _singleton; }
            set
            {
                if (_singleton == null)
                {
                    _singleton = value;
                }

                else if (_singleton != value)
                {
                    Debug.Log($"{nameof(RiptideNetProvider)} instance already exists, destroying duplicate");
                    Destroy(value);
                }
            }
        }

        static int GetMaxDataBytesPerPacket()
        {
            int bytes = MaxVoiceBytes;
            bytes -= sizeof(int); // Index
            bytes -= sizeof(double); // Timestamp
            bytes -= sizeof(byte); // Additional latency
            bytes -= sizeof(ushort); // Array length
            return bytes;
        }

        bool INetProvider.IsLocalPlayerDeafened => Singleton.MetaVc.isDeafened;

        public MetaVc MetaVc { get; private set; }

        static int MaxVoiceBytes = 1100;
        [Header("Components")]
        [SerializeField] private Player player;

        private void Start()
        {
            if (player.IsLocal) Singleton = this;

            MetaVc = GetComponent<MetaVc>();
            MetaVc.StartClient(this, player.IsLocal, GetMaxDataBytesPerPacket());
        }

        private void OnDestroy()
        {
            if (player.IsLocal) Singleton = null;

            MetaVc.StopClient();
        }

        public void RelayFrame(int index, double timestamp, ReadOnlySpan<byte> data)
        {
            // Debug.Log($"<color=blue>[RiptideNetProvider - RelayFrame]</color> Relaying voice frame for player '{player.username}' (Index: {index}, Timestamp: {timestamp}, Data Length: {data.Length} bytes)");
            byte[] array = FixedLengthArrayPool<byte>.Rent(data.Length);
            data.CopyTo(array);

            float additionalLatency = Time.deltaTime;

            if (NetworkManager.Singleton.Server.IsRunning) ServerSendVoice(player.Id, index, timestamp, additionalLatency, array);
            else ClientSendVoice(index, timestamp, additionalLatency, array);
        }

        #region ServerToClientSenders
        private void ServerSendVoice(ushort playerId, int index, double timestamp, float additionalLatency, byte[] array)
        {
            if (!Player.List.TryGetValue(playerId, out Player player))
            {
                // Debug.LogWarning($"<color=red>[RiptideNetProvider - ServerSendVoice]</color> Could not find player with ID {playerId} to send voice frame!");
                return;
            }
            // Debug.Log($"<color=green>[RiptideNetProvider - ServerSendVoice]</color> Sending voice from server for player '{player.username}'" +
            // $" (Index: {index}, Timestamp: {timestamp}, Latency: {additionalLatency}s, DatLength: {array.Length} bytes)");

            Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.voiceChat);
            message.AddUShort(player.Id);
            message.AddInt(index);
            message.AddDouble(timestamp);
            message.AddFloat(additionalLatency);
            message.AddBytes(array);
            NetworkManager.Singleton.Server.SendToAll(message);
        }
        #endregion

        #region ClientToServerSenders
        private void ClientSendVoice(int index, double timestamp, float additionalLatency, byte[] array)
        {
            // Debug.Log($"<color=green>[RiptideNetProvider - ClientSendVoice]</color> Sending voice from client for player '{player.username}' (Index: {index}, Timestamp: {timestamp}," +
            // $" Latency: {additionalLatency}s, Data Length: {array.Length} bytes)");
            Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.voiceChat);
            message.AddInt(index);
            message.AddDouble(timestamp);
            message.AddFloat(additionalLatency);
            message.AddBytes(array);
            NetworkManager.Singleton.Client.Send(message);
        }
        #endregion

        #region ServerToClientHandlers
        [MessageHandler((ushort)ServerToClientId.voiceChat)]
        private static void ReceiveServerVoice(Message message)
        {
            if (NetworkManager.Singleton.Server.IsRunning) return;
            ushort playerId = message.GetUShort();
            if (Player.List.TryGetValue(playerId, out Player player))
            {
                if (player.IsLocal) return;
                // Debug.Log($"<color=red>[RiptideNetProvider - ReceiveServerVoice]</color> Received voice from server (Player ID: {playerId})");
                player.metaVc.ReceiveFrame(message.GetInt(), message.GetDouble(), message.GetFloat(), message.GetBytes());
            }
        }
        #endregion

        #region ClientToServerHandlers
        [MessageHandler((ushort)ClientToServerId.voiceChat)]
        private static void ReceiveClientVoice(ushort fromClientId, Message message)
        {
            if (Player.List.TryGetValue(fromClientId, out Player player))
            {
                int index = message.GetInt();
                double timestamp = message.GetDouble();
                float additionalLatency = message.GetFloat();
                byte[] data = message.GetBytes();

                // Debug.Log($"<color=red>[RiptideNetProvider - ReceiveClientVoice]</color> Received voice from player {player.username}");
                player.metaVc.ReceiveFrame(index, timestamp, additionalLatency, data);
                Singleton.ServerSendVoice(fromClientId, index, timestamp, additionalLatency, data);
            }
        }
        #endregion
    }
}
#endif
