/*
 * SPDX-License-Identifier: AGPL-3.0-or-later
 * Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
 * This file is part of OmiLAXR.
 */

using System;
using UnityEngine;
using UnityEngine.Events;

namespace OmiLAXR.Components
{
    /// <summary>
    /// Monitors a GameObject's transform for significant changes in position, rotation, and scale.
    /// Triggers events when changes exceed defined thresholds.
    /// </summary>
    [AddComponentMenu("OmiLAXR / Game Objects / Transform Watcher")]
    [DisallowMultipleComponent]
    public sealed class TransformWatcher : MonoBehaviour
    {
        [Tooltip("Optional group name. A TransformTrackingBehaviour with a scope only tracks watchers with the same scope.")]
        public string scope;

        /// <summary>
        /// Data structure to hold information about a transform property change,
        /// including both the previous and current values.
        /// </summary>
        public readonly struct TransformChange
        {
            public readonly bool HasChanged;

            /// <summary>
            /// The previous value before the change occurred.
            /// </summary>
            public readonly Vector3 OldValue;

            /// <summary>
            /// The new value after the change occurred.
            /// </summary>
            public readonly Vector3 NewValue;

            public TransformChange(Vector3 oldValue, Vector3 newValue)
            {
                OldValue = oldValue;
                NewValue = newValue;
                HasChanged = true;
            }
        }

        public struct TransformChangeState
        {
            public TransformChange Position;
            public TransformChange Rotation;
            public TransformChange Scale;
            public TransformChange Forward;
        }

        [Serializable]
        public struct TransformIgnore
        {
            public bool position;
            public bool rotation;
            public bool scale;
            public bool forward;
        }

        [Tooltip("Transform properties this watcher does not report changes for.")]
        public TransformIgnore ignore;

        /// <summary>
        /// Minimum position change (in units) required to trigger the position change event.
        /// </summary>
        [Tooltip("Minimum position change (in units) required to trigger events")]
        [Min(0f)]
        public float positionThreshold = .1f;

        /// <summary>
        /// Minimum rotation change (in degrees) required to trigger the rotation change event.
        /// </summary>
        [Tooltip("Minimum rotation change (in degrees) required to trigger events")]
        [Min(0f)]
        public float rotationThreshold = 1.0f;

        /// <summary>
        /// Minimum scale change required to trigger the scale change event.
        /// </summary>
        [Tooltip("Minimum scale change required to trigger events")]
        [Min(0f)]
        public float scaleThreshold = 0.1f;

        [Tooltip("Minimum forward change required to trigger events.")]
        [Min(0f)]
        public float forwardThreshold = 1.0f;

        /// <summary>
        /// The most recent position value that exceeded the threshold.
        /// </summary>
        private Vector3 _lastPosition;

        /// <summary>
        /// The most recent scale value that exceeded the threshold.
        /// </summary>
        private Vector3 _lastScale;

        /// <summary>
        /// The most recent rotation value that exceeded the threshold.
        /// </summary>
        private Vector3 _lastRotation;

        private Vector3 _lastForward;

        /// <summary>
        /// Gets the last position that exceeded the threshold.
        /// </summary>
        public Vector3 LastPosition => _lastPosition;

        /// <summary>
        /// Gets the last scale that exceeded the threshold.
        /// </summary>
        public Vector3 LastScale => _lastScale;

        /// <summary>
        /// Gets the last rotation that exceeded the threshold.
        /// </summary>
        public Vector3 LastRotation => _lastRotation;

        public Vector3 LastForward => _lastForward;

        /// <summary>
        /// Gets the current position of the transform.
        /// </summary>
        public Vector3 CurrentPosition => transform.position;

        /// <summary>
        /// Gets the current local scale of the transform.
        /// </summary>
        public Vector3 CurrentScale => transform.localScale;

        /// <summary>
        /// Gets the current rotation of the transform in Euler angles.
        /// </summary>
        public Vector3 CurrentRotation => transform.eulerAngles;

        public Vector3 CurrentForward => transform.forward;

        /// <summary>
        /// Gets or sets the position from the previous frame.
        /// </summary>
        public Vector3 PreviousPosition { get; private set; } = Vector3.zero;

        /// <summary>
        /// Gets or sets the scale from the previous frame.
        /// </summary>
        public Vector3 PreviousScale { get; private set; } = Vector3.zero;

        /// <summary>
        /// Gets or sets the rotation from the previous frame in Euler angles.
        /// </summary>
        public Vector3 PreviousRotation { get; private set; } = Vector3.zero;

        public Vector3 PreviousForward { get; private set; } = Vector3.zero;

