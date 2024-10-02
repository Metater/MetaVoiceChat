// Based on: https://github.com/adrenak/univoice-audiosource-output/blob/master/Assets/Adrenak.UniVoice.AudioSourceOutput/Runtime/CircularAudioClip.cs

using System;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat.Output.AudioSource
{
    public class VcAudioClip : IDisposable
    {
        private readonly int samplesPerFrame;
        private readonly int framesPerClip;
        private readonly AudioClip audioClip;

        private readonly float[] emptyFrame;
        private readonly float[] emptyClip;

        public float Length => audioClip.length;

        public VcAudioClip(int samplesPerFrame, int framesPerClip, UnityEngine.AudioSource audioSource)
        {
            this.samplesPerFrame = samplesPerFrame;
            this.framesPerClip = framesPerClip;

            audioClip = AudioClip.Create(nameof(VcAudioClip), VcConfig.SamplesPerClip, channels: 1, VcConfig.SamplesPerSecond, false);

            emptyFrame = new float[samplesPerFrame];
            emptyClip = new float[VcConfig.SamplesPerClip];

            audioSource.loop = true;
            audioSource.clip = audioClip;
        }

        public void WriteFrame(int offsetFrames, float[] samples)
        {
            samples ??= emptyFrame;

            if (samples.Length != samplesPerFrame)
            {
                throw new Exception("Voice chat audio clip samples per frame does not match the config!");
            }

            int offsetSamples = samplesPerFrame * offsetFrames;
            audioClip.SetData(samples, offsetSamples);
        }

        public int GetOffsetFrames(int frameIndex)
        {
            return frameIndex % framesPerClip;
        }

        public void ClearFrame(int offsetFrames)
        {
            audioClip.SetData(emptyFrame, samplesPerFrame * offsetFrames);
        }

        public void Clear()
        {
            audioClip.SetData(emptyClip, 0);
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(audioClip);
        }
    }
}