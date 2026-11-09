using System;
using OmiLAXR.TrackingBehaviours.Learner.Gaze;
using OmiLAXR.Types;
using UnityEngine;

namespace OmiLAXR.Components
{
    /// <summary>
    /// Base class for the eye tracking calibration of an XR device. On start it calibrates if eye tracking is available
    /// and not yet calibrated. If eye tracking is unavailable or calibration fails, it can switch from
    /// <see cref="EyeGazeTrackingBehaviour"/> to <see cref="CameraGazeTrackingBehaviour"/> (<c>fallbackToCameraGaze</c>).
    /// Device modules implement <c>OnStartCalibration</c> and <c>OnStoppedCalibration</c>.
    /// </summary>
    public abstract class EyeCalibrator : PipelineComponent, ICalibratable
    {
        
        [field: SerializeField, ReadOnly]
        public bool IsCalibrated { get; protected set; } = false;
        [field: SerializeField, ReadOnly]
        public bool NeedsCalibration { get; protected set; } = false;
        [field: SerializeField, ReadOnly]
        public bool IsEyeTrackingAvailable { get; protected set; } = false;
        
        protected abstract void OnStartCalibration(Action<bool> successCallback, Action<string> failedCallback);
        protected abstract void OnStoppedCalibration(Action callback);
        
        [Tooltip("Camera gaze is tracked by default. Enable this to use camera gaze only when eye tracking is unavailable.")]
        public bool fallbackToCameraGaze = false;
        
        public void StartCalibration()
        {
            OnCalibrationStarted?.Invoke();
            OnStartCalibration(success =>
            {
                NeedsCalibration = !success;
                IsCalibrated = success;
                OnCalibrationEnded?.Invoke(success);
            }, msg =>
            {
                DebugLog.OmiLAXR.Error(msg);
                OnCalibrationEnded?.Invoke(false);
                if (fallbackToCameraGaze)
                    UseCameraGazeFallback();
            });
        }

        public void StopCalibration()
        {
            OnStoppedCalibration(() =>
            {
                OnCalibrationEnded?.Invoke(false);
            });
        }

        protected virtual void Start()
        {
            if (IsEyeTrackingAvailable && NeedsCalibration)
            {
                StartCalibration();
            }
            else
            {
                DebugLog.OmiLAXR.Warning("Eye Tracking is not available.");
                if (fallbackToCameraGaze)
                    UseCameraGazeFallback();
            }
        }

        protected virtual void UseCameraGazeFallback()
        {
            var eyeTb = GetComponentInChildren<EyeGazeTrackingBehaviour>(true);
            if (!eyeTb)
            {
                DebugLog.OmiLAXR.Error("Cannot find a <EyeGazeTrackingBehaviour>.");
                return;
            }
            var cameraGazeTb = GetComponentInChildren<CameraGazeTrackingBehaviour>(true);
            if (!cameraGazeTb)
            {
                DebugLog.OmiLAXR.Error("Cannot find a <CameraGazeTrackingBehaviour>.");
                return;
            }
            eyeTb.enabled = false;
            cameraGazeTb.enabled = true;
        }
        
        public event Action OnCalibrationStarted;
        public event Action<bool> OnCalibrationEnded;
    }
}