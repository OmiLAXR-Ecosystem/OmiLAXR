/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/

using System;
using OmiLAXR.Schedules;
using UnityEngine;

namespace OmiLAXR
{
    /// <summary>
    /// Singleton component that manages global configuration settings for the OmiLAXR system.
    /// Executes very early in Unity's execution order to ensure settings are available before other components initialize.
    /// Provides system-wide configuration that affects all pipelines and tracking behaviors.
    /// </summary>
    [DefaultExecutionOrder(-99999999)] // Execute as early as possible
    [DisallowMultipleComponent] // Enforce singleton pattern at component level
    [AddComponentMenu("OmiLAXR / Global Settings")]
    public sealed class GlobalSettings : PipelineComponent, Context.ITrackingNameSettings
    {
        /// <summary>
        /// Cached singleton instance for fast access across the system.
        /// </summary>
        private static GlobalSettings _instance;
        
        /// <summary>
        /// Singleton accessor that provides thread-safe access to the global settings instance.
        /// Automatically locates the instance in the scene if not already cached.
        /// </summary>
        public static GlobalSettings Instance => GetInstance(ref _instance);

        [SerializeField, Tooltip("Default scheduler for all scheduled tracking behaviours without their own. Falls back to a realtime ticker.")]
        private Scheduler globalScheduler;

        public Scheduler GetScheduler()
        {
            if (!globalScheduler)
            {
                DebugLog.OmiLAXR.Warning("Cannot find Global Scheduler. Please assign one to <GlobalSettings>. As fallback RealtimeScheduler is chosen.");
                globalScheduler = RealtimeTicker.Create(this);
            }
            if (!globalScheduler.owner)
                globalScheduler.owner = this;
            return globalScheduler;
        }
        
        private void Reset()
        {
            if (!globalScheduler)
                globalScheduler = RealtimeTicker.Create(this);
        }

        /// <summary>
        /// Controls how object names are resolved for tracking identification.
        /// Affects all tracking behaviors unless overridden by CustomTrackingName components.
        /// </summary>
        [Tooltip("Whether the tracked name is by default the object name or the full name path to the object in hierarchy. This option is not applied if a [CustomTrackingName] component is attached.")]
        public TrackingNameBehaviour trackingNameBehaviour = TrackingNameBehaviour.ObjectName;

        TrackingNameBehaviour Context.ITrackingNameSettings.TrackingNameBehaviour => trackingNameBehaviour;

        /// <summary>
        /// Enumeration defining different strategies for resolving object names in tracking contexts.
        /// Affects analytics data consistency and object identification across sessions.
        /// </summary>
        public enum TrackingNameBehaviour
        {
            /// <summary>
            /// Use only the GameObject's name property for tracking identification.
            /// Faster but may not be unique across complex hierarchies.
            /// </summary>
            ObjectName,
            
            /// <summary>
            /// Use the full hierarchy path from root to object for tracking identification.
            /// Provides unique identification but generates longer names and requires more processing.
            /// </summary>
            HierarchyTreePath
        }
    }
}