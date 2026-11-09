/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
#if UNITY_EDITOR
namespace OmiLAXR.Editor.Wizard
{
    /// <summary>
    /// An OmiLAXR package the wizard offers, with the step and group it belongs to.
    /// </summary>
    internal sealed class WizardModule
    {
        public readonly string Name;
        public readonly string Title;
        public readonly string Description;
        public readonly WizardStep Step;
        /// <summary>Heading that groups modules within a step, e.g. "XR input".</summary>
        public readonly string Group;
        /// <summary>Optional link shown on the module card, e.g. to a desktop app the module talks to.</summary>
        public readonly string LinkLabel;
        public readonly string LinkUrl;

        public WizardModule(string name, string title, string description, WizardStep step, string group,
            string linkLabel = null, string linkUrl = null)
        {
            Name = name;
            Title = title;
            Description = description;
            Step = step;
            Group = group;
            LinkLabel = linkLabel;
            LinkUrl = linkUrl;
        }
    }

    /// <summary>
    /// The OmiLAXR packages the wizard knows. They are listed even if the registry search fails or a package is not installed yet.
    /// Other <c>com.rwth.unity.omilaxr.*</c> packages found in the registry appear in the tools step.
    /// </summary>
    internal static class WizardModules
    {
        public const string Core = "com.rwth.unity.omilaxr";
        public const string Xapi = "com.rwth.unity.omilaxr.xapi";
        public const string HypeRate = "com.rwth.unity.omilaxr.hyperate";
        public const string ReCoPa = "com.rwth.unity.omilaxr.recopa";

        public static readonly WizardModule[] All =
        {
            new WizardModule(Xapi, "OmiLAXR.xAPI",
                "Data provider that composes xAPI statements and sends them to a Learning Record Store or to files.",
                WizardStep.Required, "Data format"),

            new WizardModule("com.rwth.unity.omilaxr.unityxr", "OmiLAXR.UnityXR",
                "Tracks interactables, gaze interactors and teleportation of the XR Interaction Toolkit.",
                WizardStep.Tracking, "XR input"),
            new WizardModule("com.rwth.unity.omilaxr.metaxr", "OmiLAXR.MetaXR",
                "Tracks grab and hand-grab interactions and provides eye calibration and face data with the Meta XR SDK.",
                WizardStep.Tracking, "XR input"),
            new WizardModule("com.rwth.unity.omilaxr.mrtk2", "OmiLAXR.MRTK2",
                "Adds tracking for the Mixed Reality Toolkit 2 (MRTK2).",
                WizardStep.Tracking, "XR input"),
            new WizardModule(HypeRate, "OmiLAXR.HypeRate",
                "Reads the heart rate from the HypeRate app. With OmiLAXR.xAPI, the PPM hook adds it to every statement.",
                WizardStep.Tracking, "Heart rate and PPM"),
            new WizardModule("com.rwth.unity.omilaxr.pseudonymization", "OmiLAXR.Pseudonymization",
                "Creates unique pseudonyms for learners, e.g. with the MTLG pseudonym service, to use instead of real names.",
                WizardStep.Tracking, "Privacy"),

            new WizardModule(ReCoPa, "OmiLAXR.ReCoPa",
                "Bridge to the Researcher Companion Panel: watch statements live, pause tracking and calibrate from the desktop app.",
                WizardStep.Tools, "ReCoPa",
                "Get the ReCoPa app", "https://github.com/OmiLAXR-Ecosystem/ReCoPa"),
            new WizardModule("com.rwth.unity.omilaxr.xapi4unity", "xAPI4Unity",
                "Editor tool that turns xAPI JSON definitions into the C# xAPI.Registry the xAPI composers use.",
                WizardStep.Tools, "xAPI definitions",
                "Open the xAPI Registry", "https://xapi.elearn.rwth-aachen.de"),
        };
    }
}
#endif
