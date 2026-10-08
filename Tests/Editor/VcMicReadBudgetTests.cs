#if UNITY_EDITOR && META_VOICE_CHAT_TESTS
using System;
using MetaVoiceChat.Input.Mic;
using NUnit.Framework;

namespace MetaVoiceChat.Tests.Editor
{
    /// <summary>Drives VcMic's read budget the way CoRecord does, against a simulated capture device.</summary>
    public sealed class VcMicReadBudgetTests
    {
        private const double PassSeconds = 1.0 / 60;
        private const double RunSeconds = 10;

        // A pass leaves at most one unread frame, because a frame is read only once the device is past its end.
        // The second frame is slack.
        private const int MaxLagFrames = 2;

        /// <summary>
        /// A reader for a capture ring of one second. Like CoRecord, it sees the device position modulo the ring and
        /// counts a wrap at most once per pass, so it undercounts when the device advances a ring or more between passes.
        /// </summary>
        private sealed class Reader
        {
            private readonly int ring;
            private readonly int frameSamples;
            private double budget;
            private double now;
            private int wraps;
            private int prevPos;
            private int readAbsPos;

            public int FramesRead;

            public Reader(int clipRate = 48_000, int frameSamples = 960)
            {
                ring = clipRate;
                this.frameSamples = frameSamples;
                budget = clipRate;
            }

            /// <summary>The true device position after <paramref name="seconds"/> at <paramref name="speed"/> times real time.</summary>
            public long DevicePos(double seconds, double speed = 1) => (long)(seconds * ring * speed);

            /// <summary>How far the read position is behind the device, measured around the ring.</summary>
            public int LagAround(long devicePos) => (int)(((devicePos % ring) - (readAbsPos % ring) + ring) % ring);

            /// <summary>One CoRecord pass at <paramref name="at"/> seconds. Returns the frames read.</summary>
            public int Pass(double at, long devicePos)
            {
                budget = VcMic.EditorTestAdapter.Refill(budget, at - now, ring, ring);
                now = at;
                int pos = (int)(devicePos % ring);
                int frames = 0;
                while (true)
                {
                    if (pos < prevPos)
                    {
                        wraps++;
                    }

                    prevPos = pos;
                    int currAbsPos = wraps * ring + pos;
                    readAbsPos = VcMic.EditorTestAdapter.Resync(readAbsPos, currAbsPos, ring, frameSamples);
                    int next = readAbsPos + frameSamples;
                    if (next >= currAbsPos || budget < frameSamples)
                    {
                        break;
                    }

                    readAbsPos = next;
                    budget -= frameSamples;
                    frames++;
                }

                FramesRead += frames;
                return frames;
            }
        }

        [Test]
        public void Resync_SkipsSamplesMoreThanOneRingBehind()
        {
            Assert.That(VcMic.EditorTestAdapter.Resync(0, 4 * 48_000, 48_000, 960), Is.EqualTo(3 * 48_000 + 960));
        }

        [Test]
        public void Resync_TrimsOnlyOnceTheBacklogPassesTheRingLessTheMargin()
        {
            int curr = 3 * 48_000;
            int atTheEdge = curr - (48_000 - 960);

            Assert.That(VcMic.EditorTestAdapter.Resync(atTheEdge, curr, 48_000, 960), Is.EqualTo(atTheEdge));
            Assert.That(VcMic.EditorTestAdapter.Resync(atTheEdge - 1, curr, 48_000, 960), Is.EqualTo(atTheEdge));
        }

        [Test]
        public void Refill_IsCappedAtOneRing()
        {
            Assert.That(VcMic.EditorTestAdapter.Refill(0, 10, 48_000, 48_000), Is.EqualTo(48_000));
        }

        [TestCase(48_000, 960)]
        [TestCase(96_000, 960)]
        [TestCase(44_100, 882)]
        public void PacedDevice_IsKeptUpWith(int clipRate, int frameSamples)
        {
            Reader reader = new(clipRate, frameSamples);
            for (int step = 1; step <= RunSeconds / PassSeconds; step++)
            {
                double at = step * PassSeconds;
                long devicePos = reader.DevicePos(at);
                reader.Pass(at, devicePos);
                Assert.That(reader.LagAround(devicePos), Is.LessThanOrEqualTo(MaxLagFrames * frameSamples), $"behind at {at:F2} s");
            }
        }

        [TestCase(0.6)]
        [TestCase(1.5)]
        [TestCase(1.9)]
        [TestCase(3.2)]
        public void Freeze_IsReadBackWithoutLingeringLag(double freezeSeconds)
        {
            // Freezes shorter and longer than the one second ring. Past one ring the wrap count is short, so the lag is
            // measured around the ring.
            double freezeStart = 3;
            Reader reader = new();
            for (int step = 1; step <= 9 / PassSeconds; step++)
            {
                double at = step * PassSeconds;
                if (at >= freezeStart && at < freezeStart + freezeSeconds)
                {
                    continue;
                }

                long devicePos = reader.DevicePos(at);
                reader.Pass(at, devicePos);
                if (at >= freezeStart + freezeSeconds + 1)
                {
                    Assert.That(reader.LagAround(devicePos), Is.LessThanOrEqualTo(MaxLagFrames * 960), $"behind at {at:F2} s");
                }
            }
        }

        [Test]
        public void RepeatedFreezes_AreEachReadBackWithoutLingeringLag()
        {
            // Three one second freezes, half a second apart. The budget has to be there for each one because it
            // refills during the freeze itself.
            (double start, double end)[] freezes = { (3, 4), (4.5, 5.5), (6, 7) };
            Reader reader = new();
            for (int step = 1; step <= 12 / PassSeconds; step++)
            {
                double at = step * PassSeconds;
                if (Array.Exists(freezes, f => at >= f.start && at < f.end))
                {
                    continue;
                }

                long devicePos = reader.DevicePos(at);
                reader.Pass(at, devicePos);
                Assert.That(reader.LagAround(devicePos), Is.LessThanOrEqualTo(MaxLagFrames * 960), $"behind at {at:F2} s");
            }
        }

        // 60 times real time is exactly one ring per pass, which the wrap count cannot see at all.
        [TestCase(48_000, 960, 20)]
        [TestCase(48_000, 960, 60)]
        [TestCase(48_000, 960, 200)]
        [TestCase(96_000, 960, 20)]
        public void DeviceFasterThanRealTime_IsReadAtMostAboutRealTime(int clipRate, int frameSamples, int speed)
        {
            Reader reader = new(clipRate, frameSamples);
            int mostInOnePass = 0;
            for (int step = 1; step <= RunSeconds / PassSeconds; step++)
            {
                double at = step * PassSeconds;
                mostInOnePass = Math.Max(mostInOnePass, reader.Pass(at, reader.DevicePos(at, speed)));
            }

            double ringFrames = (double)clipRate / frameSamples;
            Assert.That(reader.FramesRead, Is.LessThanOrEqualTo(ringFrames + 1.15 * RunSeconds * ringFrames), "no faster than real time plus drift");
            Assert.That(mostInOnePass, Is.LessThanOrEqualTo(ringFrames), "no pass reads more than one ring");
        }
    }
}
#endif
