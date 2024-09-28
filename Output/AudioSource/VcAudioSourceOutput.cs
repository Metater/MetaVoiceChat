// Influenced by: https://github.com/adrenak/univoice-audiosource-output/blob/master/Assets/Adrenak.UniVoice.AudioSourceOutput/Runtime/UniVoiceAudioSourceOutput.cs

using Assets.Metater.MetaUtils;
using UnityEngine;

// Would this be a better option? https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnAudioFilterRead.html

namespace Assets.Metater.MetaVoiceChat.Output.AudioSource
{
    public class VcAudioSourceOutput : VcAudioOutput
    {
        [Tooltip("The output audio source.")]
        public UnityEngine.AudioSource audioSource;
        [Tooltip("The time a frame lives in the buffer for before being cleared.")]
        public float frameLifetimeSeconds = 0.5f;
        [Tooltip("The maximum negative latency allowed before pausing the audio source to rebuild the buffer.")]
        public float maxNegativeLatency = 0.25f;
        [Tooltip("The proportional gain of the pitch controller. The units are percent per second of latency error.")]
        public float pitchProportionalGain = 1;

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

        private readonly MetaCsv csv = new("Time", "Error");
        private readonly MetaCsv latencyCsv = new("Time", "Latency");

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
            //print(targetLatency);
            //targetLatency = targetLatencyOverride;

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

            latencyCsv.AddRow(Time.time, targetLatency * 1000);
            //print((int)(latency * 1000));

            {
                //// Pause while latency is too low, rebuild the buffer
                //float minimumLatency = secondsPerFrame * 2;
                //if (latency < minimumLatency)
                //{
                //    if (audioSource.isPlaying)
                //    {
                //        audioSource.Pause();
                //        Debug.Log("Paused");
                //    }
                //}
                //else if (!audioSource.isPlaying)
                //{
                //    if (latency > minimumLatency)
                //    {
                //        audioSource.Play();
                //        Debug.Log("Unpaused");
                //    }
                //}
            }

            // Adjust pitch in order to reach target segment lag
            {
                float error = targetLatency - latency;

                {
                    //ema.Add(error);
                    //csv.AppendLine(Time.time + "," + ema.Value);
                    csv.AddRow(Time.time, error * 1000);
                    //csv.AppendLine(Time.time + "," + GetRawLatency());
                }

                //if (latencySegments >= config.OutputLagSegmentsRange.x && latencySegments <= config.OutputLagSegmentsRange.y)
                //{
                //    audioSource.pitch = 1f;

                //    //Debug.Log("Good");
                //}
                //else
                {
                    //Debug.Log(errorSegments);

                    // Artificially increase the error to more quickly remove steady-state error
                    //errorSegments += Mathf.Sign(errorSegments) * config.OutputErrorBias;

                    //errorSegments = (float)ema.Value / config.SegmentPeriodMs;

                    float response = -error * pitchProportionalGain;
                    response = Mathf.Clamp(response, -0.25f, 0.25f);
                    audioSource.pitch = 1f + response;
                }
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
                    if (ageSeconds > frameLifetimeSeconds)
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

            csv.Dispose();
            latencyCsv.Dispose();
        }
    }
}