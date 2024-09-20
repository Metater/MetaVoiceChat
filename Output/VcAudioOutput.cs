// Influenced by: https://github.com/adrenak/univoice-audiosource-output/blob/master/Assets/Adrenak.UniVoice.AudioSourceOutput/Runtime/UniVoiceAudioSourceOutput.cs

using System;
using System.Collections;
using System.Text;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat.Output
{
    public class VcAudioOutput : IDisposable
    {
        private readonly VcConfig config;
        private readonly AudioSource audioSource;

        private readonly VcAudioClip vcAudioClip;
        private readonly int[] clipSegmentIndicies;
        private readonly Coroutine updateCoroutine;

        private int firstSegmentIndex = -1;
        private int greatestSegmentIndex = -1;

        private readonly System.Diagnostics.Stopwatch segmentStopwatch = new();
        private float TimeSinceSegment => (float)segmentStopwatch.Elapsed.TotalSeconds;

        private readonly StringBuilder csv = new();

        public VcAudioOutput(VcConfig config)
        {
            csv.AppendLine("Time,Error");

            this.config = config;
            audioSource = config.OutputAudioSource;

            vcAudioClip = new(config);
            clipSegmentIndicies = new int[config.OutputSegmentCount];
            for (int i = 0; i < clipSegmentIndicies.Length; i++)
            {
                clipSegmentIndicies[i] = -1;
            }

            updateCoroutine = config.CoroutineProvider.StartCoroutine(CoUpdate());
        }

        private IEnumerator CoUpdate()
        {
            while (true)
            {
                int receivedSegments;
                if (greatestSegmentIndex == -1)
                {
                    receivedSegments = 0;
                }
                else
                {
                    receivedSegments = greatestSegmentIndex - firstSegmentIndex + 1;

                    // Build up buffer extra before starting output
                    //receivedSegments -= 2;
                    //if (receivedSegments < 0)
                    //{
                    //    receivedSegments = 0;
                    //}
                }

                if (receivedSegments != 0)
                {
                    float timeSinceFirstSegment = ((float)receivedSegments / config.SegmentsPerSecond) + TimeSinceSegment;
                    float targetLatency = (float)config.OutputLagSegmentsTarget / config.SegmentsPerSecond;
                    if (timeSinceFirstSegment >= targetLatency)
                    {
                        audioSource.time = GetWrappedTime(firstSegmentIndex);
                        audioSource.Play();
                        break;
                    }
                }

                yield return null;
            }

            while (true)
            {
                float latency = GetLatency();

                // Pause while latency is negative, rebuild the buffer
                if (latency < 0)
                {
                    if (audioSource.isPlaying)
                    {
                        audioSource.Pause();
                        Debug.Log("Paused");
                    }
                }
                else if (!audioSource.isPlaying)
                {
                    audioSource.Play();
                    Debug.Log("Unpaused");
                }

                // Adjust pitch in order to reach target segment lag
                {
                    float latencySegments = latency * config.OutputSegmentCount;
                    float errorSegments = config.OutputLagSegmentsTarget - latencySegments;

                    {
                        float error = errorSegments * config.SegmentPeriodMs;
                        //ema.Add(error);
                        //csv.AppendLine(Time.time + "," + ema.Value);
                        csv.AppendLine(Time.time + "," + error);
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

                        float response = -errorSegments * config.OutputPitchP;
                        response = Mathf.Clamp(response, -0.25f, 0.25f);
                        audioSource.pitch = 1f + response;
                    }
                }

                ClearOldSegments();

                yield return null;
            }
        }

        private void ClearOldSegments()
        {
            for (int i = 0; i < clipSegmentIndicies.Length; i++)
            {
                int segmentIndex = clipSegmentIndicies[i];
                if (segmentIndex != -1)
                {
                    int age = greatestSegmentIndex - segmentIndex;
                    if (age > config.OutputSegmentLifetime)
                    {
                        vcAudioClip.ClearSegment(i);
                        clipSegmentIndicies[i] = -1;
                    }
                }
            }

            //int writeOffsetSegments = vcAudioClip.GetOffsetSegments(greatestSegmentIndex);
            //int readOffsetSegments = audioSource.timeSamples / config.SamplesPerSegment;
            //int expectedSegmentIndex = greatestSegmentIndex;

            //int i = writeOffsetSegments;
            //while (i != readOffsetSegments)
            //{
            //    int segmentIndex = clipSegmentIndicies[i];
            //    if (segmentIndex != -1 && segmentIndex != expectedSegmentIndex)
            //    {
            //        vcAudioClip.ClearSegment(i);
            //        clipSegmentIndicies[i] = -1;
            //    }

            //    expectedSegmentIndex--;

            //    i--;
            //    if (i < 0)
            //    {
            //        i = config.OutputSegmentCount - 1;
            //    }
            //}
        }

        private float GetLatency()
        {
            return GetRawLatency() + TimeSinceSegment;
        }

        private float GetRawLatency()
        {
            float writeTime = GetWrappedTime(greatestSegmentIndex);
            float readTime = audioSource.time;
            float latency = writeTime - readTime;
            float clipLength = vcAudioClip.Length;

            if (latency < 0)
            {
                latency = clipLength + latency;
            }

            if (clipLength - config.OutputMaxNegativeLatency < latency)
            {
                latency -= clipLength;
            }

            return latency;
        }

        private float GetWrappedTime(int segmentIndex)
        {
            return (float)vcAudioClip.GetOffsetSegments(segmentIndex) * config.SegmentPeriodMs / 1000f;
        }

        public void FeedSegment(int segmentIndex, float[] segment = null)
        {
            int offsetSegments = vcAudioClip.GetOffsetSegments(segmentIndex);
            vcAudioClip.WriteSegment(offsetSegments, segment);
            clipSegmentIndicies[offsetSegments] = segmentIndex;

            if (firstSegmentIndex == -1)
            {
                firstSegmentIndex = segmentIndex;
            }

            if (segmentIndex > greatestSegmentIndex)
            {
                greatestSegmentIndex = segmentIndex;
            }

            segmentStopwatch.Restart();
        }

        public void Dispose()
        {
            vcAudioClip.Dispose();
            config.CoroutineProvider.StopCoroutine(updateCoroutine);

            Debug.Log(csv.ToString());
        }
    }
}