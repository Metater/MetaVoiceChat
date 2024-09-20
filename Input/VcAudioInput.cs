// https://github.com/adrenak/univoice-unimic-input/blob/master/Assets/Adrenak.UniVoice.UniMicInput/Runtime/UniVoiceUniMicInput.cs

using System;

namespace Assets.Metater.MetaVoiceChat.Input
{
    public class VcAudioInput : IDisposable
    {
        public readonly VcConfig config;
        public readonly VcAudioProcessor processor;
        public readonly VcMic mic;

        public event Action<int, float[]> OnSegmentReady;

        //private double lastDetectionSeconds = double.MinValue;

        public VcAudioInput(VcConfig config)
        {
            this.config = config;
            processor = config.AudioProcessor;

            mic = new(config);
            mic.StartRecording();
            mic.OnSegmentReady += Mic_OnSegmentReady;
        }

        private void Mic_OnSegmentReady(int segmentIndex, float[] segment)
        {
            //bool isVoiceDetected = DetectVoice(segment);
            //if (isVoiceDetected)
            //{
            //    lastDetectionSeconds = Meta.Realtime;
            //}

            //bool shouldSpeak = Meta.Realtime - config.DetectionLatchSeconds < lastDetectionSeconds;
            //if (shouldSpeak)
            //{
            OnSegmentReady?.Invoke(segmentIndex, segment);
            //}
            //else
            //{
            //    OnSegmentReady?.Invoke(segmentIndex, null);
            //}
        }

        //private bool DetectVoice(float[] segment)
        //{
        //    float percentage = GetPercentageAboveThreshold(segment);
        //    return percentage > config.DetectionPercentage;
        //}

        //private float GetPercentageAboveThreshold(float[] segment)
        //{
        //    float percentageIncrement = 1f / segment.Length;
        //    float percentage = 0;
        //    float detectionValue = config.DetectionValue;
        //    foreach (float value in segment)
        //    {
        //        if (Math.Abs(value) > detectionValue)
        //        {
        //            percentage += percentageIncrement;
        //        }
        //    }

        //    return percentage;
        //}

        public void Dispose()
        {
            mic.OnSegmentReady -= Mic_OnSegmentReady;
            mic.Dispose();
        }
    }
}