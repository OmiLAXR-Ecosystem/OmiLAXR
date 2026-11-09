using System;
using System.Collections.Generic;
using OmiLAXR.Components.Gaze;
using OmiLAXR.Components.Gaze.Fixation;
using OmiLAXR.Components.Gaze.Pursuit;
using OmiLAXR.Components.Gaze.Saccade;
using OmiLAXR.Extensions;
using OmiLAXR.Types;
using UnityEngine;

namespace OmiLAXR.TrackingBehaviours.Learner.Gaze
{
    /// <summary>
    /// Base class for eye tracking of an XR device. Uses one <see cref="GazeDetector"/> per eye and reports
    /// fixations (per eye and binocular), saccades and smooth pursuits in addition to gaze enter and leave.
    /// Device modules provide the eye data and the gaze detectors.
    /// </summary>
    public abstract class EyeGazeTrackingBehaviour : GazeTrackingBehaviour<EyeData>
    {
        public abstract string DeviceName { get; }
        
        [Tooltip("Decides when an eye movement counts as a saccade. Defaults to a velocity threshold.")]
        public SaccadeLogic saccadeLogic;
        [Tooltip("Decides when an eye movement counts as a smooth pursuit of a moving object.")]
        public PursuitLogic pursuitLogic;

        /// <summary>
        /// Rapid movement occurs between two fixations. Track gaze shifts and identify scanning patterns.
        /// </summary>
        [Gesture("Eyes"), Action("Saccade")]
        public readonly TrackingBehaviourEvent<EyeData, SaccadeData> OnSaccaded = new TrackingBehaviourEvent<EyeData, SaccadeData>();

        [Gesture("Eyes"), Action("Pursuit")]
        public readonly TrackingBehaviourEvent<EyeData, PursuitData> OnPursuit =
            new TrackingBehaviourEvent<EyeData, PursuitData>();
        
        private readonly Dictionary<GazeDetector, Eye> _eyeGazeCache = new Dictionary<GazeDetector, Eye>();
        
        private GameObject _currentActiveObjectWithBothEyes;
        private FixationDetector _fixationDetectorLeft;
        private FixationDetector _fixationDetectorRight;

        [Tooltip("Gaze detector of the left eye. Assigned automatically when the pipeline finds the eye gaze detectors.")]
        public GazeDetector leftEyeDetector;
        [Tooltip("Gaze detector of the right eye. Assigned automatically when the pipeline finds the eye gaze detectors.")]
        public GazeDetector rightEyeDetector;

        private class FixationHit
        {
            public Eye Eye;
            public FixationData Data;
            public GazeHit Hit;
        }

        private readonly FixationHit[] _fixationHits = new FixationHit[2];
        
        [SerializeField]
        public Eye createDataForSide = Eye.Both | Eye.Left | Eye.Right;

        protected override void Reset()
        {
            base.Reset();
            if (!saccadeLogic)
                saccadeLogic = ScriptableObject.CreateInstance<SaccadeLogicVelocityThreshold>();
            if (!pursuitLogic)
                pursuitLogic = ScriptableObject.CreateInstance<PursuitLogicBasic>();
        }

        protected override void Run()
        {
            if (!leftEyeDetector || !rightEyeDetector)
            {
                DebugLog.OmiLAXR.Error("Eye detectors not assigned. Disabling.");
                enabled = false;
                StopSchedules();
                return;
            }
            
            if (createDataForSide.HasFlag(Eye.Left))
                leftEyeDetector.PerformRaycast(true);

            if (createDataForSide.HasFlag(Eye.Right))
                rightEyeDetector.PerformRaycast(true);
        }
        
