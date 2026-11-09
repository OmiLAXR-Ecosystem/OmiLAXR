/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
#if UNITY_EDITOR
using System;

namespace OmiLAXR.Editor.Wizard
{
    /// <summary>
    /// A single status check of the OmiLAXR Wizard, optionally with an action that fixes it.
    /// </summary>
    public sealed class WizardCheck
    {
        /// <summary>
        /// Short description of what is checked.
        /// </summary>
        public readonly string Label;

        /// <summary>
        /// Returns true if the check passes.
        /// </summary>
        public readonly Func<bool> Evaluate;

        /// <summary>
        /// Explanation shown when the check fails. May be null.
        /// </summary>
        public readonly string Hint;

        /// <summary>
        /// Caption of the fix button. May be null if <see cref="Fix"/> is null.
        /// </summary>
        public readonly string FixLabel;

        /// <summary>
        /// Action that resolves a failed check. May be null if there is no automatic fix.
        /// </summary>
        public readonly Action Fix;

        /// <summary>
        /// Optional checks are shown as information instead of a warning when they fail.
        /// </summary>
        public readonly bool Optional;

        public WizardCheck(string label, Func<bool> evaluate, string hint = null,
            string fixLabel = null, Action fix = null, bool optional = false)
        {
            Label = label;
            Evaluate = evaluate;
            Hint = hint;
            FixLabel = fixLabel;
            Fix = fix;
            Optional = optional;
        }
    }
}
#endif
