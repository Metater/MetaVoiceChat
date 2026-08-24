using System;
using System.Runtime.InteropServices;

namespace MetaVoiceChat.Utils
{
    public class UnmanagedFloatArray
    {
        private float[] array = null;
        private IntPtr ptr = IntPtr.Zero;

        public void Fill(ArraySegment<float> segment)
        {
            GetOrInit(segment.Count, out _, false);
            Array.Copy(segment.Array, segment.Offset, array, 0, segment.Count);
            WriteToUnmanaged();
        }

        public IntPtr GetOrInit(int length, out float[] array, bool initToZero = true)
        {
            if (this.array == null || this.array.Length != length)
            {
                Free();
                this.array = new float[length];

                ptr = Marshal.AllocHGlobal(length * sizeof(float));
                if (initToZero)
                {
                    WriteToUnmanaged();
                }
            }

            array = this.array;
            return ptr;
        }

        public void Zero()
        {
            if (ptr == IntPtr.Zero)
            {
                return;
            }

            Array.Clear(array, 0, array.Length);
            WriteToUnmanaged();
        }

        public void WriteToUnmanaged()
        {
            if (ptr == IntPtr.Zero)
            {
                return;
            }

            Marshal.Copy(array, 0, ptr, array.Length);
        }

        public void ReadFromUnmanaged(int amount = -1)
        {
            if (ptr == IntPtr.Zero)
            {
                return;
            }

            if (amount == -1)
            {
                amount = array.Length;
            }

            Marshal.Copy(ptr, array, 0, amount);
        }

        public void Free()
        {
            if (ptr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(ptr);
                ptr = IntPtr.Zero;

                if (array != null)
                {
                    array = null;
                }
            }
        }
    }
}
