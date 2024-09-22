using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat
{
    public class VcLocalJitter
    {
        private readonly System.Diagnostics.Stopwatch stopwatch = new();
        public double Timestamp => stopwatch.Elapsed.TotalSeconds;

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
        private float TimeSincePreviousUpdate => (float)stopwatch.Elapsed.TotalSeconds;

        private readonly Queue<Diff> diffs = new();

        private double previousTimestamp;

        public VcRemoteJitter(double window, float defaultJitter)
        {
            this.window = window;
            this.defaultJitter = defaultJitter;
        }

        public float Update(double timestamp)
        {
            if (!stopwatch.IsRunning)
            {
                stopwatch.Restart();
                previousTimestamp = timestamp;
                return defaultJitter;
            }

            float diff = (float)(timestamp - previousTimestamp) - TimeSincePreviousUpdate;
            diffs.Enqueue(new Diff(timestamp, diff));


            if (timestamp > previousTimestamp)
            {
                previousTimestamp = timestamp;
            }

            return CalculateRmsJitter(timestamp);
        }

        private float CalculateRmsJitter(double timestamp)
        {
            RemoveOldDiffs(timestamp);
            stopwatch.Restart();

            if (diffs.Count > 0)
            {
                float rmsJitter = Mathf.Sqrt(diffs.Average(d => d.diff * d.diff));
                return rmsJitter;
            }

            return defaultJitter;
        }

        private void RemoveOldDiffs(double timestamp)
        {
            while (diffs.TryPeek(out var diff))
            {
                if (diff.GetAge(timestamp) > window)
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
            private readonly double enqueueTimestamp;
            public readonly float diff;

            public Diff(double enqueueTimestamp, float diff)
            {
                this.enqueueTimestamp = enqueueTimestamp;
                this.diff = diff;
            }

            public float GetAge(double timestamp)
            {
                return (float)(timestamp - enqueueTimestamp);
            }
        }
    }
}