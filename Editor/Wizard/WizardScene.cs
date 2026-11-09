/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
#if UNITY_EDITOR
using UnityEngine;

namespace OmiLAXR.Editor.Wizard
{
    /// <summary>
    /// Scene queries for wizard checks. Wraps the Unity-version-specific find APIs.
    /// </summary>
    public static class WizardScene
    {
        /// <summary>
        /// Returns true if the open scenes contain an object of the given type, including inactive ones.
        /// </summary>
        public static bool Contains<T>() where T : Object => Find<T>();

        /// <summary>
        /// Returns the first object of the given type in the open scenes, including inactive ones, or null.
        /// </summary>
        public static T Find<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
            return Object.FindObjectOfType<T>(true);
#endif
        }
    }
}
#endif
