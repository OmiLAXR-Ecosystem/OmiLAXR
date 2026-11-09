/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System;
using System.ComponentModel;
using UnityEngine;
using OmiLAXR.Utils;

namespace OmiLAXR.TrackingBehaviours.Learner
{
    /// <summary>
    /// Tracks session lifecycle events including start and stop.
    /// Automatically handles pipeline lifecycle and application shutdown events.
    /// </summary>
    [AddComponentMenu("OmiLAXR / 3) Tracking Behaviours / Session Tracking Behaviour")]
    [Description("Tracks the start and end of a session.")]
    public class SessionTrackingBehaviour : TrackingBehaviour
    {
        /// <summary>
        /// Indicates if a session is currently running.
        /// </summary>
        private bool _isRunning;
        
        /// <summary>
        /// Event triggered when a session starts.
        /// </summary>
        public readonly TrackingBehaviourEvent<DateTime> OnSessionStarted = new TrackingBehaviourEvent<DateTime>();
        
        /// <summary>
        /// Event triggered when a session stops.
        /// </summary>
        public readonly TrackingBehaviourEvent<DateTime> OnSessionStopped = new TrackingBehaviourEvent<DateTime>();
        
        /// <summary>
        /// Called when the pipeline starts. Triggers session start if not already running.
        /// </summary>
        protected override void BeforeStartedPipeline(Pipeline pipeline)
        {
            if (_isRunning) return;
            OnSessionStarted.Invoke(this, Clock.Now);
            _isRunning = true;
        }

        /// <summary>
        /// Called when the pipeline stops. Triggers session end.
        /// </summary>
        protected override void BeforeStoppedPipeline(Pipeline pipeline)
        {
            TriggerEnd();
        }

        /// <summary>
        /// Ends the current session if one is running.
        /// </summary>
        private void TriggerEnd()
        {
            if (!_isRunning) return;
            OnSessionStopped.Invoke(this, Clock.Now);
            _isRunning = false;
        }
    }
}