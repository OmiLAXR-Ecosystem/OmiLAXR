/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System;
using System.Collections.Generic;
using UnityEngine.Events;

namespace OmiLAXR.TrackingBehaviours
{
    /// <summary>
    /// Common interface for all tracking behavior event types.
    /// Provides standard lifecycle management methods for event cleanup and state control.
    /// </summary>
    public interface ITrackingBehaviourEvent
    {
        /// <summary>
        /// Removes all Unity Event and Action bindings from this event.
        /// </summary>
        void UnbindAll();
        
        /// <summary>
        /// Clears all registered action handlers from this event.
        /// </summary>
        void ClearActions();
        
        /// <summary>
        /// Performs complete cleanup of this event, unbinding all listeners and clearing actions.
        /// </summary>
        void Dispose();
        
        /// <summary>
        /// Temporarily disables this event, preventing it from triggering.
        /// </summary>
        void Mute();
        
        /// <summary>
        /// Gets or sets whether this event is currently disabled.
        /// </summary>
        bool IsDisabled { get; set; }
    }
    
    /// <summary>
    /// Basic tracking behavior event with no additional parameters.
    /// Supports binding to Unity Events and custom action handlers.
    /// </summary>
    public class TrackingBehaviourEvent : ITrackingBehaviourEvent
    {
        /// <summary>
        /// List of all registered action handlers for debugging and management.
        /// </summary>
        public readonly List<TrackingBehaviourAction> Actions = new List<TrackingBehaviourAction>();
        
        /// <summary>
        /// Main event that handlers subscribe to for notifications.
        /// </summary>
        public event TrackingBehaviourAction Action;
        
        /// <summary>
        /// Dictionary tracking Unity Event bindings for proper cleanup.
        /// </summary>
        private Dictionary<UnityEvent, UnityAction> _unityBinds =
            new Dictionary<UnityEvent, UnityAction>();
            
        /// <summary>
        /// Flag indicating if this event is currently muted/disabled.
        /// </summary>
        public bool IsDisabled { get; set; }

        /// <summary>
        /// Registers a new action handler with this event.
        /// Adds the handler to both the event subscription and tracking list.
        /// </summary>
        /// <param name="action">Action handler to register</param>
        public void AddHandler(TrackingBehaviourAction action)
        {
            Action += action;
            Actions.Add(action);
        }

        /// <summary>
        /// Triggers this event with the specified owner, unless the event is disabled.
        /// </summary>
        /// <param name="owner">The tracking behavior that owns this event</param>
        public void Invoke(ITrackingBehaviour owner)
        {
            if (IsDisabled)
                return;
            Action?.Invoke(owner);
        }

        /// <summary>
        /// Removes all registered action handlers from this event.
        /// Unsubscribes each action and clears the tracking list.
        /// </summary>
        public void ClearActions()
        {
            foreach (var a in Actions)
            {
                Action -= a;
            }
            Actions.Clear();
        }

        /// <summary>
        /// Performs complete cleanup by unbinding Unity Events and clearing actions.
        /// </summary>
        public void Dispose()
        {
            UnbindAll();
            ClearActions();
        }
        
        /// <summary>
        /// Disables this event, preventing future invocations.
        /// </summary>
        public void Mute()
        {
            IsDisabled = true;
        }

        /// <summary>
        /// Re-enables this event after being muted.
        /// </summary>
        public void Unmute()
        {
            IsDisabled = false;
        }
        
        /// <summary>
        /// Convenience method to bind a Unity Event to trigger this event with a specific owner.
        /// </summary>
        /// <param name="unityEvent">Unity Event to bind to</param>
        /// <param name="owner">Owner to pass when the Unity Event triggers</param>
        public void Bind(UnityEvent unityEvent, ITrackingBehaviour owner)
            => Bind(unityEvent, () => Invoke(owner));
        
