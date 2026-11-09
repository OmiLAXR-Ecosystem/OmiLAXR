/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
#if UNITY_EDITOR
using System.Collections.Generic;

namespace OmiLAXR.Editor.Wizard
{
    /// <summary>
    /// A section of the OmiLAXR Wizard. Modules implement this interface in their editor assembly
    /// to add their own checks and setup actions without the core knowing about them.
    /// The window finds all implementations via <c>TypeCache</c>, so each one needs a public parameterless constructor.
    /// </summary>
    public interface IWizardSection
    {
        /// <summary>
        /// Heading of the section in the wizard.
        /// </summary>
        string Title { get; }

        /// <summary>
        /// Wizard step in which the section appears.
        /// </summary>
        WizardStep Step { get; }

        /// <summary>
        /// Sort order of the section within its step. Lower values are drawn first. The core sections use 0.
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Checks shown in the status list of the section's step.
        /// They are evaluated when the window gets focus, the scene or project changes, or a fix was applied.
        /// </summary>
        IEnumerable<WizardCheck> GetChecks();

        /// <summary>
        /// Draws the setup actions of this section with IMGUI.
        /// </summary>
        void OnGUI();
    }
}
#endif
