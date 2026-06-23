using System;
using UnityEngine;

namespace Metater
{
    public readonly struct MetaTimer
    {
        private readonly double lengthSeconds;
        private readonly MetaInstant instant;

        private MetaTimer(double lengthSeconds, MetaInstant instant)
        {
            this.lengthSeconds = lengthSeconds;
            this.instant = instant;
        }

        public static MetaTimer SetForTime(double lengthSeconds)
        {
            return new(lengthSeconds, MetaInstant.Time);
        }
        public static MetaTimer SetForRealtime(double lengthSeconds)
        {
            return new(lengthSeconds, MetaInstant.Realtime);
        }
        public static MetaTimer SetForFixedTime(double lengthSeconds)
        {
            return new(lengthSeconds, MetaInstant.FixedTime);
        }
        public static MetaTimer SetForFixedRealtime(double lengthSeconds)
        {
            return new(lengthSeconds, MetaInstant.FixedRealtime);
        }

        public static MetaTimer SetUntilTime(double seconds)
        {
            double lengthSeconds = seconds - Meta.Time;
            return SetForTime(lengthSeconds);
        }
        public static MetaTimer SetUntilRealtime(double seconds)
        {
            double lengthSeconds = seconds - Meta.Realtime;
            return SetForRealtime(lengthSeconds);
        }
        public static MetaTimer SetUntilFixedTime(double seconds)
        {
            double lengthSeconds = seconds - Meta.FixedTime;
            return SetForFixedTime(lengthSeconds);
        }
        public static MetaTimer SetUntilFixedRealtime(double seconds)
        {
            double lengthSeconds = seconds - Meta.FixedRealtime;
            return SetForFixedRealtime(lengthSeconds);
        }

        public readonly MetaTimer Elapse()
        {
            Debug.Assert(HasElapsed);

            return instant.Type switch
            {
                MetaInstant.InstantType.Time => new(LengthSeconds, MetaInstant.Time.Offset(OverflowSeconds)),
                MetaInstant.InstantType.Realtime => new(LengthSeconds, MetaInstant.Realtime.Offset(OverflowSeconds)),
                MetaInstant.InstantType.FixedTime => new(LengthSeconds, MetaInstant.FixedTime.Offset(OverflowSeconds)),
                MetaInstant.InstantType.FixedRealtime => new(LengthSeconds, MetaInstant.FixedRealtime.Offset(OverflowSeconds)),
                _ => throw new Exception($"{nameof(MetaInstant)} is not initialized!"),
            };
        }

        public static bool Poll(ref MetaTimer timer, out bool hasElapsed)
        {
            hasElapsed = timer.HasElapsed;
            if (hasElapsed)
            {
                timer = timer.Elapse();
            }

            return true;
        }

        public readonly bool IsInitialized => instant.IsInitialized;
        public readonly double LengthSeconds => lengthSeconds;
        public readonly float LengthSecondsF => (float)LengthSeconds;
        public readonly double StartSeconds => instant.TimeSeconds;
        public readonly double EndSeconds => StartSeconds + LengthSeconds;
        public readonly double ElapsedSeconds => instant.ElapsedSeconds;
        public readonly float ElapsedSecondsF => (float)ElapsedSeconds;
        public readonly double RemainingSeconds => LengthSeconds - ElapsedSeconds;
        public readonly float RemainingSecondsF => (float)RemainingSeconds;
        public readonly float Progress => (float)(ElapsedSeconds / LengthSeconds);
        public readonly float ProgressClamped => Mathf.Clamp01(Progress);
        public readonly float RemainingProgress => (float)(RemainingSeconds / LengthSeconds);
        public readonly float RemainingProgressClamped => Mathf.Clamp01(RemainingProgress);
        public readonly bool HasElapsed => ElapsedSeconds >= LengthSeconds;
        public readonly bool IsRunning => !HasElapsed;
        public readonly double OverflowSeconds => ElapsedSeconds - LengthSeconds;
        public readonly float OverflowSecondsF => (float)OverflowSeconds;
    }
}