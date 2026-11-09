/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace OmiLAXR.TrackingBehaviours.Learner.HeadTracking
{
    /// <summary>
    /// Utility class for head gesture recognition and analysis.
    /// Provides algorithms for detecting nodding and shaking gestures from position data.
    /// </summary>
    public static class HeadUtils
    {
        /// <summary>
        /// Counts gestures in float coordinate list by detecting peaks above minimum amplitude.
        /// </summary>
        /// <param name="listOfCoordinates">List of float coordinates to analyze</param>
        /// <param name="minAmplitude">Minimum amplitude required for gesture detection</param>
        /// <returns>Number of detected gestures</returns>
        public static int GetNumOfGestures(List<float> listOfCoordinates, double minAmplitude)
        {
            var doubleList = listOfCoordinates.ConvertAll(x => (double)x);
            return GetNumOfGestures(doubleList, minAmplitude);
        }

        /// <summary>
        /// Counts gestures in HMD position list along specified axis.
        /// </summary>
        /// <param name="listOfCoordinates">List of HMD positions with timestamps</param>
        /// <param name="minAmplitude">Minimum amplitude required for gesture detection</param>
        /// <param name="axis">Axis to analyze ("x", "y", or "z")</param>
        /// <returns>Number of detected gestures</returns>
        public static int GetNumOfGestures(List<HmdTimedPosition> listOfCoordinates, double minAmplitude, string axis)
        {
            var doubleList = ConvertHmdPositionListIntoDoubleList(listOfCoordinates, axis);
            return GetNumOfGestures(doubleList, minAmplitude);
        }

        /// <summary>
        /// Core gesture counting algorithm using peak detection on smoothed data.
        /// </summary>
        /// <param name="listOfCoordinates">List of coordinates as doubles</param>
        /// <param name="minAmplitude">Minimum amplitude for peak detection</param>
        /// <returns>Maximum number of peaks or valleys detected</returns>
        private static int GetNumOfGestures(List<double> listOfCoordinates, double minAmplitude)
        {
            //smooth line for correct calculation of number of head shakes
            var smoothedLine = SmoothLine(listOfCoordinates, minAmplitude);

            //get max number of maxima and minima values
            var maxPeaks = FindMaxPeaks(smoothedLine);
            var minPeaks = FindMinPeaks(smoothedLine);
            //return the max number of maxima/minima values
            return Mathf.Max(minPeaks.Count, maxPeaks.Count);
        }

        /// <summary>
        /// Detects if gesture has finished by checking movement continuity.
        /// </summary>
        /// <param name="listOfCoordinates">List of HMD positions to check</param>
        /// <param name="positionContinuousElement">Starting position for continuity check</param>
        /// <param name="minDistance">Minimum distance threshold for gesture completion</param>
        /// <param name="axis">Axis to analyze ("x", "y", or "z")</param>
        /// <returns>True if gesture has finished</returns>
        public static bool GestureFinished(List<HmdTimedPosition> listOfCoordinates, int positionContinuousElement, double minDistance, string axis)
        {
            var floatList = ConvertHmdPositionListIntoFloatList(listOfCoordinates, axis);
            return GestureFinished(floatList, positionContinuousElement, minDistance);
        }
        
        /// <summary>
        /// Checks if recent movement amplitude is below threshold (gesture finished).
        /// </summary>
        /// <param name="listOfCoordinates">List of coordinate values</param>
        /// <param name="positionContinuousElement">Starting position for range check</param>
        /// <param name="minDistance">Minimum distance threshold</param>
        /// <returns>True if movement amplitude is below threshold</returns>
        public static bool GestureFinished(List<float> listOfCoordinates, int positionContinuousElement, double minDistance)
        {
            //get the maximum and minimum of sublist for continuous gesture check
            var maxSubDistance = listOfCoordinates.GetRange(positionContinuousElement, listOfCoordinates.Count - positionContinuousElement).Max();
            var minSubDistance = listOfCoordinates.GetRange(positionContinuousElement, listOfCoordinates.Count - positionContinuousElement).Min();

            return Math.Abs(maxSubDistance - minSubDistance) < minDistance;
        }

        /// <summary>
        /// Checks if movement amplitude is sufficient for gesture recognition.
        /// </summary>
        /// <param name="listOfCoordinates">List of HMD positions to analyze</param>
        /// <param name="minAmplitude">Minimum required amplitude</param>
        /// <param name="axis">Axis to check ("x", "y", or "z")</param>
        /// <returns>True if amplitude is sufficient</returns>
        public static bool EnoughAmplitude(List<HmdTimedPosition> listOfCoordinates, double minAmplitude, string axis)
        {
            var floatList = ConvertHmdPositionListIntoFloatList(listOfCoordinates, axis);

            return EnoughAmplitude(floatList, minAmplitude);
        }

        /// <summary>
        /// Verifies if amplitude between min/max values exceeds threshold.
        /// </summary>
        /// <param name="list">List of values to check</param>
        /// <param name="minAmplitude">Minimum amplitude threshold</param>
        /// <returns>True if amplitude exceeds threshold</returns>
        public static bool EnoughAmplitude(List<float> list, double minAmplitude)
        {
            if (list.Count <= 0) return false;
            var amplitude = Mathf.Abs(list.Max() - list.Min());
            return amplitude >= minAmplitude;
        }

        /// <summary>
        /// Smooths position data by filtering out small movements below threshold.
        /// </summary>
        /// <param name="distances">Input distance values</param>
        /// <param name="minDistanceGesture">Minimum distance for significant movement</param>
        /// <returns>Smoothed list with only significant changes</returns>
        private static List<double> SmoothLine(IReadOnlyList<double> distances, double minDistanceGesture)
        {
            var distancesSmoothed = new List<double> { distances[0] };
            var startValue = distances[0];

            foreach (var t in distances)
            {
                var res = Math.Abs(startValue - t);
                if (!(res >= minDistanceGesture)) continue;
                distancesSmoothed.Add(t);
                startValue = t;
            }
            return distancesSmoothed;
        }
        
        /// <summary>
        /// Finds start position for continuous gesture detection within time window.
        /// </summary>
        /// <param name="list">List of timestamped positions</param>
        /// <param name="continuousTimeBetweenGestures">Maximum time span for continuous gesture</param>
        /// <returns>Index of start position, or -1 if not found</returns>
        public static int GetPositionContinuousGesture(List<HmdTimedPosition> list, TimeSpan continuousTimeBetweenGestures)
        {
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if ((list[list.Count - 1].Timestamp - list[i].Timestamp) > continuousTimeBetweenGestures)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Clears position tracking lists.
        /// </summary>
        /// <param name="hmdPositions">HMD position list to clear</param>
        public static void ClearAllItems(ref List<HmdTimedPosition> hmdPositions)
        {
            var helpPointsOfFocus = new List<Vector3>();
            ClearAllItems(ref hmdPositions, ref helpPointsOfFocus);
        }

        /// <summary>
        /// Overload for clearing multiple tracking lists.
        /// </summary>
        /// <param name="hmdPositions">HMD position list to clear</param>
        /// <param name="pointsOfFocus">Points of focus list to clear</param>
        private static void ClearAllItems(ref List<HmdTimedPosition> hmdPositions, ref List<Vector3> pointsOfFocus)
        {
            var helpHmdPositionsManipulated = new List<HmdTimedPosition>();
            var helpDistanceHmdPositionsManipulated = new List<float>();
            ClearAllItems(ref hmdPositions, ref helpHmdPositionsManipulated, ref helpDistanceHmdPositionsManipulated, ref pointsOfFocus);
        }

        /// <summary>
        /// Comprehensive list clearing for tracking data.
        /// </summary>
        /// <param name="hmdPositions">HMD position list to clear</param>
        /// <param name="hmdPositionsManipulated">Manipulated HMD position list to clear</param>
        /// <param name="distanceHmdPositionsManipulated">Distance list to clear</param>
        public static void ClearAllItems(ref List<HmdTimedPosition> hmdPositions, ref List<HmdTimedPosition> hmdPositionsManipulated, ref List<float> distanceHmdPositionsManipulated)
        {
            var helpPointsOfFocus = new List<Vector3>();
            ClearAllItems(ref hmdPositions, ref hmdPositionsManipulated, ref distanceHmdPositionsManipulated, ref helpPointsOfFocus);
        }

        /// <summary>
        /// Master clear method for all tracking lists.
        /// </summary>
        /// <param name="hmdPositions">HMD position list to clear</param>
        /// <param name="hmdPositionsManipulated">Manipulated HMD position list to clear</param>
        /// <param name="distanceHmdPositionsManipulated">Distance list to clear</param>
        /// <param name="pointsOfFocus">Points of focus list to clear</param>
        public static void ClearAllItems(ref List<HmdTimedPosition> hmdPositions, ref List<HmdTimedPosition> hmdPositionsManipulated, ref List<float> distanceHmdPositionsManipulated, ref List<Vector3> pointsOfFocus)
        {
            hmdPositions.Clear();
            hmdPositionsManipulated.Clear();
            distanceHmdPositionsManipulated.Clear();
            pointsOfFocus.Clear();
        }
        
        /// <summary>
        /// Determines if movement qualifies as normal head movement (not gesture).
        /// </summary>
        /// <param name="axis">Axis to check ("x", "y", or "z")</param>
        /// <param name="hmdPositions">List of HMD positions to analyze</param>
        /// <param name="minNormalHeadMovements">Minimum threshold for normal movement</param>
        /// <returns>True if movement exceeds normal threshold</returns>
        public static bool NormalHeadMovement(string axis, List<HmdTimedPosition> hmdPositions, double minNormalHeadMovements)
        {
            // calculate if the difference between the maximum and minimum value of a list of hmdPositions is greater than a specific threshold (minNormalHeadMovements)
            var values = ConvertHmdPositionListIntoFloatList(hmdPositions, axis);
            return values.Count > 0 && (values.Max() - values.Min()) > minNormalHeadMovements;
        }

        /// <summary>
        /// Checks if enough time has passed for reliable gesture recognition.
        /// </summary>
        /// <param name="hmdPositions">List of HMD positions</param>
        /// <param name="minTimeForGestures">Minimum time required for gesture detection</param>
        /// <param name="startApplication">Reference to application start flag</param>
        /// <param name="timeSpanGestureReached">Reference to time span reached flag</param>
        public static void EnoughSamplesCollected(ref List<HmdTimedPosition> hmdPositions, TimeSpan minTimeForGestures, ref bool startApplication, ref bool timeSpanGestureReached)
        {
            var helpHmdPositionsManipulated = new List<HmdTimedPosition>();
            var helpDistanceHmdPositionsManipulated = new List<float>();
            EnoughSamplesCollected(ref hmdPositions, ref helpHmdPositionsManipulated, ref helpDistanceHmdPositionsManipulated, minTimeForGestures, ref startApplication, ref timeSpanGestureReached);
        }

        /// <summary>
        /// Internal sample collection validation with list management.
        /// </summary>
        /// <param name="hmdPositions">List of HMD positions</param>
        /// <param name="hmdPositionsManipulated">Manipulated position list</param>
        /// <param name="distanceHmdPositionsManipulated">Distance calculation list</param>
        /// <param name="minTimeForGestures">Minimum time threshold</param>
        /// <param name="startApplication">Reference to start application flag</param>
        /// <param name="timeSpanGestureReached">Reference to time span flag</param>
        private static void EnoughSamplesCollected(ref List<HmdTimedPosition> hmdPositions, ref List<HmdTimedPosition> hmdPositionsManipulated, ref List<float> distanceHmdPositionsManipulated, TimeSpan minTimeForGestures, ref bool startApplication, ref bool timeSpanGestureReached)
        {
            //calculate if timespan between first and last entry is enough for recognizing possible shakes
            if ((hmdPositions[hmdPositions.Count - 1].Timestamp - hmdPositions[0].Timestamp) > minTimeForGestures)
            {
                //first time after appication hast started, therefore clear all Lists for a correct recognition
                if (startApplication)
                {
                    ClearAllItems(ref hmdPositions, ref hmdPositionsManipulated, ref distanceHmdPositionsManipulated);
                    startApplication = false;
                }
                timeSpanGestureReached = true;
            }
            else
            {
                timeSpanGestureReached = false;
            }
        }
        
        /// <summary>
        /// Finds all local maxima in a data series.
        /// </summary>
        /// <param name="list">Data series to analyze</param>
        /// <returns>List of maximum peak values</returns>
        private static List<double> FindMaxPeaks(IReadOnlyList<double> list)
        {
            var peakList = new List<double>();
            for (var i = 1; i < (list.Count - 1); i++)
            {
                if ((list[i - 1] < list[i]) && (list[i] > list[i + 1]))
                    peakList.Add(list[i]);
            }
            return peakList;
        }

        /// <summary>
        /// Finds all local minima in a data series.
        /// </summary>
        /// <param name="list">Data series to analyze</param>
        /// <returns>List of minimum peak values</returns>
        private static List<double> FindMinPeaks(IReadOnlyList<double> list)
        {
            var peakList = new List<double>();
            for (var i = 1; i < (list.Count - 1); i++)
            {
                if ((list[i - 1] > list[i]) && (list[i] < list[i + 1]))
                    peakList.Add(list[i]);
            }
            return peakList;
        }
        
        /// <summary>
        /// Converts HMD position list to float list for specified axis.
        /// </summary>
        /// <param name="listHmdPositions">List of HMD positions</param>
        /// <param name="axis">Axis to extract ("x", "y", or "z")</param>
        /// <returns>List of float values for the specified axis</returns>
        private static List<float> ConvertHmdPositionListIntoFloatList(List<HmdTimedPosition> listHmdPositions, string axis)
        {
            var floatList = new List<float>();
            foreach (var pos in listHmdPositions)
            {
                switch (axis)
                {
                    case "x": floatList.Add(pos.Position.x); break;
                    case "y": floatList.Add(pos.Position.y); break;
                    case "z": floatList.Add(pos.Position.z); break;
                }
            }
            return floatList;
        }
        
        /// <summary>
        /// Converts HMD position list to double list for specified axis.
        /// </summary>
        /// <param name="listHmdPositions">List of HMD positions</param>
        /// <param name="axis">Axis to extract ("x", "y", or "z")</param>
        /// <returns>List of double values for the specified axis</returns>
        private static List<double> ConvertHmdPositionListIntoDoubleList(List<HmdTimedPosition> listHmdPositions, string axis)
        {
            var floatList = new List<double>();
            foreach (var pos in listHmdPositions)
            {
                switch (axis)
                {
                    case "x": floatList.Add(pos.Position.x); break;
                    case "y": floatList.Add(pos.Position.y); break;
                    case "z": floatList.Add(pos.Position.z); break;
                }
            }
            return floatList;
        }
        
        /// <summary>
        /// Saves coordinate data to text file for research and parameter optimization.
        /// </summary>
        /// <param name="list">List of coordinate values to save</param>
        /// <param name="listName">Name identifier for the output file</param>
        public static void SaveLine(List<float> list, string listName)
        {
            var myFilePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            using (var outputFile = new StreamWriter(Path.Combine(myFilePath, "Line_" + listName + ".txt"), true))
            {
                foreach (double elem in list)
                    outputFile.WriteLine(elem.ToString(CultureInfo.CurrentCulture));
            }
        }
    }
}