        private void ProcessBinocularFixation(GazeHit left, GazeHit right, int targetFixationCount = 0)
        {
            if (left?.RayHit.collider == null || right?.RayHit.collider == null)
            {
                DebugLog.OmiLAXR.Warning("Binocular fixation failed: one or both eyes have no valid collider.");
                return;
            }

            // Prüfen, ob beide Augen dasselbe Objekt fixieren
            if (left.RayHit.collider != right.RayHit.collider)
            {
                Debug.Log($"Binocular mismatch: Left on {left.RayHit.collider.name}, Right on {right.RayHit.collider.name}");
                return;
            }

            // Fixationspunkt berechnen (mittig zwischen beiden Treffern)
            var fixationPoint = (left.RayHit.point + right.RayHit.point) * 0.5f;

            // Vergenzwinkel berechnen
            var vergenceAngle = Vector3.Angle(left.GazeDirectionInWorld, right.GazeDirectionInWorld);
            Debug.Log($"Binocular fixation on {left.RayHit.collider.name} with vergence angle {vergenceAngle:F2}°");

            // Schwellwert: typische Fixations-Vergenzwinkel sind < 5°
            if (vergenceAngle >= 5.0f)
            {
                DebugLog.OmiLAXR.Print($"Vergence angle too large: {vergenceAngle:F2}°");
                return;
            }

            // Gaze-Daten für beide Augen erzeugen
            var leftGazeData = GenerateGazeData(left);
            var rightGazeData = GenerateGazeData(right);
            var eyeData = CombineEyeData(left, leftGazeData, right, rightGazeData);

            var startTime = DateTimeExt.GetFirstNotNull(_fixationDetectorLeft.CurrentFixationStart, _fixationDetectorRight.CurrentFixationStart);
            var endTime = DateTimeExt.GetFirstNotNull(_fixationDetectorLeft.CurrentFixationEnd, _fixationDetectorRight.CurrentFixationEnd);

            if (_fixationDetectorLeft.CurrentFixationStart.HasValue &&
                _fixationDetectorRight.CurrentFixationStart.HasValue)
            {
                startTime = DateTimeExt.Min(_fixationDetectorLeft.CurrentFixationStart.Value,
                    _fixationDetectorRight.CurrentFixationStart.Value);
            }

            if (_fixationDetectorLeft.CurrentFixationEnd.HasValue &&
                _fixationDetectorRight.CurrentFixationEnd.HasValue)
            {
                endTime = DateTimeExt.Max(_fixationDetectorLeft.CurrentFixationEnd.Value,
                    _fixationDetectorRight.CurrentFixationEnd.Value);
            }
            
            // FixationData erzeugen (Dauer und AOI können später ergänzt werden)
            var fixationData = new FixationData(
                left,
                fixationPoint,
                targetFixationCount,
                startTime, 
                endTime
            );

            // Event auslösen
            OnFixated.Invoke(this, eyeData, fixationData);
        }


        private static EyeData CombineEyeData(GazeHit left, GazeData leftData, GazeHit right, GazeData rightData)
        {
            var leftGazeData = (EyeData)leftData;
            var rightGazeData = (EyeData)rightData;
            return new EyeData(
                left,
                (leftGazeData.GazeOriginWorld + rightGazeData.GazeOriginWorld) * 0.5f, 
                (left.RayHit.point + right.RayHit.point) * 0.5f, 
                Eye.Both,
                new Frustum(), (leftGazeData.EyeOpenness + rightGazeData.EyeOpenness) * 0.5f,
                Mathf.Min(leftGazeData.EyeConfidence, rightGazeData.EyeConfidence), // Conservative approach
                    leftGazeData.EyeDepth,
                leftGazeData.EyeHeight,
                (leftGazeData.PupilDiameterMillimeters + rightGazeData.PupilDiameterMillimeters) / 2.0f
            );
        }

        private void DetectBothEyesDetector(GazeHit gazeHit, FixationData fixationData)
        {
            var eye = GetEye(gazeHit.GazeDetector);
            var index = -1;
            if (eye == Eye.Left)
                index = 0;
            else if (eye == Eye.Right)
                index = 1;

            if (index < 0)
                return;

            _fixationHits[index] = new FixationHit() { Hit = gazeHit, Data = fixationData, Eye = eye };

            
            var leftHit = _fixationHits[0];
            var rightHit = _fixationHits[1];
            
            if (leftHit == null || rightHit == null)
                return;

            
            if (leftHit.Hit.RayHit.collider == rightHit.Hit.RayHit.collider)
            {
                var go = leftHit.Hit.RayHit.collider.gameObject;
                if (_currentActiveObjectWithBothEyes != go)
                {
                    _currentActiveObjectWithBothEyes = go;
                    ProcessBinocularFixation(leftHit.Hit, rightHit.Hit, leftHit.Data.TargetFixationCounts);
                }
                else
                {
                    DebugLog.OmiLAXR.Print("Binocular target unchanged.");
                }
            }
            else
            {
                DebugLog.OmiLAXR.Print($"Binocular mismatch: Left on {leftHit.Hit.RayHit.collider?.name}, Right on {rightHit.Hit.RayHit.collider?.name}");
                _currentActiveObjectWithBothEyes = null;
            }
        }
        
