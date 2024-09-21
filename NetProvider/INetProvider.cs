using System;

namespace Assets.Metater.MetaVoiceChat.VcImpls
{
    public interface INetProvider
    {
        MetaVc MetaVc { get; set; }
        bool IsLocalPlayerDeafened { get; }

        void RelayFrame(int index, double timestamp, ReadOnlySpan<byte> data);
    }
}