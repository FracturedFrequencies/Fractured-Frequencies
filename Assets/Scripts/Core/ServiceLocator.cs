using System;
using System.Collections.Generic;

namespace FracturedFrequencies.Core
{
    /// <summary>
    /// Minimal global service registry. Register long-lived systems once (in the bootstrapper)
    /// and resolve them anywhere via <see cref="Get{T}"/> instead of using singletons.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new();

        public static void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            var type = typeof(T);
            if (Services.ContainsKey(type))
                throw new InvalidOperationException($"Service of type {type.Name} is already registered.");

            Services[type] = service;
        }

        public static void Unregister<T>() where T : class => Services.Remove(typeof(T));

        public static T Get<T>() where T : class
        {
            if (Services.TryGetValue(typeof(T), out var service))
                return (T)service;

            throw new InvalidOperationException(
                $"Service of type {typeof(T).Name} is not registered. Did the GameBootstrapper run?");
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out var obj))
            {
                service = (T)obj;
                return true;
            }

            service = null;
            return false;
        }

        public static bool IsRegistered<T>() where T : class => Services.ContainsKey(typeof(T));

        /// <summary>Removes all services. Called automatically before each play session.</summary>
        public static void Reset() => Services.Clear();
    }
}
