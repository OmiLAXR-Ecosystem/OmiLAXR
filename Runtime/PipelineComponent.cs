/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/

using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OmiLAXR
{
    /// <summary>
    /// Base class for all components that participate in the OmiLAXR pipeline system.
    /// Provides common utility methods for finding Unity objects in a version-compatible way.
    /// </summary>
    public abstract class PipelineComponent : MonoBehaviour, IPipelineComponent
    {
        protected virtual void OnEnable()
        {
            ApplicationShutdownManager.Register(this);
        }
        /// <summary>
        /// Finds the first object of the specified type in the scene.
        /// Handles different Unity versions by using the appropriate API calls.
        /// </summary>
        /// <param name="includeInactive">Whether to include inactive GameObjects in the search</param>
        /// <typeparam name="T">Type of object to find</typeparam>
        /// <returns>The first object of the specified type, or null if none exists</returns>
        protected static T FindObject<T>(bool includeInactive = false) where T : Object 
#if UNITY_2023_1_OR_NEWER
            => FindFirstObjectByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#else 
            => FindObjectOfType<T>(includeInactive);
#endif

        /// <summary>
        /// Finds all objects of the specified type in the scene.
        /// Handles different Unity versions by using the appropriate API calls.
        /// Provides filtering for inactive objects based on the includeInactive parameter.
        /// </summary>
        /// <param name="includeInactive">Whether to include inactive GameObjects in the search</param>
        /// <typeparam name="T">Type of objects to find</typeparam>
        /// <returns>Array of objects of the specified type</returns>
        protected static T[] FindObjects<T>(bool includeInactive = false) where T : Object
        {
#if UNITY_2023_1_OR_NEWER
    return FindObjectsByType<T>(
        includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
        FindObjectsSortMode.None
    );
#else
            return FindObjectsOfType<T>()
                .Where(o =>
                {
                    if (!o) return false;

                    if (includeInactive)
                        return true;

                    if (o is GameObject go)
                        return go.activeSelf;

                    if (o is Component c)
                        return c.gameObject.activeSelf;

                    return true; // fallback if neither GameObject nor Component
                })
                .ToArray();
#endif
        }

        /// <summary>
        /// Implements a lazy-loading singleton pattern that's compatible across Unity versions.
        /// Returns the existing instance if available, otherwise finds it in the scene.
        /// </summary>
        /// <param name="instance">Reference to the cached instance variable</param>
        /// <typeparam name="T">Type of the singleton instance</typeparam>
        /// <returns>The singleton instance</returns>
        protected static T GetInstance<T>(ref T instance) where T : Object
        {
            // Unity's null check (not ??=) so that destroyed instances, e.g. after a scene change, are looked up again
            if (!instance)
#if UNITY_2023_1_OR_NEWER
                instance = FindFirstObjectByType<T>();
#else
                instance = FindObjectOfType<T>();
#endif
            return instance;
        }

        protected virtual void OnAppQuit()
        {
            // do nothing
        }
    }
}