/* * * * *
 * XorShift64 implementation
 * ------------------------------
 * 
 * It provides a pseudo random number generator with a 64bit internal state and
 * a range of 2^64-1. It also provides some convenient methods for requesting a
 * value from a certain range.
 * 
 * In addition due to alignment issues when using modulo it also provides a
 * FairRange implementation to actually provide unique distribution
 * 
 * 
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016-2018 Markus Göbel (Bunny83)
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 * 
 * * * * */

// https://github.com/Bunny83/Utilities/blob/master/XorShift64.cs

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Metater
{
    public class MetaRandom
    {
        #region Original
        protected static ulong SEED_OFFSET = 13726359678912485784UL;
        protected static double DOUBLE_MUL = 5.42101086242752E-20;
        protected ulong state;

        public ulong Seed
        {
            get { return state ^ SEED_OFFSET; }
            set
            {
                state = value ^ SEED_OFFSET;
                if (state == 0UL)
                    state = SEED_OFFSET;
            }
        }

        public MetaRandom()
        {
            state = (ulong)System.DateTime.Now.Ticks ^ SEED_OFFSET;
            Next();
            state ^= (ulong)System.Diagnostics.Stopwatch.GetTimestamp() << 7;
            Next();
            state ^= (ulong)System.Diagnostics.Stopwatch.GetTimestamp() << 11;
            if (state == 0UL)
                state = SEED_OFFSET;
        }
        public MetaRandom(ulong seed)
        {
            Seed = seed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong Next()
        {
            // https://en.wikipedia.org/wiki/Xorshift#xorshift.2A
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            return state * 2685821657736338717UL; // 0x2545F4914F6CDD1DUL
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double NextDouble()
        {
            return (double)Next() * DOUBLE_MUL;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong Range(ulong minInclusive, ulong maxExclusive)
        {
            return minInclusive + Next() % (maxExclusive - minInclusive);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Range(int minInclusive, int maxExclusive)
        {
            return (int)Range((long)minInclusive, (long)maxExclusive);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Range(long minInclusive, long maxExclusive)
        {
            return minInclusive + (long)(Next() % (ulong)(maxExclusive - minInclusive));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double Range(double minInclusive, double maxInclusive)
        {
            return minInclusive + NextDouble() * (maxInclusive - minInclusive);
        }
        // corrects bit alignment which might shift the probability slightly to the
        // lower numbers based on the choosen range.
        public ulong FairRange(ulong maxExclusive)
        {
            ulong dif = ulong.MaxValue % maxExclusive;
            // if aligned or range too big, just pick a number
            if (dif == 0 || ulong.MaxValue / (maxExclusive / 4UL) < 2UL)
                return Next() % maxExclusive;
            ulong v = Next();
            // avoid the last incomplete set
            while (ulong.MaxValue - v < dif)
                v = Next();
            return v % maxExclusive;
        }
        public ulong FairRange(ulong minInclusive, ulong maxExclusive)
        {
            return minInclusive + FairRange(maxExclusive - minInclusive);
        }
        #endregion

        #region Extensions
        public bool Bool => Next() % 2 == 1;
        public float Float => (float)NextDouble();
        public double Double => NextDouble();
        public ulong ULong => Next();

        public T Select<T>(IList<T> list)
        {
            return list[Range(0, list.Count)];
        }

        public T Take<T>(IList<T> list)
        {
            int i = Range(0, list.Count);
            var value = list[i];
            list.RemoveAt(i);
            return value;
        }

        // https://stackoverflow.com/a/1262619
        public void Shuffle<T>(IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = Range(0, n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }
        }

        public float Range(MetaRangeFloat rangeFloat)
        {
            return (float)Range(rangeFloat.min, rangeFloat.max);
        }

        public int Range(MetaRangeInt rangeInt)
        {
            return Range(rangeInt.minInclusive, rangeInt.maxInclusive + 1);
        }

        public float CardinalDegrees => (Next() % 4) switch
        {
            0 => 0f,
            1 => 90f,
            2 => 180f,
            _ => 270f,
        };
        #endregion
    }
}