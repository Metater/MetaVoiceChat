using Assets.Metater.MetaVoiceChat.General;

namespace Assets.Metater.MetaVoiceChat.VcImpls
{
    public interface IVcImpl
    {
        MetaVc MetaVc { get; set; }
        bool IsLocalPlayerDeafened { get; }

        void RelayFrame<T>(T frame) where T : IVcFrame;
    }

    public static class IVcImplExtensions
    {

    }
}