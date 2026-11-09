/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System.Collections.Generic;
using OmiLAXR.Listeners;
using OmiLAXR.Filters;
using OmiLAXR.TrackingBehaviours;
using UnityEngine;

namespace OmiLAXR
{
    /// <summary>
    /// Generic base class for creating modular pipeline extensions that target specific pipeline types.
    /// Automatically discovers and registers child components with the target pipeline during initialization.
    /// Executes early in Unity's order to ensure extensions are applied before pipeline startup.
    /// </summary>
    /// <typeparam name="T">Specific type of Pipeline this extension targets</typeparam>
    [DefaultExecutionOrder(-100)] // Execute before most components but after GlobalSettings
    public abstract class PipelineExtension<T> : PipelineComponent, IPipelineExtension
    where T : Pipeline
    {
        /// <summary>
        /// Reference to the specific Pipeline instance this extension is attached to.
        /// Strongly typed to the generic parameter for type-safe access to pipeline-specific functionality.
        /// </summary>
        public T Pipeline { get; protected set; }
        
        /// <summary>
        /// Gets the Pipeline as the base Pipeline type for interface compliance.
        /// Required by IPipelineExtension interface for polymorphic access.
        /// </summary>
        /// <returns>The Pipeline instance cast to the base Pipeline type</returns>
        public Pipeline GetPipeline() => Pipeline;
        
        /// <summary>
        /// Collection of Listener components discovered in child objects.
        /// Listeners detect and report objects for pipeline processing.
        /// </summary>
        public readonly List<Listener> Listeners = new List<Listener>();
        
        /// <summary>
        /// Collection of TrackingBehaviour components discovered in child objects.
        /// TrackingBehaviours monitor and analyze detected objects for learning analytics.
        /// </summary>
        public readonly List<ITrackingBehaviour> TrackingBehaviours = new List<ITrackingBehaviour>();
        
        /// <summary>
        /// Collection of Filter components discovered in child objects.
        /// Filters refine the set of objects that proceed through the pipeline.
        /// </summary>
        public readonly List<Filter> Filters = new List<Filter>();
        
        /// <summary>
        /// Unity Awake callback that automatically discovers and extends the target pipeline.
        /// Searches for the target pipeline type and initiates the extension process.
        /// </summary>
        private void Awake()
        {
            // Find the target pipeline instance in the scene
            var pipeline = FindObject<T>();
            Extend(pipeline);
        }

        /// <summary>
        /// Extends the specified Pipeline with components found in this extension's hierarchy.
        /// Automatically discovers and registers all child Listeners, TrackingBehaviours, and Filters.
        /// Maintains local collections for extension management while integrating with the target pipeline.
        /// </summary>
        /// <param name="pipeline">The Pipeline instance to extend with additional capabilities</param>
        public void Extend(Pipeline pipeline)
        {
            // Cache the strongly-typed pipeline reference
            Pipeline = (T)pipeline;

            // Discover all extension components in child objects
            var listeners = gameObject.GetComponentsInChildren<Listener>();
            var tbs = gameObject.GetComponentsInChildren<ITrackingBehaviour>();
            var filters = gameObject.GetComponentsInChildren<Filter>();
            
            // Store references locally for extension management
            Listeners.AddRange(listeners);
            TrackingBehaviours.AddRange(tbs);
            Filters.AddRange(filters);
            
            // Register components with the target pipeline
            Pipeline.Listeners.AddRange(listeners);
            Pipeline.TrackingBehaviours.AddRange(tbs);
            Pipeline.Filters.AddRange(filters);
            if (!Pipeline.Extensions.Contains(this))
                Pipeline.Extensions.Add(this);

            // Log successful extension for debugging
            DebugLog.OmiLAXR.Print("Extended pipeline " + pipeline);
        }
    }
}