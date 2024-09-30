using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat.Output
{
    public class VcJitter
    {
        private readonly double timeWindow;
        private readonly int meanOffsetWindow;

        private readonly System.Diagnostics.Stopwatch stopwatch = new();
        private double LocalTimestamp => stopwatch.Elapsed.TotalSeconds;

        private readonly Queue<Entry> entries = new();
        private readonly Queue<double> offsets = new();

        public VcJitter(VcConfig config)
        {
            timeWindow = config.jitterTimeWindow;
            meanOffsetWindow = config.jitterMeanOffsetWindow;
        }

        public float Update(double timestamp)
        {
            if (!stopwatch.IsRunning)
            {
                stopwatch.Restart();
                return 0;
            }

            double localTimestamp = LocalTimestamp;

            {
                entries.Enqueue(new Entry(timestamp, localTimestamp));
                while (entries.TryPeek(out var entry))
                {
                    if (entry.GetAge(localTimestamp) > timeWindow)
                    {
                        entries.Dequeue();
                    }
                    else
                    {
                        break;
                    }
                }
            }

            {
                offsets.Enqueue(localTimestamp - timestamp);
                if (offsets.Count > meanOffsetWindow)
                {
                    offsets.Dequeue();
                }
            }

            double meanOffset = offsets.Average();

            if (entries.Count > 0)
            {
                float SquareDeviation(Entry e)
                {
                    double deviation = meanOffset + e.timestamp - e.localTimestamp;
                    return (float)(deviation * deviation);
                }

                float rmsJitter = Mathf.Sqrt(entries.Average(SquareDeviation));
                return rmsJitter;
            }

            return 0;
        }

        private readonly struct Entry
        {
            public readonly double timestamp;
            public readonly double localTimestamp;

            public Entry(double timestamp, double localTimestamp)
            {
                this.timestamp = timestamp;
                this.localTimestamp = localTimestamp;
            }

            public float GetAge(double localTimestamp)
            {
                return (float)(localTimestamp - this.localTimestamp);
            }
        }
    }
}