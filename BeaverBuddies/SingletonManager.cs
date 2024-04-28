using System;
using System.Collections.Generic;

namespace BeaverBuddies
{
    public static class SingletonManager
    {
        private static Dictionary<Type, object> map = new Dictionary<Type, object>();

        public static void Reset()
        {
            Plugin.Log("Resetting SingletonManager");
            foreach (object obj in map.Values)
            {
                if (obj is IResettableSingleton)
                {
                    ((IResettableSingleton)obj).Reset();
                }
            }
            map.Clear();
        }

        public static T RegisterSingleton<T>(T singleton)
        {
            Type type = singleton.GetType();
            if (map.ContainsKey(type))
            {
                Plugin.LogWarning($"Singleton of type {type} already registered");
                map.Remove(type);
            }
            map.Add(type, singleton);
            return singleton;
        }

        /// <summary>
        /// Gets the singleton of the requested type, or null if it is not present.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static T GetSingleton<T>()
        {
            Type t = typeof(T);
            return map.ContainsKey(t) ? (T)map[t] : default(T);
        }
    }

    /// <summary>
    /// Singleton which has static fields that need to be manually
    /// reset when unloading the map. This is appropriate in the case
    /// of fields that are frequently accessed from patched methods,
    /// where a map lookup would be inefficient, or for static fields
    /// which might be accessed as the map is loading, before the
    /// singleton is registered.
    /// </summary>
    public interface IResettableSingleton
    {
        void Reset();
    }

    public class RegisteredSingleton
    { 
        public RegisteredSingleton()
        {
            SingletonManager.RegisterSingleton(this);
        }
    }
}
