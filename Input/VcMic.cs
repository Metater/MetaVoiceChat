// Based on: https://github.com/adrenak/unimic/blob/master/Assets/UniMic/Runtime/Mic.cs

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat.Input
{
    public class VcMic : IDisposable
    {
        private readonly VcConfig config;

        public bool IsRecording { get; private set; } = false;

        public AudioClip AudioClip { get; private set; }

        public IReadOnlyList<string> Devices => Microphone.devices;
        public int CurrentDeviceIndex { get; private set; } = -1;
        private string CurrentDeviceName
        {
            get
            {
                if (CurrentDeviceIndex < 0 || CurrentDeviceIndex >= Devices.Count)
                {
                    return string.Empty;
                }

                return Devices[CurrentDeviceIndex];
            }
        }

        private int nextFrameIndex = 0;
        private int NextFrameIndex => nextFrameIndex++;

        private Coroutine recordCoroutine;

        public event Action<int, float[]> OnFrameReady;

        public VcMic(VcConfig config)
        {
            this.config = config;
        }

        public void SetDeviceIndex(int index)
        {
            if (index == CurrentDeviceIndex)
            {
                return;
            }

            CurrentDeviceIndex = index;

            if (IsRecording)
            {
                StartRecording();
            }
        }

        public void StartRecording()
        {
            if (Devices.Count <= 0)
            {
                throw new Exception("No voice chat microphone detected!");
            }

            if (CurrentDeviceIndex == -1 && Devices.Count > 0)
            {
                CurrentDeviceIndex = 0;
            }

            StopRecording();
            IsRecording = true;

            AudioClip = Microphone.Start(CurrentDeviceName, true, VcConfig.ClipLoopSeconds, VcConfig.SamplesPerSecond);

            if (AudioClip == null)
            {
                throw new Exception("Voice chat microphone failed to start recording!");
            }

            if (AudioClip.channels != 1)
            {
                throw new Exception("Voice chat microphone must have exactly one channel!");
            }

            recordCoroutine = config.general.coroutineProvider.StartCoroutine(CoRecord());
        }

        public void StopRecording()
        {
            if (recordCoroutine != null)
            {
                config.general.coroutineProvider.StopCoroutine(recordCoroutine);
                recordCoroutine = null;
            }

            IsRecording = false;

            if (!Microphone.IsRecording(CurrentDeviceName))
            {
                return;
            }

            Microphone.End(CurrentDeviceName);
            UnityEngine.Object.Destroy(AudioClip);
            AudioClip = null;
        }

        private IEnumerator CoRecord()
        {
            int loops = 0;
            int readAbsPos = 0;
            int prevPos = 0;
            float[] samples = new float[config.general.samplesPerFrame];

            while (AudioClip != null && Microphone.IsRecording(CurrentDeviceName))
            {
                bool isNewDataAvailable = true;

                while (isNewDataAvailable)
                {
                    int currPos = Microphone.GetPosition(CurrentDeviceName);
                    if (currPos < prevPos)
                    {
                        loops++;
                    }

                    prevPos = currPos;

                    int currAbsPos = loops * AudioClip.samples + currPos;
                    int nextReadAbsPos = readAbsPos + samples.Length;

                    if (nextReadAbsPos < currAbsPos)
                    {
                        int offsetSamples = readAbsPos % AudioClip.samples;
                        AudioClip.GetData(samples, offsetSamples);

                        int frameIndex = NextFrameIndex;
                        OnFrameReady?.Invoke(frameIndex, samples);

                        //if (samples.Max(s => Mathf.Abs(s)) > 0.5f)
                        //{
                        //    Debug.Log($"MIC - Frame: {Time.frameCount}, Offset: {frameIndex % config.OutputSegmentCount}, Time: {Time.realtimeSinceStartupAsDouble}");
                        //}

                        readAbsPos = nextReadAbsPos;
                        isNewDataAvailable = true;
                    }
                    else
                    {
                        isNewDataAvailable = false;
                    }
                }

                yield return null;
            }
        }

        public void Dispose()
        {
            StopRecording();
        }
    }
}