/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
#if UNITY_EDITOR
using System.Collections.Generic;
using OmiLAXR.Actors.HeartRate;
using UnityEditor;
using UnityEngine;

namespace OmiLAXR.Editor.Wizard
{
    /// <summary>
    /// Heart rate part of the tracking step: shows whether a heart rate provider (e.g. from OmiLAXR.HypeRate) is in the scene.
    /// </summary>
    internal sealed class HeartRateWizardSection : IWizardSection
    {
        public string Title => "Heart rate";
        public WizardStep Step => WizardStep.Tracking;
        public int Order => 0;

        public IEnumerable<WizardCheck> GetChecks()
        {
            yield return new WizardCheck(
                "Heart rate provider in the open scene",
                WizardScene.Contains<HeartRateProvider>,
                "Heart rate comes from a HeartRateProvider, e.g. the HypeRateProvider of OmiLAXR.HypeRate. " +
                "Add it to the actor pipeline and connect it to the 'Heart Rate Monitor' component, as in the module's prefab.",
                optional: true);
        }

        public void OnGUI()
        {
            var provider = WizardScene.Find<HeartRateProvider>();
            EditorGUILayout.LabelField(
                provider ? $"Heart rate provider: {provider.name} ({provider.GetType().Name})" : "No heart rate provider in the open scene.",
                EditorStyles.wordWrappedLabel);
            using (new EditorGUI.DisabledScope(!provider))
            {
                if (GUILayout.Button("Select heart rate provider"))
                {
                    Selection.activeObject = provider;
                    EditorGUIUtility.PingObject(provider);
                }
            }
        }
    }
}
#endif