        protected Eye GetEye(GazeDetector detector)
        {
            if (detector == null)
                return Eye.Unknown;

            if (_eyeGazeCache.TryGetValue(detector, out var cached))
                return cached;

            return _eyeGazeCache[detector] = DetectEyeSide(detector);
        }

        private void HandleGazeLeft(GazeHit gazeHit)
        {
            _fixationHits[0] = null;
            _fixationHits[1] = null;
        }

        protected abstract Eye DetectEyeSide(GazeDetector gazeDetector);

        protected (GazeDetector, GazeDetector) DetectEyeSides(GazeDetector[] gazeDetectors)
        {
            GazeDetector left = null, right = null;
            foreach (var gd in gazeDetectors)
            {
                if (gd == null)
                    continue;
                var eyeSide = DetectEyeSide(gd);
                if (eyeSide == Eye.Left)
                    left = gd;
                else if (eyeSide == Eye.Right)
                    right = gd;
            }
            return (left, right);
        }
        protected override void AfterFilteredObjects(GazeDetector[] gazeDetectors)
        {
            if (gazeDetectors.Length < 2)
                return;
            
            var (left, right) = DetectEyeSides(gazeDetectors);

            if (!left || !right)
                return;
            
            leftEyeDetector = left;
            rightEyeDetector = right;

            left.OnLeave += HandleGazeLeft;
            right.OnLeave += HandleGazeLeft;

            base.AfterFilteredObjects(gazeDetectors);
            
            _fixationDetectorLeft = leftEyeDetector.GetComponent<FixationDetector>();
            _fixationDetectorRight = rightEyeDetector.GetComponent<FixationDetector>();

            if (createDataForSide.HasFlag(Eye.Both))
            {
                _fixationDetectorLeft.OnFixationEnded += DetectBothEyesDetector;
            }
            
            foreach (var gd in gazeDetectors)
            {
                var go = gd.gameObject;
                // Saccade Detector Events
                var saccadeDetector = go.EnsureComponent<SaccadeDetector>();
                if (saccadeDetector)
                {
                    saccadeDetector.saccadeLogic = saccadeLogic;
                    saccadeDetector.hmdTransform = HmdTransform;
                    saccadeDetector.OnSaccadeEnded += HandleOnSaccaded;
                }

                var pursuitDetector = go.EnsureComponent<PursuitDetector>();
                if (pursuitDetector)
                {
                    pursuitDetector.pursuitLogic = pursuitLogic;
                    pursuitDetector.hmdTransform = HmdTransform;
                    pursuitDetector.OnPursuitEnded += HandleOnPursuit;
                }
            }
            
            // Cleanup events
            if (!createDataForSide.HasFlag(Eye.Left))
            {
                UnbindEvents(leftEyeDetector);
            }
            if (!createDataForSide.HasFlag(Eye.Right))
            {
                UnbindEvents(rightEyeDetector);
            }
        }

        protected override void UnbindEvents(GazeDetector gd)
        {
            base.UnbindEvents(gd);

            gd.OnLeave -= HandleGazeLeft;
            
            var fixationDetector = gd.GetComponent<FixationDetector>();
            fixationDetector.OnFixationEnded -= DetectBothEyesDetector;
            
            var saccadeDetector = gd.GetComponent<SaccadeDetector>();
            saccadeDetector.OnSaccadeEnded -= HandleOnSaccaded;

            var pursuitDetector = gd.GetComponent<PursuitDetector>();
            pursuitDetector.OnPursuitEnded -= HandleOnPursuit;
        }

        protected void AutoAssignOwners<T>(GazeDetector[] gds) where T : Component
        {
            foreach (var gd in gds)
            {
                gd.AssignOwner<T>();
            }
        }

        private void HandleOnSaccaded(GazeHit gazeHit, SaccadeData data)
            => OnSaccaded?.Invoke(this, GenerateGazeData(gazeHit), data);

        private void HandleOnPursuit(GazeHit gazeHit, PursuitData data)
            => OnPursuit?.Invoke(this, GenerateGazeData(gazeHit), data);
    }
}