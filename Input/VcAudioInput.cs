// Influenced by: https://github.com/adrenak/univoice-unimic-input/blob/master/Assets/Adrenak.UniVoice.UniMicInput/Runtime/UniVoiceUniMicInput.cs

using System;

namespace Assets.Metater.MetaVoiceChat.Input
{
    public class VcAudioInput : IDisposable
    {
        public readonly VcConfig config;
        public readonly VcInputFilter inputFilter;
        public readonly VcMic mic;

        public event Action<int, float[]> OnFrameReady;

        public VcAudioInput(VcConfig config)
        {
            this.config = config;
            inputFilter = config.general.inputFilter;

            mic = new(config);
            mic.StartRecording();
            mic.OnFrameReady += Mic_OnFrameReady;
        }

        private void Mic_OnFrameReady(int frameIndex, float[] samples)
        {
            inputFilter.Filter(ref samples);
            OnFrameReady?.Invoke(frameIndex, samples);
        }

        public void Dispose()
        {
            mic.OnFrameReady -= Mic_OnFrameReady;
            mic.Dispose();
        }
    }
}