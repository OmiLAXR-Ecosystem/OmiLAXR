/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Scripting;
using OmiLAXR.Utils;

namespace OmiLAXR.TrackingBehaviours.System
{
    /// <summary>
    /// Comprehensive system-level tracking behavior that monitors application lifecycle events.
    /// Tracks game start, quit, focus changes, and pause states with precise timestamps.
    /// Uses Unity's runtime initialization hooks to capture early application events.
    /// </summary>
    [AddComponentMenu("OmiLAXR / 3) Tracking Behaviours / System Tracking Behaviour")]
    [Description("Tracks states of game (started, quit, paused, resumed, focused, unfocused) and detects state changes.")]
    public class SystemTrackingBehaviour : TrackingBehaviour
    {
        /// <summary>
        /// Event triggered when the application/game starts with the start timestamp.
        /// </summary>
        public readonly TrackingBehaviourEvent<DateTime> OnStartedGame = new TrackingBehaviourEvent<DateTime>();
        
        /// <summary>
        /// Event triggered when the application/game quits with the quit timestamp.
        /// </summary>
        public readonly TrackingBehaviourEvent<DateTime> OnQuitGame = new TrackingBehaviourEvent<DateTime>();
        
        /// <summary>
        /// Event triggered when application focus changes.
        /// Second parameter indicates if the application gained (true) or lost (false) focus.
        /// </summary>
        public readonly TrackingBehaviourEvent<DateTime, bool> OnFocusedGame = new TrackingBehaviourEvent<DateTime, bool>();
        
        /// <summary>
        /// Event triggered when application pause state changes.
        /// Second parameter indicates if the application was paused (true) or resumed (false).
        /// </summary>
        public readonly TrackingBehaviourEvent<DateTime, bool> OnPausedGame = new TrackingBehaviourEvent<DateTime, bool>();
        
        /// <summary>
        /// Static controller for capturing application start time before most systems initialize.
        /// Uses Unity's RuntimeInitializeOnLoadMethod to hook into very early application lifecycle.
        /// </summary>
        [Preserve]
        protected static class SystemStartController
        {
            /// <summary>
            /// Timestamp of when the application started initializing.
            /// Captured at the earliest possible moment during Unity's startup sequence.
            /// </summary>
            public static DateTime? StartTime;
            
            /// <summary>
            /// Called automatically by Unity before the splash screen appears.
            /// Records the precise moment the application begins initialization.
            /// </summary>
            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
            private static void GameStarted()
            {
                StartTime = Clock.Now;
            }
        }

        // State tracking variables
        private bool _isFirstRun = true;
        private bool _sendStartSignal;
        private bool _isRunning;

        /// <summary>
        /// Manually triggers the game start signal if it hasn't been sent yet.
        /// Uses the captured start time from SystemStartController or current time as fallback.
        /// </summary>
        public void SendStartSignal()
        {
            if (_sendStartSignal)
                return;
            
            // Use captured start time or current time as fallback
            var now = SystemStartController.StartTime.HasValue ? SystemStartController.StartTime.Value : Clock.Now;
            OnStartedGame?.Invoke(this, now);
            _sendStartSignal = true;
        }

        /// <summary>
        /// Called when the tracking pipeline starts.
        /// Ensures the start signal is sent and marks the system as running.
        /// </summary>
        /// <param name="pipeline">The pipeline that started this behavior</param>
        protected override void OnStartedPipeline(Pipeline pipeline)
        {
            if (_isRunning) return;
            base.OnStartedPipeline(pipeline);
            SendStartSignal();
            _isRunning = true;
        }

        protected override void OnQuitPipeline(Pipeline pipeline)
        {
            base.OnQuitPipeline(pipeline);
            TriggerEnd();
        }

        /// <summary>
        /// Internal method to handle application termination.
        /// Triggers the quit event if the system is currently running.
        /// </summary>
        private void TriggerEnd()
        {
            if (!_isRunning) return;
            OnQuitGame.Invoke(this, Clock.Now);
            _isRunning = false;
        }
        
        /// <summary>
        /// Unity callback for application focus changes.
        /// Triggers focus events with timestamp and focus state.
        /// </summary>
        /// <param name="hasFocus">True if application gained focus, false if lost</param>
        protected virtual void OnApplicationFocus(bool hasFocus)
        {
            if (!enabled)
                return;
            OnFocusedGame?.Invoke(this, Clock.Now, hasFocus);
        }

        /// <summary>
        /// Unity callback for application pause state changes.
        /// Ignores the first call which occurs during initial application startup.
        /// </summary>
        /// <param name="pauseStatus">True if application was paused, false if resumed</param>
        protected virtual void OnApplicationPause(bool pauseStatus)
        {
            if (!enabled)
                return;
            
            // Skip the first pause callback which happens during app initialization
            if (_isFirstRun)
            {
                _isFirstRun = false;
                return;
            }
            OnPausedGame?.Invoke(this, Clock.Now, pauseStatus);
        }
    }
}