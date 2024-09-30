// Based on: https://github.com/adrenak/unimic/blob/master/Assets/UniMic/Runtime/Mic.cs

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Do device list changes at runtime cause any bugs?
// What if someone unplugs a microphone while recording?

namespace Assets.Metater.MetaVoiceChat.Input.Mic
{
    public class VcMic : IDisposable
    {
        private readonly MonoBehaviour coroutineProvider;
        private readonly int samplesPerFrame;

        public bool IsRecording { get; private set; } = false;

        public AudioClip AudioClip { get; private set; }

        public IReadOnlyList<string> Devices => Microphone.devices;
        public int CurrentDeviceIndex { get; private set; } = -1;
        public string CurrentDeviceName
        {
            get
            {
                if (CurrentDeviceIndex < 0 || CurrentDeviceIndex >= Devices.Count)
                {
                    return "";
                }

                return Devices[CurrentDeviceIndex];
            }
        }

        private int nextFrameIndex = 0;
        private int NextFrameIndex => nextFrameIndex++;

        private Coroutine recordCoroutine;

        public event Action<int, float[]> OnFrameReady;

        public VcMic(MonoBehaviour coroutineProvider, int samplesPerFrame)
        {
            this.coroutineProvider = coroutineProvider;
            this.samplesPerFrame = samplesPerFrame;
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
                throw new Exception("No microphone detected for voice chat!");
            }

            if (CurrentDeviceIndex < 0 || CurrentDeviceIndex >= Devices.Count)
            {
                CurrentDeviceIndex = 0;
            }

            StopRecording();
            IsRecording = true;

            AudioClip = Microphone.Start(CurrentDeviceName, true, VcConfig.ClipLoopSeconds, VcConfig.SamplesPerSecond);

            if (AudioClip == null)
            {
                throw new Exception("Microphone failed to start recording for voice chat!");
            }

            if (AudioClip.channels != 1)
            {
                throw new Exception("Microphone must have exactly one channel for voice chat!");
            }

            recordCoroutine = coroutineProvider.StartCoroutine(CoRecord());
        }

        public void StopRecording()
        {
            if (recordCoroutine != null)
            {
                coroutineProvider.StopCoroutine(recordCoroutine);
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
            int i = 0;
            int readAbsPos = 0;
            int prevPos = 0;
            float[] samples = new float[samplesPerFrame];

            while (AudioClip != null && Microphone.IsRecording(CurrentDeviceName))
            {
                bool isNewDataAvailable = true;

                while (isNewDataAvailable)
                {
                    int currPos = Microphone.GetPosition(CurrentDeviceName);
                    if (currPos < prevPos)
                    {
                        i++;
                    }

                    prevPos = currPos;

                    int currAbsPos = i * AudioClip.samples + currPos;
                    int nextReadAbsPos = readAbsPos + samples.Length;

                    if (nextReadAbsPos < currAbsPos)
                    {
                        int offsetSamples = readAbsPos % AudioClip.samples;
                        AudioClip.GetData(samples, offsetSamples);

                        int index = NextFrameIndex;
                        OnFrameReady?.Invoke(index, samples);

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