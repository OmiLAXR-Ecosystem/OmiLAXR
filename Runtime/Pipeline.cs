/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OmiLAXR.Composers;
using OmiLAXR.Endpoints;
using OmiLAXR.Listeners;
using OmiLAXR.Filters;
using OmiLAXR.TrackingBehaviours;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OmiLAXR
{
    /// <summary>
    /// Core orchestration component for the OmiLAXR learning analytics pipeline system.
    /// Manages the complete lifecycle of object detection, filtering, tracking, and analytics generation.
    /// Coordinates between listeners, filters, tracking behaviors, and data providers to create
    /// a comprehensive learning analytics solution for Unity applications.
    /// </summary>
    [AddComponentMenu("OmiLAXR / Core / Pipeline")]
    [DefaultExecutionOrder(0)] // Execute at standard time, after extensions and settings
    public class Pipeline : MonoBehaviour, IPipelineEvents
    {
        /// <summary>
        /// Memory for holding running pipelines.
        /// </summary>
        private static readonly List<Pipeline> RunningPipelines = new List<Pipeline>();
        
        /// <summary>
        /// Amount of running pipelines.
        /// </summary>
        public static int RunningPipelinesCount => RunningPipelines.Count;
        
        /// <summary>
        /// Is true, if this pipeline is the last running.
        /// </summary>
        public bool IsLastRunningPipeline => RunningPipelinesCount == 1 && RunningPipelines[0].Equals(this);
        
        /// <summary>
        /// Indicates whether the pipeline is currently active and processing objects.
        /// Based on the GameObject's active state in the scene hierarchy.
        /// </summary>
        public bool IsRunning => gameObject.activeSelf;
        
        /// <summary>
        /// Reference to the Actor component representing the learner being tracked.
        /// Can be an individual Actor or an ActorGroup for multi-user scenarios.
        /// </summary>
        public Actor actor;
        
        /// <summary>
        /// Reference to the Instructor component that manages this pipeline's learning context.
        /// Provides oversight and coordination for analytics generation and delivery.
        /// </summary>
        public Instructor instructor;

        /// <summary>
        /// Collection of Listener components that detect objects in the scene.
        /// Listeners form the first stage of the pipeline by finding trackable objects.
        /// </summary>
        public readonly List<Listener> Listeners = new List<Listener>();
        
        /// <summary>
        /// Collection of DataProvider components that handle analytics data processing.
        /// DataProviders compose and send learning analytics statements to configured endpoints.
        /// </summary>
        public readonly List<DataProvider> DataProviders = new List<DataProvider>();
        
        /// <summary>
        /// Collection of TrackingBehaviour components that monitor detected objects.
        /// TrackingBehaviours generate events and gather data about learner interactions.
        /// </summary>
        public readonly List<ITrackingBehaviour> TrackingBehaviours = new List<ITrackingBehaviour>();
        
        /// <summary>
        /// Collection of Filter components that refine the set of tracked objects.
        /// Filters remove unwanted objects and focus tracking on relevant interactions.
        /// </summary>
        public readonly List<Filter> Filters = new List<Filter>();

        /// <summary>
        /// Dictionary mapping action names to their associated tracking behavior events.
        /// Built dynamically from ActionAttribute annotations on tracking behavior events.
        /// </summary>
        public readonly Dictionary<string, List<ITrackingBehaviourEvent>> Actions = new Dictionary<string, List<ITrackingBehaviourEvent>>();
        
        /// <summary>
        /// Dictionary mapping gesture names to their associated tracking behavior events.
        /// Built dynamically from GestureAttribute annotations on tracking behavior events.
        /// </summary>
        public readonly Dictionary<string, List<ITrackingBehaviourEvent>> Gestures = new Dictionary<string, List<ITrackingBehaviourEvent>>();

        /// <summary>
        /// Collection of pipeline extensions that have been applied to this pipeline.
        /// Extensions provide modular enhancements without modifying the core pipeline code.
        /// </summary>
        public readonly List<IPipelineExtension> Extensions = new List<IPipelineExtension>();

        /// <summary>
        /// Array of ActorDataProvider components specific to this pipeline's actor.
        /// Cached during initialization for efficient access to actor-specific data providers.
        /// </summary>
        public ActorDataProvider[] ActorDataProviders { get; protected set; }

        /// <summary>
        /// Finds the first pipeline of the specified type in the scene.
        /// Uses Unity version-appropriate methods for optimal compatibility.
        /// </summary>
        /// <typeparam name="T">Type of pipeline to find</typeparam>
        /// <returns>First pipeline instance of the specified type, or null if none found</returns>
        #if UNITY_2023_1_OR_NEWER
        public static T GetPipeline<T>() where T : Pipeline
            => FindFirstObjectByType<T>();
        #else
        public static T GetPipeline<T>() where T : Pipeline
            => FindObjectOfType<T>();
        #endif

        /// <summary>
        /// Finds the pipelines of any type in the scene.
        /// Uses Unity version-appropriate methods for optimal compatibility.
        /// </summary>
        /// <returns>All pipelines found, or null if none exist</returns>
        #if UNITY_2023_1_OR_NEWER 
        public static Pipeline[] GetAll() => FindObjectsByType<Pipeline>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        #else
        public static Pipeline[] GetAll() => FindObjectsOfType<Pipeline>();
        #endif

        /// <summary>
        /// Finds the only active pipelines of any type in the scene.
        /// Uses Unity version-appropriate methods for optimal compatibility.
        /// </summary>
        /// <returns>All active pipelines found, or null if none exist</returns>
        public static Pipeline[] GetAllRunningPipelines() => RunningPipelines.ToArray();
        
        /// <summary>
        /// Gets the first DataProvider of the specified type from this pipeline's collection.
        /// Provides type-safe access to specific data provider implementations.
        /// </summary>
        /// <typeparam name="T">Type of DataProvider to find</typeparam>
        /// <returns>First DataProvider of the specified type, or null if none found</returns>
        public T GetDataProvider<T>() where T : DataProvider
            => DataProviders.OfType<T>().Select(dp => dp).FirstOrDefault();
        
        /// <summary>
        /// Gets the first TrackingBehaviour of the specified type from this pipeline's collection.
        /// Provides type-safe access to specific tracking behavior implementations.
        /// </summary>
        /// <typeparam name="T">Type of TrackingBehaviour to find (must be a PipelineComponent)</typeparam>
        /// <returns>First TrackingBehaviour of the specified type, or null if none found</returns>
        public T GetTrackingBehaviour<T>() where T : PipelineComponent, ITrackingBehaviour
            => TrackingBehaviours.OfType<T>().Select(dp => dp).FirstOrDefault();
        
        /// <summary>
        /// Gets the first Filter of the specified type from this pipeline's collection.
        /// Provides type-safe access to specific filter implementations.
        /// </summary>
        /// <typeparam name="T">Type of Filter to find</typeparam>
        /// <returns>First Filter of the specified type, or null if none found</returns>
        public T GetFilters<T>() where T : Filter
            => Filters.OfType<T>().Select(dp => dp).FirstOrDefault();
        
        /// <summary>
        /// Gets the first Listener of the specified type from this pipeline's collection.
        /// Provides type-safe access to specific listener implementations.
        /// </summary>
        /// <typeparam name="T">Type of Listener to find</typeparam>
        /// <returns>First Listener of the specified type, or null if none found</returns>
        public T GetListener<T>() where T : Listener
            => Listeners.OfType<T>().Select(listener => listener).FirstOrDefault();

        /// <summary>Event fired after pipeline initialization is complete but before startup.</summary>
        public event PipelineInitHandler AfterInit;
        
        /// <summary>Event fired immediately before the pipeline starts processing objects.</summary>
        public event PipelineStartedHandler BeforeStartedPipeline;
        
        /// <summary>Event fired immediately after the pipeline has started processing objects.</summary>
        public event PipelineStartedHandler AfterStartedPipeline;
        
        /// <summary>Event fired during the collection phase of pipeline initialization.</summary>
        public event PipelineInitHandler OnCollect; 
        
        /// <summary>Event fired when listeners find objects, before filtering is applied.</summary>
        public event Action<Object[]> AfterFoundObjects;
        
        /// <summary>Event fired after filters have processed the found objects.</summary>
        public event Action<Object[]> AfterFilteredObjects;
        
        /// <summary>Event fired when a composer creates an analytics statement.</summary>
        public event ComposerAction<IStatement> AfterComposedStatement; 
        
        /// <summary>Event fired before an endpoint sends a statement to its destination.</summary>
        public event EndpointAction<IStatement> BeforeSendStatement; 
        
        /// <summary>Event fired after an endpoint successfully sends a statement.</summary>
        public event EndpointAction<IStatement> AfterSendStatement;
        
        /// <summary>Event fired immediately before the pipeline stops processing objects.</summary>
        public event PipelineStoppedHandler BeforeStoppedPipeline; 
        
        /// <summary>Event fired immediately after the pipeline has stopped processing objects.</summary>
        public event PipelineStoppedHandler AfterStoppedPipeline; 
        
        public event PipelineStoppedHandler OnQuit;

        /// <summary>
        /// Collection of objects currently being tracked by the pipeline.
        /// Populated by listeners and filtered through the pipeline's filter chain.
        /// </summary>
        public readonly List<Object> TrackingObjects = new List<Object>();
        
        /// <summary>
        /// Flag to prevent multiple cleanup calls during shutdown sequences.
        /// Ensures cleanup operations are performed exactly once per lifecycle.
        /// </summary>
        private bool _cleanupCalled;
        
        /// <summary>
        /// Flag to indicate that the application is quitting.
        /// Used to prevent pipeline cleanup during application shutdown.
        /// </summary>
        private bool _isQuittingApplication;
        
        /// <summary>
        /// Flag to prevent multiple startup calls during initialization sequences.
        /// Ensures startup operations are performed exactly once per lifecycle.
        /// </summary>
        private bool _startupCalled;

        /// <summary>
        /// Adds a pipeline component to the appropriate collection based on its type.
        /// Automatically categorizes components and handles extension registration.
        /// </summary>
        /// <param name="comp">PipelineComponent to add to the pipeline</param>
        public void Add(PipelineComponent comp)
        {
            var type = comp.GetType();
            
            // Add to appropriate collection based on component type
            if (type.IsSubclassOf(typeof(Listener)))
            {
                if (!Listeners.Contains(comp))
                    Listeners.Add(comp as Listener);
            }
            else if (type.IsSubclassOf(typeof(Filter)))
            {
                if (!Filters.Contains(comp))
                    Filters.Add(comp as Filter);
            }
            else if (typeof(ITrackingBehaviour).IsAssignableFrom(type))
            {
                var tb = comp as ITrackingBehaviour;
                if (!TrackingBehaviours.Contains(tb))
                    TrackingBehaviours.Add(tb);
            }
            else if (typeof(IPipelineExtension).IsAssignableFrom(type))
            {
                // Handle pipeline extensions with special registration logic
                var ext = comp as IPipelineExtension;
                if (ext != null && !Extensions.Contains(ext))
                {
                    ext.Extend(this); // Apply extension to this pipeline (registers itself in Extensions)
                    if (!Extensions.Contains(ext))
                        Extensions.Add(ext);
                }
            }
        }

        /// <summary>
        /// Finds the Actor component associated with this pipeline.
        /// Checks for ActorGroup first, then falls back to individual Actor.
        /// </summary>
        /// <returns>Actor or ActorGroup component, or null if none found</returns>
        private static void AddMissing<TItem>(List<TItem> target, IEnumerable<TItem> items)
        {
            foreach (var item in items)
            {
                if (!target.Contains(item))
                    target.Add(item);
            }
        }

        private Actor FindActor()
        {
            var actorGroup = GetComponent<ActorGroup>();
            return actorGroup ?? GetComponent<Actor>();
        }
        
        /// <summary>
        /// Initializes the pipeline by discovering and configuring all components.
        /// Sets up event bindings, collects components, and prepares for operation.
        /// Called once during the pipeline's lifetime before startup.
        /// </summary>
        private void Init()
        {
            // Ensure we have an actor for this pipeline
            if (actor == null)
                actor = FindActor();

            // Cache actor-specific data providers for efficient access
            ActorDataProviders = GetComponentsInChildren<ActorDataProvider>(false);
            
            // Discover and collect all pipeline components from hierarchy
            // Init runs on every (re)start, so only add components that are not registered yet
            AddMissing(TrackingBehaviours, GetComponentsInChildren<ITrackingBehaviour>(false));
            AddMissing(Listeners, GetComponentsInChildren<Listener>(false));
            AddMissing(Filters, GetComponentsInChildren<Filter>(false));
            
            // Find all available data providers in the scene (not just children)
#if UNITY_2023_1_OR_NEWER
            AddMissing(DataProviders, FindObjectsByType<DataProvider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
#else
            AddMissing(DataProviders, FindObjectsOfType<DataProvider>(false));
#endif
            
            // Bind pipeline events to data provider events for analytics processing
            foreach (var dp in DataProviders)
            {
                // Connect composer events for statement creation
                foreach (var c in dp.Composers)
                {
                    c.AfterComposed -= AfterComposed; // avoid duplicate subscriptions on restart
                    c.AfterComposed += AfterComposed;
                }
                
                // Connect endpoint events for statement delivery
                foreach (var ep in dp.Endpoints)
                {
                    ep.OnSendingStatement -= OnSendingStatement;
                    ep.OnSentStatement -= OnSentStatement;
                    ep.OnSendingStatement += OnSendingStatement;
                    ep.OnSentStatement += OnSentStatement;
                }
            }

            // Calculate component counts for logging
            var composersCount = DataProviders.Aggregate(0, (i, provider) => i + provider.Composers.Count);
            var hooksCount = DataProviders.Aggregate(0, (i, provider) => i + provider.Hooks.Count);
            
            // Notify interested parties that collection phase is beginning
            OnCollect?.Invoke(this);
            
            // Log initialization summary for debugging
            Log($"Initialized with {Listeners.Count} listeners, {Filters.Count} filters, {TrackingBehaviours.Count} tracking behaviours, {composersCount} composers, {hooksCount} hooks and {DataProviders.Count} data providers" );
            
            // Notify that initialization is complete
            AfterInit?.Invoke(this);
        }

        /// <summary>
        /// Event handler for composer statement creation events.
        /// Forwards the event to pipeline subscribers for processing.
        /// </summary>
        /// <param name="composer">Composer that created the statement</param>
        /// <param name="statement">Statement that was composed</param>
        private void AfterComposed(IComposer composer, IStatement statement)
            => AfterComposedStatement?.Invoke(composer, statement);
        
        /// <summary>
        /// Event handler for endpoint statement sending events.
        /// Forwards the event to pipeline subscribers before delivery.
        /// </summary>
        /// <param name="endpoint">Endpoint that will send the statement</param>
        /// <param name="statement">Statement being sent</param>
        private void OnSendingStatement(Endpoint endpoint, IStatement statement)
            => BeforeSendStatement?.Invoke(endpoint, statement);
        
        /// <summary>
        /// Event handler for endpoint statement sent events.
        /// Forwards the event to pipeline subscribers after successful delivery.
        /// </summary>
        /// <param name="endpoint">Endpoint that sent the statement</param>
        /// <param name="statement">Statement that was sent</param>
        private void OnSentStatement(Endpoint endpoint, IStatement statement)
            => AfterSendStatement?.Invoke(endpoint, statement);
        
        /// <summary>
        /// Analyzes tracking behaviors to build action and gesture event mappings.
        /// Uses reflection to discover events with ActionAttribute and GestureAttribute annotations.
        /// Called during pipeline initialization to enable event categorization and filtering.
        /// </summary>
        private void CollectGesturesAndActions()
        {
            var tbs = TrackingBehaviours.ToArray();
            Actions.Clear();
            Gestures.Clear();

            // Process each tracking behavior for annotated events
            foreach (var ts in tbs)
            {
                var properties = ts.GetTrackingBehaviourEvents();
                    
                foreach (var p in properties)
                {
                    var ev = p.GetValue(ts) as ITrackingBehaviourEvent;
                    var actionAttrs = p.GetCustomAttributes<ActionAttribute>();
                    var gestureAttrs = p.GetCustomAttributes<GestureAttribute>();

                    // Collect action-attributed events
                    foreach (var actionAttr in actionAttrs)
                    {
                        if (Actions.ContainsKey(actionAttr.Name))
                            Actions[actionAttr.Name].Add(ev);
                        else 
                            Actions.Add(actionAttr.Name, new List<ITrackingBehaviourEvent>() { ev });
                    }

                    // Collect gesture-attributed events
                    foreach (var gestureAttr in gestureAttrs)
                    {
                        if (Gestures.ContainsKey(gestureAttr.Name))
                            Gestures[gestureAttr.Name].Add(ev);
                        else 
                            Gestures.Add(gestureAttr.Name, new List<ITrackingBehaviourEvent>() { ev });
                    }
                }
            }
        }
        
        /// <summary>
        /// Logs a formatted message with pipeline context information.
        /// Automatically includes the pipeline type name for easier debugging.
        /// </summary>
        /// <param name="message">Format string for the log message</param>
        /// <param name="ps">Parameters for string formatting</param>
        protected void Log(string message, params object[] ps)
            => DebugLog.OmiLAXR.Print($"({GetType().Name}) " + message, ps);

        /// <summary>
        /// Unity OnEnable callback that triggers pipeline startup.
        /// Called when the GameObject becomes active in the scene.
        /// </summary>
        private void OnEnable()
        {
            ApplicationShutdownManager.Register(this);
            Startup();
        }

        /// <summary>
        /// Starts the pipeline operation by initializing components and beginning object tracking.
        /// Ensures startup operations are performed only once per lifecycle.
        /// Connects listener events and begins the object detection and tracking process.
        /// </summary>
        private void Startup()
        {
            // Prevent duplicate startup calls
            if (_startupCalled)
                return;
            
            // Initialize pipeline if not already done
            Init();
            
            _startupCalled = true;
            
            // Build action/gesture mappings from tracking behaviors
            CollectGesturesAndActions();
            TrackingObjects.Clear();

            // Start all enabled listeners to begin object detection
            foreach (var listener in Listeners)
            {
                listener.OnFoundObjects += FoundObjects; // Connect event handler
                if (listener.enabled)
                    listener.StartListening();
            }
            
            // Notify subscribers that pipeline is starting
            BeforeStartedPipeline?.Invoke(this);
            Log($"Started Pipeline with {TrackingObjects.Count} tracking target objects...");
            AfterStartedPipeline?.Invoke(this);
            
            if (!RunningPipelines.Contains(this))
                RunningPipelines.Add(this);
            
            _cleanupCalled = false; // Reset cleanup flag for this lifecycle
        }
        
        /// <summary>
        /// Unity OnDisable callback that triggers pipeline cleanup.
        /// Called when the GameObject becomes inactive in the scene.
        /// </summary>
        private void OnDisable()
        {
            Cleanup();
        }

        /// <summary>
        /// Stops pipeline operation and cleans up all resources and event subscriptions.
        /// Ensures cleanup operations are performed only once per lifecycle.
        /// Disconnects events, stops endpoints, and resets state for potential restart.
        /// </summary>
        private void Cleanup()
        {
            // Prevent duplicate cleanup calls
            if (_cleanupCalled)
                return;
            
            Log("Cleaning up Pipeline...");
            
            _cleanupCalled = true;
            BeforeStoppedPipeline?.Invoke(this);
            
            // Clear tracked objects
            TrackingObjects.Clear();
            
            // Disconnect listener events and stop listening
            foreach (var listener in Listeners.Where(l => l.enabled))
            {
                if (listener == null)
                    continue;
                listener.OnFoundObjects -= FoundObjects;
            }
            
            // Check if this pipeline is the last one
            if (IsLastRunningPipeline)
            {
                // Trigger finally OnQuit event.
                if (_isQuittingApplication)
                {
                    OnQuit?.Invoke(this); // todo: still not perfect!!!
                }
                
                // Stop all enabled endpoints from sending data
                foreach (var endpoint in DataProviders.SelectMany(dp => dp.Endpoints))
                {
                    if (endpoint == null || !endpoint.enabled)
                        continue;
                    endpoint.StopSending();
                }
            }
            Log("Cleaned and stopped Pipeline.");
            AfterStoppedPipeline?.Invoke(this);

            if (IsLastRunningPipeline)
            {
                // Disconnect data provider events to prevent memory leaks
                foreach (var dp in DataProviders)
                {
                    foreach (var c in dp.Composers)
                    {
                        c.AfterComposed -= AfterComposed;
                    }
                    foreach (var ep in dp.Endpoints)
                    {
                        ep.OnSendingStatement -= OnSendingStatement;
                        ep.OnSentStatement -= OnSentStatement;
                    }
                }
            }

            if (RunningPipelines.Contains(this))
                RunningPipelines.Remove(this);
            
            _startupCalled = false; // Reset startup flag for potential restart
        }

        /// <summary>
        /// Manually starts the pipeline by activating the GameObject and calling startup logic.
        /// Provides programmatic control over pipeline activation.
        /// </summary>
        public void StartPipeline()
        {
            if (IsRunning)
                return;
            
            Log("Starting Pipeline...");
            gameObject.SetActive(true);
            Startup();
        }

        /// <summary>
        /// Unity OnApplicationQuit callback that ensures cleanup on application exit.
        /// Guarantees proper resource cleanup even if normal shutdown doesn't occur.
        /// </summary>
        protected virtual void OnAppQuit()
        {
            _isQuittingApplication = true;
            Cleanup();
        }

        /// <summary>
        /// Unity OnDestroy callback that ensures cleanup when the GameObject is destroyed.
        /// Guarantees proper resource cleanup during scene changes or object destruction.
        /// </summary>
        private void OnDestroy()
        {
            Cleanup();
        }

        /// <summary>
        /// Manually stops the pipeline by calling cleanup logic and deactivating the GameObject.
        /// Provides programmatic control over pipeline deactivation.
        /// </summary>
        public void StopPipeline()
        {
            if (!IsRunning)
                return;
            
            Log("Stopping Pipeline...");

            Cleanup();
            _startupCalled = false;
            
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Enables or disables specific actions by name, affecting their event processing.
        /// Provides runtime control over which actions are tracked and processed.
        /// </summary>
        /// <param name="disabled">Whether to disable (true) or enable (false) the actions</param>
        /// <param name="names">Collection of action names to affect, or null for all actions</param>
        public void SetDisabledActions(bool disabled, IEnumerable<string> names = null)
        {
            foreach (var pair in Actions.Where(p => names == null || names.Contains(p.Key)))
            {
                pair.Value.ForEach(v => v.IsDisabled = disabled);
            }
        }
        
        /// <summary>
        /// Enables or disables specific gestures by name, affecting their event processing.
        /// Provides runtime control over which gestures are tracked and processed.
        /// </summary>
        /// <param name="disabled">Whether to disable (true) or enable (false) the gestures</param>
        /// <param name="names">Collection of gesture names to affect, or null for all gestures</param>
        public void SetDisabledGestures(bool disabled, IEnumerable<string> names = null)
        {
            foreach (var pair in Gestures.Where(p => names == null || names.Contains(p.Key)))
            {
                pair.Value.ForEach(v => v.IsDisabled = disabled);
            }
        }

        /// <summary>
        /// Event handler for objects found by listeners.
        /// Applies the filter chain to found objects and adds results to tracking collection.
        /// Core method that processes objects through the pipeline's filtering stages.
        /// </summary>
        /// <param name="objects">Array of objects found by listeners</param>
        private void FoundObjects(Object[] objects)
        {
            // Notify subscribers about found objects before filtering
            AfterFoundObjects?.Invoke(objects);

            // Apply all enabled filters in sequence to refine the object set
            objects = ObjectFilters.Apply(Filters, objects);
            
            // Notify subscribers about filtered objects
            AfterFilteredObjects?.Invoke(objects);
            
            // Add filtered objects to the tracking collection
            TrackingObjects.AddRange(objects);
        }
    }
}