        /// <summary>
        /// Event triggered when position changes exceed the defined threshold.
        /// Provides the old and new position values.
        /// </summary>
        [Tooltip("Event triggered when position changes exceed the threshold"),
         Obsolete("Use GetTransformChangeState() or <TransformWatcherEvents> instead.", true)]
        public UnityEvent<TransformChange> onChangedPosition = new UnityEvent<TransformChange>();

        /// <summary>
        /// Event triggered when scale changes exceed the defined threshold.
        /// Provides the old and new scale values.
        /// </summary>
        [Tooltip("Event triggered when scale changes exceed the threshold"),
         Obsolete("Use GetTransformChangeState() or <TransformWatcherEvents> instead.", true)]
        public UnityEvent<TransformChange> onChangedScale = new UnityEvent<TransformChange>();

        /// <summary>
        /// Event triggered when rotation changes exceed the defined threshold.
        /// Provides the old and new rotation values in Euler angles.
        /// </summary>
        [Tooltip("Event triggered when rotation changes exceed the threshold"),
         Obsolete("Use GetTransformChangeState() or <TransformWatcherEvents> instead.", true)]
        public UnityEvent<TransformChange> onChangedRotation = new UnityEvent<TransformChange>();

        /// <summary>
        /// Detects if a Vector3 value has changed beyond the specified threshold.
        /// </summary>
        /// <param name="curValue">Reference to the current value being tracked.</param>
        /// <param name="newValue">The new value to compare against.</param>
        /// <param name="threshold">The minimum distance required to register a change.</param>
        /// <returns>True if the change exceeds the threshold, false otherwise.</returns>
        private static bool DetectChange(ref Vector3 lastValue, Vector3 newValue, float threshold, out Vector3 oldValue)
        {
            oldValue = lastValue;

            if (threshold <= 0f)
            {
                if (newValue == lastValue) return false;
                lastValue = newValue;
                return true;
            }

            var delta = newValue - lastValue;
            if (delta.sqrMagnitude <= threshold * threshold)
                return false;

            lastValue = newValue;
            return true;
        }

        // Cache one computed state per frame so multiple callers do not recompute.
        private int _cachedFrame = -1;
        private TransformChangeState _cachedState;

        // Cache raw transform reads once per frame as well.
        private Vector3 _cachedPos;
        private Vector3 _cachedScale;
        private Vector3 _cachedRot;
        private Vector3 _cachedFwd;

        private void OnEnable()
        {
            var t = transform;

            _lastPosition = t.position;
            _lastScale = t.localScale;
            _lastRotation = t.eulerAngles;
            _lastForward = t.forward;

            PreviousPosition = _lastPosition;
            PreviousScale = _lastScale;
            PreviousRotation = _lastRotation;
            PreviousForward = _lastForward;

            _cachedFrame = -1;
            _cachedState = default;
        }

        /// <summary>
        /// Monitors transform changes every frame and triggers events when changes exceed thresholds.
        /// </summary>
        public TransformChangeState GetTransformChangeState(TransformIgnore? ignoredChanges = null)
        {
            // Sample only once per frame. If multiple tracking behaviours call this, the second call is O(1).
            if (_cachedFrame != Time.frameCount)
            {
                _cachedFrame = Time.frameCount;

                var t = transform;

                _cachedPos = t.position;
                _cachedScale = t.localScale;
                _cachedRot = t.eulerAngles;
                _cachedFwd = t.forward;

                TransformChangeState state = default;

                // Compute using the component-level ignore so the cached state represents the watcher configuration.
                if (!ignore.position && DetectChange(ref _lastPosition, _cachedPos, positionThreshold, out var oldPos))
                    state.Position = new TransformChange(oldPos, _cachedPos);

                if (!ignore.rotation && DetectChange(ref _lastRotation, _cachedRot, rotationThreshold, out var oldRot))
                    state.Rotation = new TransformChange(oldRot, _cachedRot);

                if (!ignore.scale && DetectChange(ref _lastScale, _cachedScale, scaleThreshold, out var oldScale))
                    state.Scale = new TransformChange(oldScale, _cachedScale);

                if (!ignore.forward && DetectChange(ref _lastForward, _cachedFwd, forwardThreshold, out var oldFwd))
                    state.Forward = new TransformChange(oldFwd, _cachedFwd);

                PreviousPosition = _cachedPos;
                PreviousScale = _cachedScale;
                PreviousRotation = _cachedRot;
                PreviousForward = _cachedFwd;

                _cachedState = state;
            }

            // Caller-specific ignore masking without recomputing.
            var ign = ignoredChanges ?? ignore;

            if (!ign.position && !ign.rotation && !ign.scale && !ign.forward)
                return _cachedState;

            TransformChangeState masked = _cachedState;

            if (ign.position) masked.Position = default;
            if (ign.rotation) masked.Rotation = default;
            if (ign.scale) masked.Scale = default;
            if (ign.forward) masked.Forward = default;

            return masked;
        }
    }
}