/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace OmiLAXR.Editor.Wizard
{
    /// <summary>
    /// Opens the OmiLAXR Wizard once in a new project, and on every editor start if 'Show on startup' is on.
    /// This is a separate class because Unity does not allow <see cref="Application.isBatchMode"/> in the static
    /// constructor of an <see cref="EditorWindow"/>.
    /// </summary>
    [InitializeOnLoad]
    internal static class WizardStartup
    {
        private const string SessionKey = "OmiLAXR.Wizard.OpenedThisSession";

        static WizardStartup()
        {
            if (Application.isBatchMode)
                return;
            EditorApplication.delayCall += OpenOnStartup;
        }

        /// <summary>
        /// Whether the wizard opens on every editor start in this project. It always opens once in a new project.
        /// </summary>
        public static bool ShowOnStartup
        {
            get => EditorPrefs.GetBool(ProjectKey("ShowOnStartup"), false);
            set => EditorPrefs.SetBool(ProjectKey("ShowOnStartup"), value);
        }

        // EditorPrefs are shared by all projects, so keys contain the project path
        private static string ProjectKey(string key) => $"OmiLAXR.Wizard.{key}.{Application.dataPath}";

        private static void OpenOnStartup()
        {
            if (SessionState.GetBool(SessionKey, false))
                return;
            SessionState.SetBool(SessionKey, true);

            var firstTime = !EditorPrefs.HasKey(ProjectKey("Seen"));
            EditorPrefs.SetBool(ProjectKey("Seen"), true);

            if (firstTime || ShowOnStartup)
                WizardWindow.Open();
        }
    }
}
#endif
