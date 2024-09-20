// Based on: https://github.com/adrenak/univoice-audiosource-output/blob/master/Assets/Adrenak.UniVoice.AudioSourceOutput/Runtime/CircularAudioClip.cs

using System;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat.Output
{
    public class VcAudioClip : IDisposable
    {
        private readonly VcConfig config;

        private readonly AudioClip audioClip;

        private readonly float[] emptySegment;
        private readonly float[] emptyClip;

        public float Length => audioClip.length;

        public VcAudioClip(VcConfig config)
        {
            this.config = config;

            audioClip = AudioClip.Create(nameof(VcAudioClip), config.OutputSampleCount, config.ChannelCount, config.SamplesPerSecond, false);

            emptySegment = new float[config.SamplesPerSegment];
            emptyClip = new float[config.OutputSampleCount];

            var audioSource = config.OutputAudioSource;
            audioSource.loop = true;
            audioSource.clip = audioClip;
        }

        public void WriteSegment(int offsetSegments, float[] segment)
        {
            segment ??= emptySegment;

            if (segment.Length != config.SamplesPerSegment)
            {
                throw new Exception("Voice chat audio clip segment length does not match the config!");
            }

            audioClip.SetData(segment, config.SamplesPerSegment * offsetSegments);
        }

        public int GetOffsetSegments(int segmentIndex)
        {
            return segmentIndex % config.OutputSegmentCount;
        }

        public void ClearSegment(int offsetSegments)
        {
            audioClip.SetData(emptySegment, config.SamplesPerSegment * offsetSegments);
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