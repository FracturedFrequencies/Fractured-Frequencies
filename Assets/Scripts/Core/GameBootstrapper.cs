using UnityEngine;

namespace FracturedFrequencies.Core
{
    /// <summary>
    /// Creates all global systems automatically before the first scene loads,
    /// no matter which scene you press Play in. No bootstrap scene required.
    /// Add new global services in <see cref="RegisterServices"/>.
    /// </summary>
    public static class GameBootstrapper
    {
        // Runs first: reset static state (needed when "Enter Play Mode Options" skips domain reload).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ServiceLocator.Reset();
            EventBusRegistry.ClearAll();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            var systemsRoot = new GameObject("[Systems]");
            Object.DontDestroyOnLoad(systemsRoot);

            RegisterServices(systemsRoot);
        }

        private static void RegisterServices(GameObject systemsRoot)
        {
            ServiceLocator.Register(systemsRoot.AddComponent<GameManager>());

            // Add future global systems here, e.g.:
            // ServiceLocator.Register(systemsRoot.AddComponent<AudioManager>());
        }
    }
}
