/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OmiLAXR.Editor.Wizard
{
    /// <summary>
    /// Wizard checks and actions of the core package: scoped registry, pipeline, data provider and credentials.
    /// </summary>
    internal sealed class CoreWizardSection : IWizardSection
    {
        public string Title => "OmiLAXR";
        public WizardStep Step => WizardStep.Required;
        public int Order => 0;

        private static string CredentialsPath => Path.Combine(Application.streamingAssetsPath, "credentials.json");
        private static string BearerPath => Path.Combine(Application.streamingAssetsPath, "bearer.json");

        public IEnumerable<WizardCheck> GetChecks()
        {
            yield return new WizardCheck(
                "Scoped registry for OmiLAXR packages",
                ScopedRegistry.IsConfigured,
                "Without it, the Package Manager cannot find OmiLAXR modules and updates.",
                "Add registry", ScopedRegistry.Configure);

            yield return new WizardCheck(
                "Pipeline in the open scene",
                WizardScene.Contains<Pipeline>,
                "The OmiLAXR prefab contains a learner pipeline with actor and instructor.",
                "Add prefab", AddPrefabToScene.AddPrefab);

            yield return new WizardCheck(
                "Data provider in the open scene",
                WizardScene.Contains<DataProvider>,
                "A data provider turns tracking events into statements and sends them. " +
                "Install a data provider module such as OmiLAXR.xAPI (below) and add its prefab.");

            yield return new WizardCheck(
                "Credentials file in StreamingAssets",
                () => File.Exists(CredentialsPath) || File.Exists(BearerPath),
                "Web endpoints read their login from 'credentials.json' or 'bearer.json'. Local file endpoints need none.",
                "Create credentials.json", CustomMenu.CreateCredentialsFile,
                optional: true);
        }

        public void OnGUI()
        {
            // Deferred, because refreshing the AssetDatabase in the middle of OnGUI breaks the IMGUI layout
            if (GUILayout.Button("Add OmiLAXR pipeline prefab to the scene"))
                EditorApplication.delayCall += AddPrefabToScene.AddPrefab;
            if (GUILayout.Button("Create 'credentials.json' (basic auth)"))
                EditorApplication.delayCall += CustomMenu.CreateCredentialsFile;
            if (GUILayout.Button("Create 'bearer.json' (bearer token)"))
                EditorApplication.delayCall += CustomMenu.CreateBearerAuthFile;
        }
    }
}
#endif
