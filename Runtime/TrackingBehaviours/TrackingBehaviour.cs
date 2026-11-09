/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OmiLAXR.Schedules;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OmiLAXR.TrackingBehaviours
{
    /// <summary>
    /// Non-generic base class for tracking behaviors that work with general Unity Objects.
    /// Provides a simplified API by using Object as the tracking type, eliminating the need
    /// for generic type parameters when working with mixed object types.
    /// </summary>
    public abstract class TrackingBehaviour : TrackingBehaviour<Object>
    {
        /// <summary>
        /// Empty implementation to be overridden by derived classes if needed.
        /// Called after objects have been filtered through the pipeline. Can be called multiple times.
        /// Base implementation intentionally does nothing - derived classes override to process objects.
        /// </summary>
        /// <param name="objects">Array of filtered objects to track</param>
        protected override void AfterFilteredObjects(Object[] objects)
        {
            // Base implementation is empty - derived classes should override if needed
        }
    }
    
    /// <summary>
    /// Generic base class for all tracking behaviors in the OmiLAXR system.
    /// Provides functionality to track specific types of objects through the pipeline.
    /// Executes early in Unity's order to ensure tracking is set up before other components.
    /// 
    /// This class handles the complete lifecycle of tracking behaviors:
    /// - Pipeline event subscription and cleanup
    /// - Object filtering and type-safe processing  
    /// - Scheduler management for timed operations
    /// - Event binding and unbinding for memory management
    /// </summary>
    /// <typeparam name="T">Type of Unity Objects to track (must inherit from UnityEngine.Object)</typeparam>
    [DefaultExecutionOrder(-1)]
    public abstract class TrackingBehaviour<T> : ActorPipelineComponent, ITrackingBehaviour
    where T : Object
    {
        /// <summary>
        /// Collection of schedulers that control when this tracking behavior executes.
        /// Manages interval timers, timeout operations, and other scheduled tasks.
        /// All schedulers are automatically started/stopped with the pipeline lifecycle.
        /// </summary>
        protected readonly List<Scheduler> Schedulers = new List<Scheduler>();

        /// <summary>
        /// Creates a simple interval scheduler with just the tick action and interval duration.
        /// Convenience method for basic interval operations without advanced configuration.
        /// </summary>
        /// <param name="onTick">Action to execute at each interval</param>
        /// <param name="intervalSeconds">Time between executions in seconds</param>
        /// <returns>Configured IntervalTicker instance</returns>
        protected IntervalTicker SetInterval(Action onTick, float intervalSeconds = 1.0f, int ttLTicks = -1)
            => SetInterval(IntervalTicker.Create(this, intervalSeconds, onTick, ttLTicks, runImmediate: true));

        protected IntervalTicker SetInterval(IntervalTicker ticker) => (IntervalTicker)SetTicker(ticker);
        
        protected Scheduler SetTicker(Scheduler ticker)
        {
            Schedulers.Add(ticker);
            return ticker;
        }

        /// <summary>
        /// Creates and configures a timeout-based scheduler with full settings control.
        /// The scheduler will execute the action once after the specified timeout period.
        /// </summary>
        /// <param name="onTick">Action to execute when timeout expires</param>
        /// <param name="settings">Detailed configuration for the timeout timer</param>
        /// <param name="onTickStart">Optional action called before timeout starts</param>
        /// <param name="onTickEnd">Optional action called after timeout completes</param>
        /// <returns>Configured TimeoutTicker instance for further control</returns>
        protected TimeoutTicker SetTimeout(TimeoutTicker timeoutTicker)
            => (TimeoutTicker)SetTicker(timeoutTicker);

        /// <summary>
        /// Creates a simple timeout scheduler with just the tick action and timeout duration.
        /// Convenience method for basic timeout operations without advanced configuration.
        /// </summary>
        /// <param name="onTick">Action to execute when timeout expires</param>
        /// <param name="timeoutSeconds">Timeout duration in seconds</param>
        /// <returns>Configured TimeoutTicker instance</returns>
        protected TimeoutTicker SetTimeout(Action onTick, float timeoutSeconds = 1.0f)
            => SetTimeout(TimeoutTicker.Create(this, timeoutSeconds, onTick, runImmediate: true));
       
        /// <summary>
        /// Initializes the tracking behavior by setting up schedulers and event subscriptions.
        /// Connects to pipeline events to receive objects for tracking and manages the complete
        /// lifecycle of the tracking behavior from pipeline start to stop.
        /// 
        /// This method is critical for proper tracking behavior operation and handles:
        /// - Pipeline lifecycle event binding
        /// - Object discovery and filtering event handling
        /// - Error handling for initialization failures
        /// - Scheduler lifecycle management
        /// </summary>
        protected override void OnEnable()
        {
            try
            {
                // Pipelines toggle their GameObject on start/stop, so OnEnable runs on every restart.
                // Subscribe only once per pipeline, otherwise every restart duplicates all callbacks.
                var pipeline = Pipeline;
                if (_subscribedPipeline == pipeline)
                    return;
                UnsubscribePipeline();

                pipeline.BeforeStartedPipeline += HandleBeforeStartedPipeline;
                pipeline.AfterStartedPipeline += HandleAfterStartedPipeline;
                pipeline.BeforeStoppedPipeline += HandleBeforeStoppedPipeline;
                pipeline.AfterStoppedPipeline += HandleAfterStoppedPipeline;
                pipeline.AfterFoundObjects += HandleAfterFoundObjects;
                pipeline.AfterFilteredObjects += HandleAfterFilteredObjects;
                pipeline.OnQuit += HandleQuit;
                _subscribedPipeline = pipeline;
            }
            catch (Exception ex)
            {
                // Log initialization errors but don't crash the application
                DebugLog.OmiLAXR.Error($"Error initializing {GetType().Name}: {ex.Message}");
            }
        }

        private Pipeline _subscribedPipeline;

        private void UnsubscribePipeline()
        {
            if (!_subscribedPipeline)
                return;
            _subscribedPipeline.BeforeStartedPipeline -= HandleBeforeStartedPipeline;
            _subscribedPipeline.AfterStartedPipeline -= HandleAfterStartedPipeline;
            _subscribedPipeline.BeforeStoppedPipeline -= HandleBeforeStoppedPipeline;
            _subscribedPipeline.AfterStoppedPipeline -= HandleAfterStoppedPipeline;
            _subscribedPipeline.AfterFoundObjects -= HandleAfterFoundObjects;
            _subscribedPipeline.AfterFilteredObjects -= HandleAfterFilteredObjects;
            _subscribedPipeline.OnQuit -= HandleQuit;
            _subscribedPipeline = null;
        }

        protected virtual void OnDestroy() => UnsubscribePipeline();

        private void HandleBeforeStartedPipeline(Pipeline _)
        {
            if (!enabled)
                return;
            BeforeStartedPipeline(Pipeline);
        }

        // Handle pipeline start - this is where tracking begins
        private void HandleAfterStartedPipeline(Pipeline _)
        {
            if (!enabled)
                return;
            OnStartedPipeline(Pipeline);
        }

        private void HandleBeforeStoppedPipeline(Pipeline _)
        {
            if (!enabled)
                return;
            BeforeStoppedPipeline(Pipeline);
        }

        // Handle pipeline stop - cleanup and resource disposal
        private void HandleAfterStoppedPipeline(Pipeline _)
        {
            if (!enabled)
                return;
            OnStoppedPipeline(Pipeline);
            ClearSchedules(); // Stop and clean up all schedulers
            Dispose(AllFilteredObjects.Select(o => o as Object).ToArray()); // Clean up tracked objects
        }

        // Handle objects discovered by the pipeline (before filtering)
        private void HandleAfterFoundObjects(Object[] objects)
        {
            if (!enabled)
                return;
            // Type optimization: skip selection if T is already Object type
            AfterFoundObjects(typeof(T) == typeof(Object) ? objects as T[] : Select<T>(objects));
        }

        // Handle objects after they've been filtered by the pipeline (main tracking target)
        private void HandleAfterFilteredObjects(Object[] objects)
        {
            if (!enabled)
                return;
            var selectedObjects = ExtractOrSelect<T>(objects);
            var newSelectedObjects = new List<T>();
            // Filter objects to match our tracking type and store them
            foreach (var o in objects)
            {
                if (AllFilteredObjects.Contains(o))
                    continue;
                AllFilteredObjects.Add(o);
            }
            foreach (var s in selectedObjects)
            {
                if (SelectedObjects.Contains(s))
                    continue;
                SelectedObjects.Add(s);
                newSelectedObjects.Add(s);
            }
            AfterFilteredObjects(newSelectedObjects.ToArray());
        }

        private void HandleQuit(Pipeline _)
        {
            if (!enabled)
                return;
            OnQuitPipeline(Pipeline);
        }
        
        /// <summary>
        /// Called when the pipeline is about to quit (Application quit).
        /// Override to perform cleanup operations before the pipeline stops.
        /// </summary>
        protected virtual void OnQuitPipeline(Pipeline pipeline) {}

        /// <summary>
        /// Called immediately before the pipeline starts.
        /// Override to perform pre-start initialization or validation.
        /// </summary>
        /// <param name="pipeline">Reference to the pipeline about to start</param>
        protected virtual void BeforeStartedPipeline(Pipeline pipeline) {}
        
        /// <summary>
        /// Called immediately before the pipeline stops.
        /// Override to perform cleanup preparation or save state before shutdown.
        /// </summary>
        /// <param name="pipeline">Reference to the pipeline about to stop</param>
        protected virtual void BeforeStoppedPipeline(Pipeline pipeline) {}
        
        /// <summary>
        /// Called when the pipeline starts. Override to implement custom start behavior.
        /// This is where tracking behaviors should initialize their tracking logic,
        /// set up event bindings, and prepare for object monitoring.
        /// </summary>
        /// <param name="pipeline">Reference to the started pipeline</param>
        protected virtual void OnStartedPipeline(Pipeline pipeline) {}
        
        /// <summary>
        /// Called before the pipeline stops. Override to implement custom stop behavior.
        /// Use this for final cleanup, data persistence, or graceful shutdown procedures.
        /// </summary>
        /// <param name="pipeline">Reference to the stopping pipeline</param>
        protected virtual void OnStoppedPipeline(Pipeline pipeline) {}
        
        /// <summary>
        /// Called after objects are found by the pipeline but before filtering.
        /// Override to implement custom processing on found objects, such as
        /// preliminary validation, logging, or preparation for filtering.
        /// </summary>
        /// <param name="objects">Array of found objects of type T</param>
        protected virtual void AfterFoundObjects(T[] objects) {}
        
        /// <summary>
        /// Called after objects have been filtered by the pipeline.
        /// Must be implemented by derived classes to process the filtered objects. 
        /// Can be called multiple times as new objects are discovered or existing ones are updated.
        /// 
        /// This is the main entry point for tracking logic - derived classes should:
        /// - Set up event bindings for the provided objects
        /// - Initialize tracking state for new objects
        /// - Configure monitoring parameters
        /// </summary>
        /// <param name="objects">Array of filtered objects of type T to begin tracking</param>
        protected abstract void AfterFilteredObjects(T[] objects);
        
        protected readonly List<T> SelectedObjects = new List<T>();

        /// <summary>
        /// Stores the currently selected objects for tracking.
        /// Maintains a persistent list of all objects that have been filtered
        /// through the pipeline and are actively being tracked.
        /// Used for cleanup operations and state management.
        /// </summary>
        protected static readonly List<Object> AllFilteredObjects = new List<Object>();

        /// <summary>
        /// Starts all registered schedulers if the component is enabled.
        /// Called automatically when the pipeline starts to begin timed operations.
        /// </summary>
        protected void StartSchedules()
        {
            if (!enabled)
                return;
            foreach (var scheduler in Schedulers)
            {
                if (!scheduler.owner)
                    scheduler.owner = this;
                scheduler.Start();
            }
        }

        /// <summary>
        /// Stops all registered schedulers without removing them from the collection.
        /// Preserves scheduler configuration for potential restart.
        /// </summary>
        protected void StopSchedules()
        {
            foreach (var scheduler in Schedulers)
                scheduler.Stop();
        }

        /// <summary>
        /// Stops and removes all schedulers from the collection.
        /// Called during pipeline shutdown to ensure complete cleanup.
        /// After this call, new schedulers must be created for future operations.
        /// </summary>
        protected void ClearSchedules()
        {
            StopSchedules(); // Stop all running schedulers first
            Schedulers.Clear(); // Remove all scheduler references
        }

        /// <summary>
        /// Unity lifecycle method called when the component is disabled.
        /// Ensures proper cleanup of schedulers and tracking events to prevent
        /// memory leaks and dangling event references.
        /// </summary>
        protected virtual void OnDisable()
        {
            StopSchedules(); // Stop all running operations
            DisposeAllTrackingEvents(); // Clean up event bindings
        }

        /// <summary>
        /// Unbinds all tracking events to prevent memory leaks and unexpected behavior.
        /// Uses reflection to find all ITrackingBehaviourEvent fields and calls UnbindAll on each.
        /// This ensures complete cleanup of event subscriptions regardless of the specific event types used.
        /// </summary>
        protected void DisposeAllTrackingEvents()
        {
            // Get all fields that implement ITrackingBehaviourEvent interface
            var fields = GetTrackingBehaviourEvents();

            foreach (var field in fields)
            {
                // Get the actual event instance from this tracking behavior
                var fieldValue = field.GetValue(this) as ITrackingBehaviourEvent;

                // Unbind all listeners to prevent memory leaks
                fieldValue?.UnbindAll();
            }
        }
        
        /// <summary>
        /// Cleans up resources when tracking stops.
        /// Called automatically during pipeline shutdown with all tracked objects.
        /// Base implementation unbinds all tracking events - override to add custom cleanup.
        /// </summary>
        /// <param name="objects">Array of objects that were being tracked</param>
        protected virtual void Dispose(Object[] objects)
        {
            DisposeAllTrackingEvents(); // Clean up all event bindings by default
        }
        
        /// <summary>
        /// Retrieves all fields in this class that implement ITrackingBehaviourEvent.
        /// Used for automatic event management and cleanup operations.
        /// Searches both public and private instance fields to ensure complete coverage.
        /// </summary>
        /// <returns>Array of FieldInfo for fields of type ITrackingBehaviourEvent</returns>
        public FieldInfo[] GetTrackingBehaviourEvents()
        {
            return GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(f => typeof(ITrackingBehaviourEvent).IsAssignableFrom(f.FieldType))
                .ToArray();
        }
        
        /// <summary>
        /// Filters an array of Unity Objects to only include those of the specified type.
        /// Uses both exact type matching and inheritance checking to capture all compatible objects.
        /// Essential for type-safe object tracking in the generic pipeline system.
        /// </summary>
        /// <typeparam name="TS">Type to filter for (must inherit from UnityEngine.Object)</typeparam>
        /// <param name="objects">Array of Unity Objects to filter</param>
        /// <returns>Array of objects of type TS, with null objects filtered out</returns>
        protected TS[] Select<TS>(Object[] objects) where TS : Object
            => objects
                .Where(o => o.GetType() == typeof(TS) || o.GetType().IsSubclassOf(typeof(TS)))
                .Select(o => o as TS).ToArray();
        
        protected TS[] Extract<TS>(Object[] objects) where TS : Component
        {
            return objects
                .Select(obj =>
                {
                    if (obj is GameObject go)
                        return go.GetComponent<TS>();
                    if (obj is Component comp)
                        return comp.GetComponent<TS>();
                    return null;
                })
                .Where(c => c != null)
                .ToArray();
        }
        
        protected TS[] ExtractOrSelect<TS>(Object[] objects) where TS : Object
        {
            var targetType = typeof(TS);

            // If TS is a Component or a subclass of Component, extract via GetComponent
            if (typeof(Component).IsAssignableFrom(targetType))
            {
                return objects
                    .Select(obj =>
                    {
                        if (obj is GameObject go)
                            return go.GetComponent<TS>();
                        if (obj is Component comp)
                            return comp.GetComponent<TS>();
                        return null;
                    })
                    .Where(c => c != null)
                    .ToArray();
            }

            // Otherwise, just filter and cast directly
            return objects
                .Where(o => targetType.IsAssignableFrom(o.GetType()))
                .Cast<TS>()
                .ToArray();
        }
        
        /// <summary>
        /// Gets the first object of the specified type from an array of Unity Objects.
        /// Useful for finding singleton components or primary objects of a specific type.
        /// Returns null if no matching object is found.
        /// </summary>
        /// <typeparam name="TS">Type to find (must inherit from UnityEngine.Object)</typeparam>
        /// <param name="objects">Array of Unity Objects to search</param>
        /// <returns>The first object of type TS, or null if none exists</returns>
        protected TS First<TS>(Object[] objects) where TS : Object
            => (TS)objects
                .FirstOrDefault(o => o.GetType() == typeof(TS) || o.GetType().IsSubclassOf(typeof(TS)));
    }
}