/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using OmiLAXR.Types;
using UnityEngine;

namespace OmiLAXR.Context
{
    /// <summary>
    /// Learning context component that defines the primary language for the current scenario.
    /// Provides language information for internationalization and localization of analytics statements.
    /// Implements singleton pattern to ensure consistent language context across the application.
    /// </summary>
    [AddComponentMenu("OmiLAXR / Scenario Context / Scenario Language")]
    [DisallowMultipleComponent]
    public sealed class ScenarioLanguage : LearningContext, ILanguageProvider
    {
        /// <summary>
        /// Static reference to the singleton instance.
        /// </summary>
        private static ScenarioLanguage _instance;
        
        /// <summary>
        /// Singleton accessor for the scenario language instance.
        /// Ensures only one language context exists per scene.
        /// </summary>
        public static ScenarioLanguage Instance => GetInstance(ref _instance);

        /// <summary>
        /// The primary language setting for this scenario.
        /// Defaults to English (en) and can be configured in the inspector.
        /// Used for localizing statement content and metadata.
        /// </summary>
        public Languages language = Languages.en;

        /// <inheritdoc />
        public string GetLanguageCode()
        {
            // Enum names of reserved words are prefixed with '_'
            var code = language.ToString();
            return code[0] == '_' ? code.Substring(1) : code;
        }
    }
}