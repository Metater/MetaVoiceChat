#define R3

using R3;

#if R3
namespace Assets.Metater.MetaVoiceChat.General
{
    public class R3MetaVc : MetaVc
    {
        /// <summary>
        /// This plays back the voice of the local player
        /// </summary>
        public SerializableReactiveProperty<bool> isEchoEnabled;
        /// <summary>
        /// This is the local player and they don't want to hear anyone else
        /// </summary>
        public SerializableReactiveProperty<bool> isDeafened;
        /// <summary>
        /// This is the local player and they don't want anyone to hear them
        /// </summary>
        public SerializableReactiveProperty<bool> isInputMuted;
        /// <summary>
        /// This is a remote player that the local player doesn't want to hear
        /// </summary>
        public SerializableReactiveProperty<bool> isOutputMuted;
        /// <summary>
        /// This is the local player and they are speaking
        /// </summary>
        public SerializableReactiveProperty<bool> isSpeaking;
    }
}
#endif
