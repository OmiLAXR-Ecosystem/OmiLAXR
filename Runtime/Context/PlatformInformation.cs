/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System;
using System.ComponentModel;
using OmiLAXR.Composers;
using UnityEngine;

namespace OmiLAXR.Context
{
    /// <summary>
    /// Singleton component that provides standardized platform information for learning analytics.
    /// Generates platform identification strings that include module, composer, version, and OS information
    /// with optional custom prefixes and suffixes. Essential for analytics data contextualization
    /// and cross-platform compatibility tracking in learning environments.
    /// </summary>
    [AddComponentMenu("OmiLAXR / Scenario Context / Platform Information")]
    [DisallowMultipleComponent]
    [Description("Provides platform information in following format [prefixes:]OmiLAXR.{MODULE}:{COMPOSER}:{VERSION}:{OS}[:suffixes].")]
    public sealed class PlatformInformation : LearningContext, IPlatformInfoProvider
    {
        /// <summary>
        /// Serializable data structure for storing custom prefixes and suffixes
        /// that can be appended to the standard platform information string.
        /// Allows for environment-specific or deployment-specific customization
        /// of platform identification without modifying core logic.
        /// </summary>
        [Serializable]
        public struct PlatformInformationAppendix
        {
            /// <summary>
            /// Array of suffix strings to append after the platform information.
            /// Suffixes are joined with colons and added to the end of the platform string.
            /// Useful for deployment identifiers, environment tags, or additional context.
            /// </summary>
            [SerializeField] 
            public string[] suffixes;
            
            /// <summary>
            /// Array of prefix strings to prepend before the platform information.
            /// Prefixes are joined with colons and added to the beginning of the platform string.
            /// Useful for organization identifiers, project tags, or hierarchical context.
            /// </summary>
            [SerializeField] 
            public string[] prefixes;
        }
        
        /// <summary>
        /// Private static instance reference for singleton pattern implementation.
        /// Ensures only one PlatformInformation instance exists throughout the application lifecycle.
        /// </summary>
        private static PlatformInformation _instance;
        
        /// <summary>
        /// Public singleton accessor that retrieves or creates the PlatformInformation instance.
        /// Uses the inherited GetInstance method from LearningContext to ensure proper
        /// singleton behavior with Unity component lifecycle management.
        /// </summary>
        public static PlatformInformation Instance => GetInstance(ref _instance);
        
        /// <summary>
        /// Configuration data for custom prefixes and suffixes to be included in platform strings.
        /// Serialized to the Unity Inspector, allowing runtime configuration of additional
        /// platform identification context without code changes.
        /// </summary>
        [SerializeField] 
        public PlatformInformationAppendix appendix = new PlatformInformationAppendix();

        /// <summary>
        /// Generates a standardized platform identification string for analytics and logging purposes.
        /// Creates a colon-separated string containing module, composer, version, and OS information
        /// with optional custom prefixes and suffixes for comprehensive platform identification.
        /// 
        /// Format: [prefixes:]OmiLAXR.{module}:{composer}:v{version}:{OS}[:suffixes]
        /// Example: "dev:test:OmiLAXR.Analytics:InteractionComposer:v1.2.3:WindowsPlayer:staging:prod"
        /// </summary>
        /// <param name="module">The OmiLAXR module name (e.g., "Analytics", "Tracking", "Assessment")</param>
        /// <param name="version">The version string for the module (e.g., "1.2.3", "2.0.0-beta")</param>
        /// <param name="composer">The composer providing the current statement.</param>
        /// <returns>
        /// Complete platform identification string with format:
        /// [custom_prefixes:]OmiLAXR.{module}:{composer}:v{version}:{unity_platform}[:custom_suffixes]
        /// </returns>
        public string GetPlatformString(string module, string version, IComposer composer)
        {
            var composerName = composer.GetType().Name;
            var composerGroup = composer.GetGroup();

            var sdkStr = "";
            if (SdkProvider.Instance)
            {
                var sdk = SdkProvider.Instance.GetName();
                var sdkVersion = SdkProvider.Instance.GetVersion();
                sdkStr = $"::{sdk}v{sdkVersion}";
            }
            
            // Build the core platform string with module, composer, version, and Unity platform
            var platformStr = $"Unity{Application.platform}v{Application.unityVersion}::OmiLAXR.{module}v{version}::{composerGroup}.{composerName}{sdkStr}";

            // Prepend custom prefixes if any are configured
            if (appendix.prefixes != null && appendix.prefixes.Length > 0)
            {
                platformStr = $"{string.Join(":", appendix.prefixes)}:{platformStr}";
            }
            
            // Append custom suffixes if any are configured
            if (appendix.suffixes != null && appendix.suffixes.Length > 0)
            {
                platformStr = $"{platformStr}:{string.Join(":", appendix.suffixes)}";
            }

            return platformStr;
        }
    }
}