        /// <summary>
        /// Binds a Unity Event to execute a custom action when triggered.
        /// Tracks the binding for proper cleanup later.
        /// </summary>
        /// <param name="unityEvent">Unity Event to bind to</param>
        /// <param name="invoker">Action to execute when Unity Event triggers</param>
        public void Bind(UnityEvent unityEvent, UnityAction invoker)
        {
            // Use version-appropriate dictionary method
#if UNITY_2020 || UNITY_2019
            if (!_unityBinds.ContainsKey(unityEvent))
                _unityBinds.Add(unityEvent, invoker);
#else
            if (!_unityBinds.TryAdd(unityEvent, invoker))
                return;
#endif
            unityEvent.AddListener(invoker);
        }

        /// <summary>
        /// Removes a previously bound Unity Event listener.
        /// </summary>
        /// <param name="unityEvent">Unity Event to unbind from</param>
        public void Unbind(UnityEvent unityEvent)
        {
            unityEvent.RemoveListener(_unityBinds[unityEvent]);
            _unityBinds.Remove(unityEvent);
        }

        /// <summary>
        /// Removes all Unity Event bindings from this event.
        /// </summary>
        public void UnbindAll()
        {
            foreach (var ev in _unityBinds.Keys)
            {
                ev.RemoveListener(_unityBinds[ev]);
            }
            _unityBinds.Clear();
        }
    }
    
    /// <summary>
    /// Tracking behavior event with one typed parameter.
    /// Extends the basic event to include strongly-typed data in event notifications.
    /// </summary>
    /// <typeparam name="T">Type of the event parameter</typeparam>
    public class TrackingBehaviourEvent<T> : ITrackingBehaviourEvent
    {
        /// <summary>
        /// List of all registered typed action handlers.
        /// </summary>
        public readonly List<TrackingBehaviourAction<T>> Actions = new List<TrackingBehaviourAction<T>>();
        
        /// <summary>
        /// Main typed event that handlers subscribe to.
        /// </summary>
        public event TrackingBehaviourAction<T> Action;
        
        /// <summary>
        /// Dictionary tracking Unity Event bindings for cleanup.
        /// </summary>
        private Dictionary<UnityEvent, UnityAction> _unityBinds =
            new Dictionary<UnityEvent, UnityAction>();
            
        /// <summary>
        /// Flag indicating if this event is currently disabled.
        /// </summary>
        public bool IsDisabled { get; set; }

        /// <summary>
        /// Registers a typed action handler with this event.
        /// </summary>
        /// <param name="action">Typed action handler to register</param>
        public void AddHandler(TrackingBehaviourAction<T> action)
        {
            Action += action;
            Actions.Add(action);
        }

        /// <summary>
        /// Triggers this event with the specified owner and typed argument.
        /// </summary>
        /// <param name="owner">The tracking behavior that owns this event</param>
        /// <param name="arg">Typed argument to pass to handlers</param>
        public void Invoke(ITrackingBehaviour owner, T arg)
        {
            if (IsDisabled)
                return;
            Action?.Invoke(owner, arg);
        }

        /// <summary>
        /// Removes all registered action handlers from this event.
        /// </summary>
        public void ClearActions()
        {
            foreach (var a in Actions)
            {
                Action -= a;
            }
            Actions.Clear();
        }

        /// <summary>
        /// Performs complete cleanup of this event.
        /// </summary>
        public void Dispose()
        {
            ClearActions();
            UnbindAll();
        }

        /// <summary>
        /// Convenience method to bind Unity Event with pre-configured owner and argument.
        /// </summary>
        /// <param name="unityEvent">Unity Event to bind to</param>
        /// <param name="owner">Owner to pass when triggered</param>
        /// <param name="arg">Argument to pass when triggered</param>
        public void Bind(UnityEvent unityEvent, ITrackingBehaviour owner, T arg)
         => Bind(unityEvent, () => Invoke(owner, arg));

        /// <summary>
        /// Binds Unity Event to custom action.
        /// </summary>
        /// <param name="unityEvent">Unity Event to bind to</param>
        /// <param name="invoker">Action to execute when triggered</param>
        public void Bind(UnityEvent unityEvent, UnityAction invoker)
        {
#if UNITY_2020 || UNITY_2019
            if (!_unityBinds.ContainsKey(unityEvent))
                _unityBinds.Add(unityEvent, invoker);
#else
            if (!_unityBinds.TryAdd(unityEvent, invoker))
                return;
#endif
            unityEvent.AddListener(invoker);
        }
        

