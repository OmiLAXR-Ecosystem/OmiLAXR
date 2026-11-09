using System;
using UnityEngine;
using OmiLAXR.Utils;

namespace OmiLAXR.Components.Gaze.Saccade
{
    [CreateAssetMenu(menuName = "OmiLAXR / Eye Gaze / Saccade / Velocity Threshold", fileName = "SaccadeLogicVelocityThreshold")]
    public class SaccadeLogicVelocityThreshold : SaccadeLogic
    {
        [Tooltip("Start threshold in deg/s to detect saccade")]
        public float saccadeStartThreshold = 100f;

        [Tooltip("End threshold in deg/s to end saccade (hysteresis)")]
        public float saccadeEndThreshold = 60f;

        private bool _inSaccade;
        private Vector3 _startDirection;
        private Vector3 _startPoint;
        private GazeHit _startHit;
        private float? _startPupilMm;
        private DateTime _startWallClock;
        private float _elapsedSeconds;

        public override void ResetLogic()
        {
            _inSaccade = false;
            _startDirection = default;
            _startPoint = default;
            _startHit = null;
            _startPupilMm = null;
            _elapsedSeconds = 0f;
            _startWallClock = default;
        }

        public override bool TryUpdateSaccade(
            GazeHit previousHit,
            GazeHit currentHit,
            Vector3 previousDirection,
            Vector3 currentDirection,
            float deltaTime,
            float? pupilDiameterMillimeters,
            out bool isStart,
            out SaccadeData data)
        {
            // Defaults
            isStart = false;
            data = null;

            deltaTime = Mathf.Max(deltaTime, 1e-6f);
            var angleStep = Vector3.Angle(previousDirection, currentDirection);  // deg between samples
            var velocity = angleStep / deltaTime;                                // deg/s

            if (_inSaccade)
            {
                // Laufzeit akkumulieren
                _elapsedSeconds += deltaTime;

                // Ende?
                if (velocity < saccadeEndThreshold)
                {
                    _inSaccade = false;

                    // Gesamtsamplitude (Start -> aktueller Blick)
                    var amplitudeDeg = Vector3.Angle(_startDirection, currentDirection);

                    var endPoint = SafeHitPoint(currentHit);
                    var endTime = Clock.Now;
                    var duration = OmiLAXR.Types.Duration.FromMilliseconds(
                        (int)(endTime - _startWallClock).TotalMilliseconds
                    );

                    data = new SaccadeData(
                        hit: currentHit,
                        startGazeCoordinates: _startPoint,
                        endGazeCoordinates: endPoint,
                        saccadeAmplitudeDegrees: amplitudeDeg,
                        pupilDiameterMillimeters: _startPupilMm ?? pupilDiameterMillimeters,
                        startTime: _startWallClock,
                        endTime: endTime
                    );

                    return true; // state change -> end with data
                }
            }
            else
            {
                // Start?
                if (velocity > saccadeStartThreshold)
                {
                    _inSaccade = true;

                    _startDirection = previousDirection;
                    _startPoint = SafeHitPoint(previousHit);
                    _startHit = previousHit;
                    _startPupilMm = pupilDiameterMillimeters;
                    _elapsedSeconds = 0f;
                    _startWallClock = Clock.Now;

                    // Optional: Start-Daten zurückgeben (für Streaming/Live-Protokoll)
                    isStart = true;
                    data = new SaccadeData(
                        hit: currentHit,                     // Ziel ist noch provisorisch (erste Probe nach Start)
                        startGazeCoordinates: _startPoint,
                        endGazeCoordinates: SafeHitPoint(currentHit),
                        saccadeAmplitudeDegrees: 0f,           // Amplitude zu Beginn 0
                        pupilDiameterMillimeters: _startPupilMm,
                        startTime: _startWallClock,
                        endTime: null
                    );
                    return true; // state change -> start with data
                }
            }

            // Kein Zustandswechsel
            return false;
        }

        private static Vector3 SafeHitPoint(GazeHit hit)
        {
            // Fallback, falls kein Collider o.ä. vorliegt
            if (hit != null && hit.RayHit.collider != null)
                return hit.RayHit.point;

            // Ohne gültigen Hit lieber (0,0,0) benutzen statt NaNs
            return Vector3.zero;
        }
    }
}
