using System;
using System.Collections.Generic;
using System.Text;

namespace TimberModTest
{
    public class SingletonManager
    {
        private static Dictionary<Type, object> map = new Dictionary<Type, object>();

        public static void Reset()
        {
            foreach (object obj in map.Values)
            {
                if (obj is IResettableSingleton)
                {
                    ((IResettableSingleton)obj).Reset();
                }
            }
            map.Clear();
        }

        public static T Register<T>(T singleton)
        {
            Type type = singleton.GetType();
            if (map.ContainsKey(type))
            {
                throw new Exception($"Singleton of type {type} already registered");
            }
            map.Add(type, singleton);
            return singleton;
        }

        public static T GetSingleton<T>()
        {
            Type type = typeof(T);
            if (map.ContainsKey(type))
            {
                return (T)map[type];
            }
            throw new Exception($"Singleton of type {type} not registered");
        }

        /// <summary>
        /// Shorthand for GetSingleton
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T S<T>()
        {
            return GetSingleton<T>();
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
}
