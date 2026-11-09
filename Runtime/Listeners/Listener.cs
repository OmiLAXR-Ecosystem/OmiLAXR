/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OmiLAXR.Listeners
{
    /// <summary>
    /// Abstract base class for all object detection listeners in the OmiLAXR pipeline system.
    /// Listeners are responsible for discovering and providing Unity Objects to the pipeline
    /// for analytics tracking. They serve as the entry point for identifying trackable objects
    /// in the scene and making them available for further processing by composers and endpoints.
    /// </summary>
    public abstract class Listener : ActorPipelineComponent, IObjectSource
    {
        /// <summary>
        /// Event triggered when objects are discovered and provided to the pipeline.
        /// Subscribers (typically pipeline components) receive the array of found objects
        /// for further processing, filtering, and analytics generation.
        /// </summary>
        public event System.Action<Object[]> OnFoundObjects;
        
        /// <summary>
        /// Abstract method that defines how this listener discovers objects.
        /// Implementing classes must define their specific object detection logic here.
        /// Called when the listener needs to start monitoring for objects.
        /// </summary>
        public abstract void StartListening();

        /// <summary>
        /// Generic utility method for detecting and providing objects of a specific type to the pipeline.
        /// Searches for all objects of the specified type and reports them through the Found method.
        /// Commonly used by implementing classes to simplify object discovery.
        /// </summary>
        /// <param name="includeInactive">Whether to include inactive/disabled objects in the search</param>
        /// <typeparam name="T">The Unity Object type to search for</typeparam>
        protected void Detect<T>(bool includeInactive = false) where T : Object
        {
            // Use the inherited FindObjects method to locate objects of type T
            var objects = FindObjects<T>(includeInactive);
            
            // Report the discovered objects to the pipeline
            Found(objects);
        }
        
        /// <summary>
        /// Reports discovered objects to the pipeline through the OnFoundObjects event.
        /// Performs validation and type conversion before triggering the event.
        /// Only reports non-empty arrays when the listener is enabled.
        /// </summary>
        /// <param name="objects">Array of discovered objects to report</param>
        /// <typeparam name="T">The specific Unity Object type being reported</typeparam>
        protected void Found<T>(params T[] objects) where T : Object
        {
            // Skip if listener is disabled or no objects were provided
            if (!enabled || objects == null || objects.Length == 0)
                return;
                
            // Convert to base Object array for pipeline compatibility
            var items = objects.Select(o => o as Object).ToArray();
            
            // Trigger the event only if we have valid objects
            if (items.Length > 0)
                OnFoundObjects?.Invoke(items);
        }
    }
}