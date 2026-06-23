using System;
using System.Collections.Generic;
using UnityEngine;

namespace Metater
{
    public static class MetaCache
    {
        private static readonly Dictionary<Type, UnityEngine.Object> objectCache = new();

        public static T Object<T>() where T : UnityEngine.Object
        {
            Type type = typeof(T);
            if (objectCache.TryGetValue(type, out var obj) && obj != null)
            {
                return (T)obj;
            }

            T objTyped = UnityEngine.Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            objectCache[type] = objTyped;
            return objTyped;
        }
    }
}