        /// <summary>
        /// Removes Unity Event binding.
        /// </summary>
        /// <param name="unityEvent">Unity Event to unbind</param>
        public void Unbind(UnityEvent unityEvent)
        {
            unityEvent.RemoveListener(_unityBinds[unityEvent]);
            _unityBinds.Remove(unityEvent);
        }

        /// <summary>
        /// Disables this event.
        /// </summary>
        public void Mute()
        {
            IsDisabled = true;
        }

        /// <summary>
        /// Re-enables this event.
        /// </summary>
        public void Unmute()
        {
            IsDisabled = false;
        }

        /// <summary>
        /// Removes all Unity Event bindings.
        /// </summary>
        public void UnbindAll()
        {
            foreach (var ev in _unityBinds.Keys)
            {
                ev.RemoveListener(_unityBinds[ev]);
            }
            _unityBinds.Clear();
        }
    }
    
    /// <summary>
    /// Tracking behavior event with sender and value parameters.
    /// Provides enhanced event data with both the sending component and associated value.
    /// </summary>
    /// <typeparam name="TSender">Type of the component that sent the event</typeparam>
    /// <typeparam name="TValue">Type of the value associated with the event</typeparam>
    public class TrackingBehaviourEvent<TSender, TValue> : ITrackingBehaviourEvent
    {
        /// <summary>
        /// List of all registered action handlers for this two-parameter event.
        /// </summary>
        public readonly List<TrackingBehaviourAction<TSender, TValue>> Actions = new List<TrackingBehaviourAction<TSender, TValue>>();
        
        /// <summary>
        /// Main event with sender and value parameters.
        /// </summary>
        public event TrackingBehaviourAction<TSender, TValue> Action;

        /// <summary>
        /// Unity Event bindings with typed values for cleanup tracking.
        /// </summary>
        private Dictionary<UnityEvent<TValue>, UnityAction<TValue>> _unityBinds =
            new Dictionary<UnityEvent<TValue>, UnityAction<TValue>>();
        
        /// <summary>
        /// Action bindings for C# Action<TValue> delegates.
        /// </summary>
        private Dictionary<Action<TValue>, Action<TValue>> _actionBinds =
            new Dictionary<Action<TValue>, Action<TValue>>();
            
        /// <summary>
        /// Disabled state flag for this event.
        /// </summary>
        public bool IsDisabled { get; set; }
        
        /// <summary>
        /// Gets the number of registered handlers for this event.
        /// </summary>
        public int HandlerCount => Actions.Count;

        /// <summary>
        /// Adds a new handler for this two-parameter event.
        /// </summary>
        /// <param name="action">Handler to register</param>
        public void AddHandler(TrackingBehaviourAction<TSender, TValue> action)
        {
            Action += action;
            Actions.Add(action);
        }

        /// <summary>
        /// Invokes this event with owner, sender, and value parameters.
        /// </summary>
        /// <param name="owner">Tracking behavior that owns this event</param>
        /// <param name="sender">Component that generated the event</param>
        /// <param name="value">Value associated with the event</param>
        public void Invoke(ITrackingBehaviour owner, TSender sender, TValue value)
        {
            if (IsDisabled)
                return;
            Action?.Invoke(owner, sender, value);
        }
        
        /// <summary>Disables this event.</summary>
        public void Mute() => IsDisabled = true;

        /// <summary>Re-enables this event.</summary>
        public void Unmute() => IsDisabled = false;
        
        /// <summary>
        /// Clears all registered action handlers.
        /// </summary>
        public void ClearActions()
        {
            foreach (var a in Actions)
            {
                Action -= a;
            }
            Actions.Clear();
        }

        /// <summary>
        /// Performs complete event cleanup.
        /// </summary>
        public void Dispose()
        {
            UnbindAll();
            ClearActions();
        }
        
