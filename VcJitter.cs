using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat
{
    public class VcLocalJitter
    {
        private readonly System.Diagnostics.Stopwatch stopwatch = new();
        public double TimeSinceStart => stopwatch.Elapsed.TotalSeconds;

        public VcLocalJitter()
        {
            stopwatch.Restart();
        }
    }

    public class VcRemoteJitter
    {
        private readonly double window;
        private readonly float defaultJitter;

        private readonly System.Diagnostics.Stopwatch stopwatch = new();
        private float TimeSinceUpdate => (float)stopwatch.Elapsed.TotalSeconds;

        private readonly Queue<Diff> diffs = new();

        private double previousTime;

        public VcRemoteJitter(double window, float defaultJitter)
        {
            this.window = window;
            this.defaultJitter = defaultJitter;
        }

        public float Update(double time)
        {
            if (!stopwatch.IsRunning)
            {
                stopwatch.Restart();
                previousTime = time;
                return defaultJitter;
            }

            float diff = (float)(time - previousTime) - TimeSinceUpdate;
            diffs.Enqueue(new Diff(time, diff));


            if (time > previousTime)
            {
                previousTime = time;
            }

            return CalculateRmsJitter(time);
        }

        private float CalculateRmsJitter(double time)
        {
            RemoveOldDiffs(time);
            stopwatch.Restart();

            if (diffs.Count > 0)
            {
                float rmsJitter = Mathf.Sqrt(diffs.Average(d => d.diff * d.diff));
                return rmsJitter;
            }

            return defaultJitter;
        }

        private void RemoveOldDiffs(double time)
        {
            while (diffs.TryPeek(out var diff))
            {
                if (diff.GetAge(time) > window)
                {
                    diffs.Dequeue();
                }
                else
                {
                    break;
                }
            }
        }

        private readonly struct Diff
        {
            private readonly double enqueueTime;
            public readonly float diff;

            public Diff(double enqueueTime, float diff)
            {
                this.enqueueTime = enqueueTime;
                this.diff = diff;
            }

            public float GetAge(double time)
            {
                return (float)(time - enqueueTime);
            }
        }
    }
}