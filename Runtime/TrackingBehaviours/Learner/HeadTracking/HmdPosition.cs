/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using OmiLAXR.Utils;

namespace OmiLAXR.TrackingBehaviours.Learner.HeadTracking
{
    /// <summary>
    /// Singleton class for tracking HMD position data using the main camera.
    /// Provides utilities for position sampling and extrema detection.
    /// </summary>
    public class HmdPosition
    {
        /// <summary>
        /// Reference to the main camera for position tracking.
        /// </summary>
        private readonly Camera _mMainCamera;

        /// <summary>
        /// Singleton instance holder.
        /// </summary>
        private static HmdPosition _instance;
        
#if UNITY_2019
        /// <summary>
        /// Singleton accessor with lazy initialization for Unity 2019.
        /// </summary>
        public static HmdPosition Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new HmdPosition();
                return _instance;
            }
        } 
#else 
        /// <summary>
        /// Singleton accessor with null-coalescing assignment for Unity 2020+.
        /// </summary>
        public static HmdPosition Instance => _instance ??= new HmdPosition();
#endif
        
        /// <summary>
        /// Private constructor initializes main camera reference.
        /// </summary>
        private HmdPosition()
        {
            _mMainCamera = Camera.main;
        }

        /// <summary>
        /// Gets current HMD position with timestamp. Returns default if no main camera exists.
        /// </summary>
        public HmdTimedPosition GetHmdPosition() => _mMainCamera != null ? new HmdTimedPosition(Clock.Now, _mMainCamera.transform.position) : default;

        /// <summary>
        /// Finds maximum or minimum position in list along specified axis.
        /// </summary>
        /// <param name="maxmin">"max" for maximum, "min" for minimum</param>
        /// <param name="axis">"x", "y", or "z" axis</param>
        /// <param name="list">List of positions to search</param>
        /// <returns>Extrema position and its index, or default values if list is empty</returns>
        public (HmdTimedPosition hmdPosition, int index) GetMaxMinPosition(string maxmin, string axis, List<HmdTimedPosition> list)
        {
            if (list.Count > 0)
            {
                // Extract axis values from position list
                var values = new List<float>();
                switch (axis)
                {
                    case "x":
                        values.AddRange(list.Select(pos => pos.Position.x));
                        break;
                    case "y":
                        values.AddRange(list.Select(pos => pos.Position.y));
                        break;
                    case "z":
                        values.AddRange(list.Select(pos => pos.Position.z));
                        break;
                }

                switch (maxmin)
                {
                    // Find maximum value and its index
                    case "max":
                        var maxValue = values.Max();
                        var maxIndex = values.IndexOf(maxValue);
                        return (list[maxIndex], maxIndex);
                    
                    // Find minimum value and its index
                    case "min":
                        var minValue = values.Min();
                        var minIndex = values.IndexOf(minValue);
                        return (list[minIndex], minIndex);
                }
            }
            
            // Return default values if list is empty
            var defaultHmdPosition = new HmdTimedPosition();
            return (defaultHmdPosition, -1);
        }
    }
}