        /// <summary>
        /// Binds to a typed Unity Event with custom invoker.
        /// </summary>
        /// <param name="unityEvent">Unity Event to bind to</param>
        /// <param name="invoker">Custom invoker action</param>
        public void Bind(UnityEvent<TValue> unityEvent, UnityAction<TValue> invoker)
        {
#if !UNITY_2021_1_OR_NEWER
            if (!_unityBinds.ContainsKey(unityEvent))
                _unityBinds.Add(unityEvent, invoker);
#else
            if (!_unityBinds.TryAdd(unityEvent, invoker))
                return;
#endif
            unityEvent.AddListener(invoker);
        }

        /// <summary>
        /// Removes Unity Event binding.
        /// </summary>
        /// <param name="unityEvent">Unity Event to unbind</param>
        public void Unbind(UnityEvent<TValue> unityEvent)
        {
           unityEvent.RemoveListener(_unityBinds[unityEvent]);
           _unityBinds.Remove(unityEvent);
        }

        /// <summary>
        /// Binds to a C# Action delegate with custom invoker.
        /// </summary>
        /// <param name="action">Action to bind to</param>
        /// <param name="invoker">Custom invoker</param>
        [Obsolete("Has no effect: delegates are immutable, so binding to a passed Action cannot reach the caller's delegate. Use Bind(UnityEvent<TValue>, UnityAction<TValue>) or subscribe the invoker directly.")]
        public void Bind(Action<TValue> action, Action<TValue> invoker)
        {
#if !UNITY_2021_1_OR_NEWER
            if (!_actionBinds.ContainsKey(action))
                _actionBinds.Add(action, invoker);
#else
            if (!_actionBinds.TryAdd(action, invoker))
                return;
#endif
            action += invoker;
        }
        
        /// <summary>
        /// Removes Action binding.
        /// </summary>
        /// <param name="action">Action to unbind</param>
        [Obsolete("Has no effect: delegates are immutable, so binding to a passed Action cannot reach the caller's delegate. Use Bind(UnityEvent<TValue>, UnityAction<TValue>) or subscribe the invoker directly.")]
        public void Unbind(Action<TValue> action)
        {
            action -= _actionBinds[action];
            _actionBinds.Remove(action);
        }
        
        /// <summary>
        /// Removes all bindings (Unity Events and Actions).
        /// </summary>
        public void UnbindAll()
        {
            // Clean up Unity Event bindings
            foreach (var ev in _unityBinds.Keys)
            {
                ev.RemoveListener(_unityBinds[ev]);
            }
            _unityBinds.Clear();
            
            // Clean up Action bindings
            void unbind(Action<TValue> action)
            {
                action -= _actionBinds[action];
            }
            foreach (var ev in _actionBinds.Keys)
            {
                unbind(ev);
            }
            _actionBinds.Clear();
        }
    }
    
    /// <summary>
    /// Tracking behavior event with sender, value, and additional arguments.
    /// Most comprehensive event type supporting three parameters for complex event scenarios.
    /// </summary>
    /// <typeparam name="TSender">Type of the sending component</typeparam>
    /// <typeparam name="TValue">Type of the primary value</typeparam>
    /// <typeparam name="TArgs">Type of additional arguments</typeparam>
    public class TrackingBehaviourEvent<TSender, TValue, TArgs> : ITrackingBehaviourEvent
    {
        /// <summary>
        /// List of registered three-parameter action handlers.
        /// </summary>
        public readonly List<TrackingBehaviourAction<TSender, TValue, TArgs>> Actions = new List<TrackingBehaviourAction<TSender, TValue, TArgs>>();
        
        /// <summary>
        /// Main three-parameter event.
        /// </summary>
        public event TrackingBehaviourAction<TSender, TValue, TArgs> Action;

        /// <summary>Unity Event bindings tracking dictionary.</summary>
        private Dictionary<UnityEvent<TValue>, UnityAction<TValue>> _unityBinds =
            new Dictionary<UnityEvent<TValue>, UnityAction<TValue>>();
            
