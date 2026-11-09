/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
#if UNITY_EDITOR
namespace OmiLAXR.Editor.Wizard
{
    /// <summary>
    /// Pages of the OmiLAXR Wizard in the order they are shown.
    /// </summary>
    public enum WizardStep
    {
        Welcome,
        /// <summary>Everything a project needs to track and send data.</summary>
        Required,
        /// <summary>Optional tracking: XR input, heart rate and other physiological measures, privacy.</summary>
        Tracking,
        /// <summary>Tools around OmiLAXR, such as ReCoPa and xAPI4Unity.</summary>
        Tools,
        /// <summary>MCP server for AI coding agents.</summary>
        AI,
        Done
    }
}
#endif
