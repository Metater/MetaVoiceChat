using System.Collections.Generic;

namespace Assets.Metater.MetaVoiceChat.Utils
{
    public static class FixedLengthArrayPool<T>
    {
        private static readonly Dictionary<int, Stack<T[]>> pool = new();

        public static T[] Rent(int length)
        {
            if (!pool.TryGetValue(length, out var stack))
            {
                stack = new Stack<T[]>();
                pool.Add(length, stack);
            }

            if (stack.Count > 0)
            {
                return stack.Pop();
            }

            return new T[length];
        }

        public static void Return(T[] array)
        {
            if (!pool.TryGetValue(array.Length, out var stack))
            {
                stack = new Stack<T[]>();
                pool.Add(array.Length, stack);
            }

            stack.Push(array);
        }
    }
}