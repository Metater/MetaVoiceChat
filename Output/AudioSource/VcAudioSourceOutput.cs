// Influenced by: https://github.com/adrenak/univoice-audiosource-output/blob/master/Assets/Adrenak.UniVoice.AudioSourceOutput/Runtime/UniVoiceAudioSourceOutput.cs

using UnityEngine;

// Would this be a better option? https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnAudioFilterRead.html

namespace Assets.Metater.MetaVoiceChat.Output.AudioSource
{
    public class VcAudioSourceOutput : VcAudioOutput
    {
        [Tooltip("The output audio source.")]
        public UnityEngine.AudioSource audioSource;
        [Tooltip("The time a frame lives in the buffer for before being cleared out. The units are seconds.")]
        [Range(0.1f, 0.75f)]
        public float frameLifetime = 0.5f;
        [Tooltip("The largest magnitude latency considered negative before wrapping around to positive values. The units are seconds.")]
        [Range(0.1f, 0.5f)]
        public float maxNegativeLatency = 0.25f;
        [Tooltip("The proportional gain of the pitch P-controller. The units are percent per second of latency error.")]
        [Range(0, 10)]
        public float pitchProportionalGain = 1;
        [Tooltip("The maximum increase or decrease in pitch allowed for corrections. The units are percent.")]
        [Range(0f, 0.5f)]
        public float pitchMaxCorrection = 0.2f; // Was 0.25 for a while

        private int framesPerSecond;
        private float secondsPerFrame;

        private VcAudioClip vcAudioClip;
        private int[] clipFrameIndicies;

        private int firstFrameIndex = -1;
        private int greatestFrameIndex = -1;

        private readonly System.Diagnostics.Stopwatch frameStopwatch = new();
        private float TimeSincePreviousFrame => (float)frameStopwatch.Elapsed.TotalSeconds;

        private bool isInit = false;

        private float targetLatency;

        private void Start()
        {
            // Unity implements doppler by changing the pitch of the audio clip. This interferes with our purposes.
            audioSource.dopplerLevel = 0;

            var config = metaVc.config;
            framesPerSecond = config.framesPerSecond;
            secondsPerFrame = config.secondsPerFrame;

            vcAudioClip = new(config.samplesPerFrame, config.framesPerClip, audioSource);
            clipFrameIndicies = new int[config.framesPerClip];
            for (int i = 0; i < clipFrameIndicies.Length; i++)
            {
                clipFrameIndicies[i] = -1;
            }
        }

        private void Update()
        {
            // Build up the buffer until the target latency is reached and start playing at the correct clip time
            if (!isInit)
            {
                int receivedFrames;
                if (greatestFrameIndex == -1)
                {
                    receivedFrames = 0;
                }
                else
                {
                    receivedFrames = greatestFrameIndex - firstFrameIndex + 1;
                }

                if (receivedFrames != 0)
                {
                    float timeSinceFirstFrame = ((float)receivedFrames / framesPerSecond) + TimeSincePreviousFrame;
                    if (timeSinceFirstFrame >= targetLatency)
                    {
                        audioSource.time = GetWrappedTime(firstFrameIndex);
                        audioSource.Play();
                        isInit = true;
                    }
                }

                if (!isInit)
                {
                    return;
                }
            }

            float latency = GetLatency();

            // Pause while latency is too low, rebuild the buffer
            // At one point minimum latencies were both zero and also secondsPerFrame * 2
            // I don't think this block is really needed
            //{
            //    float pauseMinimumLatency = 0;
            //    float rebuildMinimumLatency = secondsPerFrame * 2;
            //    if (latency < pauseMinimumLatency)
            //    {
            //        if (audioSource.isPlaying)
            //        {
            //            audioSource.Pause();
            //            Debug.Log("Paused");
            //        }
            //    }
            //    else if (!audioSource.isPlaying)
            //    {
            //        if (latency > rebuildMinimumLatency)
            //        {
            //            audioSource.Play();
            //            Debug.Log("Unpaused");
            //        }
            //    }
            //}

            // Idea: Mirror uses unsymmetrical corrections for NetworkTime, should this?
            // Mirror does this:
            /*
                [Tooltip("Local timeline acceleration in % while catching up.")]
                [Range(0, 1)]
                public double catchupSpeed = 0.02f; // see snap interp demo. 1% is too slow.

                [Tooltip("Local timeline slowdown in % while slowing down.")]
                [Range(0, 1)]
                public double slowdownSpeed = 0.04f; // slow down a little faster so we don't encounter empty buffer (= jitter)
            */

            // Adjust pitch in order to reach target latency
            {
                float error = targetLatency - latency;
                float response = -error * pitchProportionalGain;
                response = Mathf.Clamp(response, -pitchMaxCorrection, pitchMaxCorrection);
                audioSource.pitch = 1f + response;
            }

            ClearOldFrames();
        }

        private void ClearOldFrames()
        {
            for (int i = 0; i < clipFrameIndicies.Length; i++)
            {
                int frameIndex = clipFrameIndicies[i];
                if (frameIndex != -1)
                {
                    int ageFrames = greatestFrameIndex - frameIndex;
                    float ageSeconds = ageFrames * secondsPerFrame;
                    if (ageSeconds > frameLifetime)
                    {
                        vcAudioClip.ClearFrame(i);
                        clipFrameIndicies[i] = -1;
                    }
                }
            }
        }

        private float GetLatency()
        {
            return GetRawLatency() + TimeSincePreviousFrame;
        }

        private float GetRawLatency()
        {
            float writeTime = GetWrappedTime(greatestFrameIndex);
            float readTime = audioSource.time;
            float latency = writeTime - readTime;
            float clipLength = vcAudioClip.Length;

            if (latency < 0)
            {
                latency = clipLength + latency;
            }

            if (clipLength - maxNegativeLatency < latency)
            {
                latency -= clipLength;
            }

            return latency;
        }

        private float GetWrappedTime(int frameIndex)
        {
            return vcAudioClip.GetOffsetFrames(frameIndex) * secondsPerFrame;
        }

        protected override void ReceiveFrame(int index, float[] samples, float targetLatency)
        {
            this.targetLatency = targetLatency;

            int offsetFrames = vcAudioClip.GetOffsetFrames(index);
            vcAudioClip.WriteFrame(offsetFrames, samples);
            clipFrameIndicies[offsetFrames] = index;

            if (firstFrameIndex == -1)
            {
                firstFrameIndex = index;
            }

            if (index > greatestFrameIndex)
            {
                greatestFrameIndex = index;
            }

            frameStopwatch.Restart();
        }

        private void OnDestroy()
        {
            vcAudioClip.Dispose();
        }
    }
}