        /// <summary>Action bindings tracking dictionary.</summary>
        private Dictionary<Action<TValue>, Action<TValue>> _actionBinds =
            new Dictionary<Action<TValue>, Action<TValue>>();
            
        /// <summary>Event disabled state flag.</summary>
        public bool IsDisabled { get; set; }

        /// <summary>
        /// Registers a three-parameter action handler.
        /// </summary>
        /// <param name="action">Handler to register</param>
        public void AddHandler(TrackingBehaviourAction<TSender, TValue, TArgs> action)
        {
            Action += action;
            Actions.Add(action);
        }
        
        /// <summary>
        /// Invokes event with all three parameters.
        /// </summary>
        /// <param name="owner">Tracking behavior owner</param>
        /// <param name="sender">Sending component</param>
        /// <param name="value">Primary value</param>
        /// <param name="args">Additional arguments</param>
        public void Invoke(ITrackingBehaviour owner, TSender sender, TValue value, TArgs args)
        {
            if (IsDisabled)
                return;
            Action?.Invoke(owner, sender, value, args);
        }
        
        /// <summary>Disables the event.</summary>
        public void Mute() => IsDisabled = true;

        /// <summary>Re-enables the event.</summary>
        public void Unmute() => IsDisabled = false;
        
        /// <summary>Clears all action handlers.</summary>
        public void ClearActions()
        {
            foreach (var a in Actions)
            {
                Action -= a;
            }
            Actions.Clear();
        }

        /// <summary>Complete event cleanup.</summary>
        public void Dispose()
        {
            UnbindAll();
            ClearActions();
        }

        /// <summary>Binds to C# Action with invoker.</summary>
        [Obsolete("Has no effect: delegates are immutable, so binding to a passed Action cannot reach the caller's delegate. Use Bind(UnityEvent<TValue>, UnityAction<TValue>) or subscribe the invoker directly.")]
        public void Bind(Action<TValue> action, Action<TValue> invoker)
        {
#if !UNITY_2021_1_OR_NEWER
            if (!_actionBinds.ContainsKey(action))
                _actionBinds.Add(action, invoker);
#else
            if (!_actionBinds.TryAdd(action, invoker))
                return;
#endif
            action += invoker;
        }
        
        /// <summary>Removes Action binding.</summary>
        [Obsolete("Has no effect: delegates are immutable, so binding to a passed Action cannot reach the caller's delegate. Use Bind(UnityEvent<TValue>, UnityAction<TValue>) or subscribe the invoker directly.")]
        public void Unbind(Action<TValue> action)
        {
            action -= _actionBinds[action];
            _actionBinds.Remove(action);
        }
        
        /// <summary>Binds to Unity Event with invoker.</summary>
        public void Bind(UnityEvent<TValue> unityEvent, UnityAction<TValue> invoker)
        {
            #if !UNITY_2021_1_OR_NEWER
            if (!_unityBinds.ContainsKey(unityEvent))
                _unityBinds.Add(unityEvent, invoker);
            #else
            if (!_unityBinds.TryAdd(unityEvent, invoker))
                return;
            #endif
            unityEvent.AddListener(invoker);
        }

        /// <summary>Removes Unity Event binding.</summary>
        public void Unbind(UnityEvent<TValue> unityEvent)
        {
            unityEvent.RemoveListener(_unityBinds[unityEvent]);
            _unityBinds.Remove(unityEvent);
        }

        /// <summary>Removes all bindings (Unity Events and Actions).</summary>
        public void UnbindAll()
        {
            // Clean up Unity Events
            foreach (var ev in _unityBinds.Keys)
            {
                ev.RemoveListener(_unityBinds[ev]);
            }
            _unityBinds.Clear();

            // Clean up Actions
            void unbind(Action<TValue> action)
            {
                action -= _actionBinds[action];
            }
            foreach (var ev in _actionBinds.Keys)
            {
                unbind(ev);
            }
            _actionBinds.Clear();
        }
    }
}