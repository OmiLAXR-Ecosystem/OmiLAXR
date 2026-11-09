/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System.ComponentModel;
using UnityEngine;

namespace OmiLAXR.Context
{
    /// <summary>
    /// Learning context component that tracks and monitors frame rate performance.
    /// Provides both instantaneous and smoothed average FPS measurements for performance analytics.
    /// Uses exponential moving average to provide stable average measurements without memory overhead.
    /// </summary>
    [AddComponentMenu("OmiLAXR / Scenario Context / FPS Monitor")]
    [DisallowMultipleComponent]
    [Description("Tracks Frames per Seconds.")]
    public sealed class FpsMonitor : LearningContext, IFpsProvider
    {
        /// <summary>
        /// Static reference to the singleton instance.
        /// </summary>
        private static FpsMonitor _instance;
        
        /// <summary>
        /// Singleton accessor for the FPS monitor instance.
        /// Ensures consistent performance monitoring across the application.
        /// </summary>
        public static FpsMonitor Instance => GetInstance(ref _instance);

        /// <summary>
        /// Current instantaneous frames per second measurement.
        /// Updated every frame based on the current frame time.
        /// </summary>
        [SerializeField, InspectorName("FPS"), ReadOnly]
        private int currentFPS;
        
        /// <summary>
        /// Smoothed average frames per second using exponential moving average.
        /// Provides a stable measurement that reduces noise from frame time variations.
        /// </summary>
        [SerializeField, InspectorName("Average FPS"), ReadOnly]
        private float averageFPS;
        
        /// <summary>
        /// Public accessor for the current instantaneous FPS value.
        /// </summary>
        public int CurrentFPS => currentFPS;

        /// <inheritdoc />
        public int? GetFps() => enabled ? CurrentFPS : (int?)null;
        
        /// <summary>
        /// Public accessor for the current smoothed average FPS value.
        /// </summary>
        public float CurrentAverageFPS => averageFPS;
        
        /// <summary>
        /// Smoothed frame delta time for calculating instantaneous FPS.
        /// Uses a smoothing factor to reduce noise in frame time measurements.
        /// </summary>
        private float _deltaTime;
        
        /// <summary>
        /// Time interval (in seconds) between average FPS updates.
        /// Controls how frequently the exponential moving average is recalculated.
        /// </summary>
        private float _fpsUpdateInterval = 0.5f;
        
        /// <summary>
        /// Timer tracking time since last average FPS update.
        /// </summary>
        private float _fpsTimer;
        
        /// <summary>
        /// Flag indicating if this is the first average calculation.
        /// Used to initialize the exponential moving average with the first measurement.
        /// </summary>
        private bool _isFirstAverage = true;
        
        /// <summary>
        /// Smoothing factor for exponential moving average calculation.
        /// Range: 0-1, where higher values give more weight to recent measurements.
        /// </summary>
        private float _smoothingFactor = 0.1f;

        /// <summary>
        /// Unity Update method called once per frame.
        /// Calculates instantaneous FPS and updates the smoothed average periodically.
        /// </summary>
        private void Update()
        {
            // Smooth the frame delta time to reduce noise
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
            
            // Calculate instantaneous FPS from smoothed delta time
            currentFPS = (int)(1.0f / _deltaTime);
            
            // Update timer for average FPS calculation
            _fpsTimer += Time.unscaledDeltaTime;
            
            // Update average FPS at specified intervals
            if (_fpsTimer >= _fpsUpdateInterval)
            {
                if (_isFirstAverage)
                {
                    // Initialize average with first measurement
                    averageFPS = currentFPS;
                    _isFirstAverage = false;
                }
                else
                {
                    // Calculate exponential moving average
                    // Formula: newAvg = currentValue * alpha + oldAvg * (1 - alpha)
                    averageFPS = currentFPS * _smoothingFactor + averageFPS * (1 - _smoothingFactor);
                }
                
                // Reset timer for next update
                _fpsTimer = 0.0f;
            }
        }
    }
}