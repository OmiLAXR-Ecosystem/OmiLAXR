using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using OmiLAXR.Utils;

namespace OmiLAXR.Components.Gaze.Pursuit
{
    [CreateAssetMenu(menuName = "OmiLAXR / Eye Gaze / Pursuit / Basic", fileName = "PursuitLogicBasic")]
    public class PursuitLogicBasic : PursuitLogic
    {
        [Header("Thresholds (deg/s)")]
        public float minTargetVelocityDegPerSec = 2f;
        public float saccadeVelocityBreakDegPerSec = 180f;
        public float minGain = 0.6f;
        public float maxGain = 1.5f;

        [Header("Tracking Error (deg)")]
        public float maxStartErrorDeg = 15f;
        public float breakErrorDeg = 18f;

        [Header("Timing")]
        [Tooltip("Minimale Dauer, bevor eine Verfolgung als 'valide' beendet geloggt wird (ms).")]
        public int minDurationMs = 20;
        public float maxDurationSec = 10f;

        [Header("Smoothing")]
        public int gainAverageSampleCount = 5;
        public int maxBadGainFrames = 2;

        private bool _inPursuit;
        private DateTime _startTimeUtc;
        private GazeHit _startHit;

        private float _sumEyeAngleDeg;
        private float _sumTargetAngleDeg;
        private float _sumErrorDeg;
        private int _samples;
        private float _elapsedSec;

        private Vector3 _lastTargetDir;

        private readonly Queue<float> _recentGains = new Queue<float>();
        private int _badGainFrames = 0;

        public override void ResetLogic()
        {
            _inPursuit = false;
            _startTimeUtc = default;
            _startHit = null;

            _sumEyeAngleDeg = 0f;
            _sumTargetAngleDeg = 0f;
            _sumErrorDeg = 0f;
            _samples = 0;
            _elapsedSec = 0f;

            _lastTargetDir = Vector3.zero;

            _recentGains.Clear();
            _badGainFrames = 0;
        }

        public override bool TryUpdatePursuit(
            GazeHit currentHit,
            Vector3 prevEyeDir, Vector3 currEyeDir,
            Vector3 prevTargetDir, Vector3 currTargetDir,
            float deltaTime,
            out bool isStart,
            out PursuitData data)
        {
            isStart = false;
            data = null;

            deltaTime = Mathf.Max(deltaTime, 1e-6f);

            var eyeStepDeg = Vector3.Angle(prevEyeDir, currEyeDir);
            var targetStepDeg = Vector3.Angle(prevTargetDir, currTargetDir);

            var eyeVel = eyeStepDeg / deltaTime;
            var targetVel = targetStepDeg / deltaTime;
            var errorDeg = Vector3.Angle(currEyeDir, currTargetDir);

            // Gain smoothing
            var gain = targetVel > 0.01f ? eyeVel / targetVel : 0f;
            _recentGains.Enqueue(gain);
            if (_recentGains.Count > gainAverageSampleCount)
                _recentGains.Dequeue();

            var avgGain = _recentGains.Count > 0 ? _recentGains.Average() : 0f;
            var gainAcceptable = avgGain >= minGain && avgGain <= maxGain;

            if (_inPursuit)
            {
                // Debug.Log($"[Pursuit TRACE] eyeVel={eyeVel:F1}, targetVel={targetVel:F1}, gain={gain:F2}, avgGain={avgGain:F2}, error={errorDeg:F1}");

                _sumEyeAngleDeg += eyeStepDeg;
                _sumTargetAngleDeg += targetStepDeg;
                _sumErrorDeg += errorDeg;
                _samples++;
                _elapsedSec += deltaTime;

                var tooFast = eyeVel > saccadeVelocityBreakDegPerSec;
                var tooInaccurate = errorDeg > breakErrorDeg;
                var lostTarget = currentHit == null || currentHit.RayHit.collider == null;
                var targetTooSlow = targetVel < minTargetVelocityDegPerSec;

                if (!gainAcceptable)
                    _badGainFrames++;
                else
                    _badGainFrames = 0;

                var gainOutOfBounds = _badGainFrames >= maxBadGainFrames;
                var timeoutReached = _elapsedSec >= maxDurationSec;

                var directionReversed = false;
                if (_lastTargetDir != Vector3.zero)
                {
                    var angle = Vector3.Angle(_lastTargetDir, currTargetDir);
                    directionReversed = angle > 150f;
                }
                _lastTargetDir = currTargetDir;

                var shouldEnd =
                    tooFast || tooInaccurate || lostTarget ||
                    gainOutOfBounds || targetTooSlow ||
                    directionReversed || timeoutReached;

                if (shouldEnd)
                {
                    // Debug.Log($"[Pursuit BREAK] fast={tooFast}, inaccurate={tooInaccurate}, lost={lostTarget}, slow={targetTooSlow}, gainFail={gainOutOfBounds}, reversed={directionReversed}, timeout={timeoutReached}");

                    var endUtc = Clock.UtcNow;
                    var durationMs = (int)(_elapsedSec * 1000f);

                    if (durationMs >= minDurationMs && _samples >= 2)
                    {
                        var avgEyeVel = _sumEyeAngleDeg / _elapsedSec;
                        var avgTargetVel = _sumTargetAngleDeg / _elapsedSec;
                        var avgError = _sumErrorDeg / _samples;

                        data = new PursuitData(
                            hit: _startHit,
                            avgVelocityDegPerSec: avgEyeVel,
                            trackingErrorDeg: avgError,
                            startTime: _startTimeUtc,
                            endTime: endUtc,
                            targetVelocityDegPerSec: avgTargetVel,
                            sampleCount: _samples
                        );
                    }

                    // Debug.Log($"[Pursuit END] duration={durationMs}ms | samples={_samples} | gain={avgGain:F2} | valid={isValid}");

                    ResetLogic();
                    return true;
                }

                return false;
            }

            // Try to start pursuit
            if (currentHit != null &&
                currentHit.RayHit.collider != null &&
                targetVel >= minTargetVelocityDegPerSec &&
                gain >= minGain && gain <= maxGain &&
                errorDeg <= maxStartErrorDeg)
            {
                _inPursuit = true;
                _startTimeUtc = Clock.UtcNow;
                _startHit = currentHit;

                _sumEyeAngleDeg = 0f;
                _sumTargetAngleDeg = targetStepDeg;
                _sumErrorDeg = errorDeg;
                _samples = 1;
                _elapsedSec = deltaTime;
                _lastTargetDir = currTargetDir;

                _recentGains.Clear();
                _recentGains.Enqueue(gain);
                _badGainFrames = 0;

                isStart = true;
                // Debug.Log($"[Pursuit START] error={errorDeg:F1} | targetVel={targetVel:F1} | gain={gain:F2}");

                data = null;
                return true;
            }

            return false;
        }
    }
}
