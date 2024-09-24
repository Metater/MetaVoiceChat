// Influenced by: https://github.com/adrenak/univoice-unimic-input/blob/master/Assets/Adrenak.UniVoice.UniMicInput/Runtime/UniVoiceUniMicInput.cs

namespace Assets.Metater.MetaVoiceChat.Input.Mic
{
    public class VcMicAudioInput : VcAudioInput
    {
        private VcMic mic;

        private void Start()
        {
            int samplesPerFrame = metaVc.config.samplesPerFrame;

            mic = new(this, samplesPerFrame);
            mic.OnFrameReady += SendFrame;
            mic.StartRecording();
        }

        private void OnDestroy()
        {
            mic.OnFrameReady -= SendFrame;
            mic.Dispose();
        }
    }
}