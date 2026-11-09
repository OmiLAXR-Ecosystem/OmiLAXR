/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System;
using System.ComponentModel;
using UnityEngine;

namespace OmiLAXR.Context
{
    /// <summary>
    /// Learning context component that provides a registration UUID for the current session.
    /// Used for grouping related learning activities and statements under a common identifier.
    /// Implements singleton pattern and supports both manual UUID assignment and automatic generation.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    [AddComponentMenu("OmiLAXR / Scenario Context / Registration")]
    [Description("Component for holding a registration UUID.")]
    public sealed class Registration : LearningContext, IRegistrationProvider
    {
        /// <summary>
        /// Static reference to the singleton instance.
        /// </summary>
        private static Registration _instance;
        
        /// <summary>
        /// Singleton accessor for the registration instance.
        /// Ensures only one registration context exists per scene.
        /// </summary>
        public static Registration Instance => GetInstance(ref _instance);
        
        /// <summary>
        /// The registration UUID string, must conform to RFC4122 standard.
        /// Used to group related learning activities and analytics statements.
        /// Can be manually set in the inspector or automatically generated.
        /// </summary>
        [Header("Must be an UUID according to RFC4122.")]
        public string uuid;

        /// <summary>
        /// When enabled, automatically generates a new UUID during Awake().
        /// Useful for ensuring each session has a unique registration identifier.
        /// </summary>
        public bool autoGenerateUuid;

        /// <summary>
        /// Unity lifecycle method called when the component awakens.
        /// Automatically generates a UUID if autoGenerateUuid is enabled.
        /// </summary>
        private void Awake()
        {
            if (autoGenerateUuid)
                uuid = GenerateUuid().ToString();
        }

        /// <summary>
        /// Generates a new RFC4122-compliant UUID.
        /// Uses System.Guid.NewGuid() to ensure uniqueness and standards compliance.
        /// </summary>
        /// <returns>A new globally unique identifier</returns>
        /// <inheritdoc />
        public Guid? GetRegistration() => enabled && Guid.TryParse(uuid, out var registration) ? registration : (Guid?)null;

        public Guid GenerateUuid()
        {
            return System.Guid.NewGuid();
        }
    }
}