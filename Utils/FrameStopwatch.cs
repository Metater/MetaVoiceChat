using UnityEngine;

namespace Assets.Metater.MetaVoiceChat.Utils
{
    public class FrameStopwatch
    {
        private readonly System.Diagnostics.Stopwatch stopwatch = new();

        private int currentFrame = -1;
        private int warningFrame = -1;

        public void Start()
        {
            if (Time.frameCount != currentFrame)
            {
                currentFrame = Time.frameCount;
                stopwatch.Restart();
            }
            else
            {
                stopwatch.Start();
            }
        }

        public void Stop(float warningMs, string warningMessage, bool shouldReset, bool allowMultipleWarningsPerFrame)
        {
            if (shouldReset)
            {
                Reset();
                return;
            }

            stopwatch.Stop();

            float elapsedMs = (float)stopwatch.Elapsed.TotalMilliseconds;
            if (elapsedMs < warningMs)
            {
                return;
            }

            if (currentFrame == warningFrame && !allowMultipleWarningsPerFrame)
            {
                return;
            }

            Debug.LogWarning($"{warningMessage}\nElapsed ms of {elapsedMs} is greater than warning ms of {warningMs} on frame {currentFrame}.");
            warningFrame = currentFrame;
        }

        public void Reset()
        {
            stopwatch.Stop();
            stopwatch.Reset();

            currentFrame = -1;
            warningFrame = -1;
        }
    }
}