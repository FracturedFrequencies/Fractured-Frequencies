using System.Collections.Generic;
using UnityEngine;

namespace FracturedFrequencies.Gravity
{
    /// <summary>
    /// Central gravity lookup. Unity's global gravity is NOT used in this project:
    /// every body asks the GravityManager what gravity acts at its position.
    ///
    /// Rule: of all sources that affect a point, only those with the highest priority count.
    /// Sources with the same priority are added together (so several planets can pull at once).
    /// If no source affects the point, DefaultGravity is used.
    ///
    /// Static registry (like ServiceLocator), so it works in any scene without setup.
    /// </summary>
    public static class GravityManager
    {
        public static readonly Vector3 DefaultGravity = new Vector3(0f, -9.81f, 0f);

        private static readonly List<GravitySource> Sources = new();

        // Clears the list when "Enter Play Mode Options" skips domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Sources.Clear();

        public static void Register(GravitySource source)
        {
            if (source != null && !Sources.Contains(source)) Sources.Add(source);
        }

        public static void Unregister(GravitySource source) => Sources.Remove(source);

        public static Vector3 GetGravity(Vector3 position)
        {
            int bestPriority = int.MinValue;
            bool any = false;
            for (int i = 0; i < Sources.Count; i++)
            {
                var s = Sources[i];
                if (s == null || !s.isActiveAndEnabled || !s.Affects(position)) continue;
                if (s.Priority > bestPriority) bestPriority = s.Priority;
                any = true;
            }
            if (!any) return DefaultGravity;

            Vector3 gravity = Vector3.zero;
            for (int i = 0; i < Sources.Count; i++)
            {
                var s = Sources[i];
                if (s == null || !s.isActiveAndEnabled || s.Priority != bestPriority || !s.Affects(position)) continue;
                gravity += s.GetGravity(position);
            }
            return gravity;
        }

        /// <summary>"Up" at a position: the opposite of gravity. Falls back to world up in zero gravity.</summary>
        public static Vector3 GetUp(Vector3 position)
        {
            Vector3 gravity = GetGravity(position);
            return gravity.sqrMagnitude > 1e-6f ? -gravity.normalized : Vector3.up;
        }
    }
}
