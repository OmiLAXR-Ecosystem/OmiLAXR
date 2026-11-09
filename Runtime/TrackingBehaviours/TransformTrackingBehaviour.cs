/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/

using System;
using System.ComponentModel;
using OmiLAXR.Components;
using UnityEngine;

namespace OmiLAXR.TrackingBehaviours
{
    /// <summary>
    /// Monitors transform changes (position, rotation, scale) in GameObjects with TransformWatcher components.
    /// Supports both interval-based polling and real-time change detection modes.
    /// Can selectively ignore specific transform properties based on configuration.
    /// </summary>
    [AddComponentMenu("OmiLAXR / 3) Tracking Behaviours / Transform Tracking Behaviour")]
    [Description("Tracks position, rotation and scale changes in a game object holding <TransformWatcher> component.")]
    public class TransformTrackingBehaviour : ScheduledTrackingBehaviour<TransformWatcher>
    {
        [Tooltip("Only track TransformWatchers with this scope. Empty tracks all of them.")]
        public string scope;

        /// <summary>
        /// Enable real-time change detection by binding to TransformWatcher events.
        /// When false, only interval-based checking is performed.
        /// </summary>
        public bool detectOnChange = true;

        /// <summary>
        /// Flags to ignore specific transform components during tracking.
        /// Allows selective monitoring of only position, rotation, or scale changes.
        /// </summary>
        public TransformWatcher.TransformIgnore ignore;

        /// <summary>
        /// Event triggered when a tracked object's position changes.
        /// Provides both the TransformWatcher and detailed change information.
        /// </summary>
        [Gesture("Locomotion"), Action("Translation")]
        public readonly TrackingBehaviourEvent<TransformWatcher, TransformWatcher.TransformChange> OnChangedPosition =
            new TrackingBehaviourEvent<TransformWatcher, TransformWatcher.TransformChange>();

        /// <summary>
        /// Event triggered when a tracked object's rotation changes.
        /// Includes old and new rotation values in the change data.
        /// </summary>
        [Gesture("Locomotion"), Action("Rotation")]
        public readonly TrackingBehaviourEvent<TransformWatcher, TransformWatcher.TransformChange> OnChangedRotation =
            new TrackingBehaviourEvent<TransformWatcher, TransformWatcher.TransformChange>();

        /// <summary>
        /// Event triggered when a tracked object's scale changes.
        /// Captures scale modifications with before/after values.
        /// </summary>
        [Gesture("Locomotion"), Action("Scale")]
        public readonly TrackingBehaviourEvent<TransformWatcher, TransformWatcher.TransformChange> OnChangedScale =
            new TrackingBehaviourEvent<TransformWatcher, TransformWatcher.TransformChange>();

        /// <summary>
        /// Event triggered when a tracked object's forward changes.
        /// Captures forward modifications with before/after values.
        /// </summary>
        [Gesture("Locomotion"), Action("Forward")]
        public readonly TrackingBehaviourEvent<TransformWatcher, TransformWatcher.TransformChange> OnChangedForward =
            new TrackingBehaviourEvent<TransformWatcher, TransformWatcher.TransformChange>();

        /// <summary>
        /// Sets up real-time event bindings for detected TransformWatcher components.
        /// Binds to each watcher's change events for immediate notification of transform modifications.
        /// </summary>
        /// <param name="transformWatchers">Array of TransformWatcher components to monitor</param>
        protected override void AfterFilteredObjects(TransformWatcher[] transformWatchers)
        {
        }

        protected override void Run()
        {
            var arr = SelectedObjects;
            if (arr == null || arr.Count == 0)
                return;

            var scopeLocal = scope;
            var filterScope = !string.IsNullOrEmpty(scopeLocal);
            var ign = ignore;
            var detect = detectOnChange;

            for (var i = 0; i < arr.Count; i++)
            {
                var tw = arr[i];
                if (tw == null)
                    continue;

                if (filterScope && tw.scope != scopeLocal)
                    continue;

                var state = tw.GetTransformChangeState(ign);

                if (!detect || state.Position.HasChanged)
                    OnChangedPosition?.Invoke(this, tw, state.Position);

                if (!detect || state.Rotation.HasChanged)
                    OnChangedRotation?.Invoke(this, tw, state.Rotation);

                if (!detect || state.Scale.HasChanged)
                    OnChangedScale?.Invoke(this, tw, state.Scale);

                if (!detect || state.Forward.HasChanged)
                    OnChangedForward?.Invoke(this, tw, state.Forward);
            }
        }
    }
}