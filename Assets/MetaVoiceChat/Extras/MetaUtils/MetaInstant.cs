using System;

namespace Metater
{
    public readonly struct MetaInstant
    {
        private readonly InstantType type;
        private readonly double timeSeconds;

        public MetaInstant(InstantType type, double timeSeconds)
        {
            this.type = type;
            this.timeSeconds = timeSeconds;
        }

        public static MetaInstant Time => new(InstantType.Time, Meta.Time);
        public static MetaInstant Realtime => new(InstantType.Realtime, Meta.Realtime);
        public static MetaInstant FixedTime => new(InstantType.FixedTime, Meta.FixedTime);
        public static MetaInstant FixedRealtime => new(InstantType.FixedRealtime, Meta.FixedRealtime);

        public readonly InstantType Type => type;
        public readonly bool IsInitialized => Type != InstantType.None;
        public readonly double TimeSeconds => timeSeconds;
        public readonly double ElapsedSeconds => type switch
        {
            InstantType.Time => Meta.Time - timeSeconds,
            InstantType.Realtime => Meta.Realtime - timeSeconds,
            InstantType.FixedTime => Meta.FixedTime - timeSeconds,
            InstantType.FixedRealtime => Meta.FixedRealtime - timeSeconds,
            _ => throw new Exception($"{nameof(MetaInstant)} is not initialized!"),
        };
        public readonly float ElapsedSecondsF => (float)ElapsedSeconds;

        public MetaInstant Offset(double offsetSeconds)
        {
            return new(type, timeSeconds + offsetSeconds);
        }

        public double TimeUntilElapsed(double elapsedSeconds)
        {
            return elapsedSeconds - ElapsedSeconds;
        }
        public float TimeUntilElapsedF(double elapsedSeconds) => (float)TimeUntilElapsed(elapsedSeconds);

        public double TimeSinceElapsed(double elapsedSeconds)
        {
            return ElapsedSeconds - elapsedSeconds;
        }
        public float TimeSinceElapsedF(double elapsedSeconds) => (float)TimeSinceElapsed(elapsedSeconds);

        public double TimeUntil(double timeSeconds)
        {
            return timeSeconds - TimeSeconds;
        }
        public float TimeUntilF(double timeSeconds) => (float)TimeUntilF(timeSeconds);

        public double TimeSince(double timeSeconds)
        {
            return TimeSeconds - timeSeconds;
        }
        public float TimeSinceF(double timeSeconds) => (float)TimeSince(timeSeconds);

        public bool IsOutsideCooldown(double cooldownSeconds)
        {
            return !IsInitialized || ElapsedSeconds > cooldownSeconds;
        }

        public bool IsInsideCooldown(double cooldownSeconds)
        {
            return IsInitialized && ElapsedSeconds <= cooldownSeconds;
        }

        public enum InstantType
        {
            None,
            Time,
            Realtime,
            FixedTime,
            FixedRealtime
        }
    }
}