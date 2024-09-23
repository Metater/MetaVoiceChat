using System;

namespace Assets.Metater.MetaVoiceChat.NetProviders
{
    public interface INetProvider
    {
        bool IsLocalPlayerDeafened { get; }

        void RelayFrame(int index, double timestamp, ReadOnlySpan<byte> data);
    }
}