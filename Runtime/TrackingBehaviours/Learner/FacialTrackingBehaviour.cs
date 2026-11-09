/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using OmiLAXR.Components;
using OmiLAXR.Components.Facial.Emotion;
using UnityEngine;

namespace OmiLAXR.TrackingBehaviours.Learner
{
    /// <summary>
    /// Base class for face tracking of an XR device. On every scheduler tick it reads the face data, reports it,
    /// and evaluates the configured emotions, raising an event when an emotion becomes active or inactive.
    /// Device modules implement <c>FetchFaceData</c>.
    /// </summary>
    public abstract class FacialTrackingBehaviour : ScheduledTrackingBehaviour
    {
        public struct EmotionChangedEvent
        {
            public EmotionLogic Emotion; // reference to the ScriptableObject
            public bool IsActive; // true = activated, false = deactivated
            public float Intensity; // intensity at time of change
            public FaceData FaceData;
        }

        [Gesture("Face"), Action("Data")]
        public TrackingBehaviourEvent<FaceData> OnDataUpdated = new TrackingBehaviourEvent<FaceData>();

        [Gesture("Face"), Action("Emotion")] public TrackingBehaviourEvent<EmotionChangedEvent> OnEmotionChanged =
            new TrackingBehaviourEvent<EmotionChangedEvent>();

        [SerializeField, Tooltip("Emotions evaluated on each tick (anger, disgust, fear, happiness, sadness, smile, surprise by default).")]
        private EmotionLogic[] emotions;

        public abstract bool IsAvailable { get; }
        public abstract bool IsEnabled { get; }

        protected abstract FaceData FetchFaceData();

        public FaceData FaceData { get; private set; }
        private FaceData _lastFaceData;

        [Tooltip("Only report face data when it differs from the previous tick. Disable to report it on every tick.")]
        public bool detectOnChange = true;
        
        // A delegate that knows how to build the provider from FaceData
        protected void Reset()
        {
            scheduler = GetDefaultScheduler();
            emotions = new EmotionLogic[]
            {
                ScriptableObject.CreateInstance<AngerLogic>(),
                ScriptableObject.CreateInstance<DisgustLogic>(),
                ScriptableObject.CreateInstance<FearLogic>(),
                ScriptableObject.CreateInstance<HappinessLogic>(),
                ScriptableObject.CreateInstance<SadnessLogic>(),
                ScriptableObject.CreateInstance<SmileLogic>(),
                ScriptableObject.CreateInstance<SurpriseLogic>()
            };
        }

        protected override void Run()
        {
            if (!(IsAvailable))
            {
                DebugLog.OmiLAXR.Error("Face detection is not available. Disabling <FacialTrackingBehaviour>.");
                enabled = false;
                StopSchedules();
                return;
            }

            var fd = FaceData = FetchFaceData();

            
            if (fd == null)
                return;
            
            if (!detectOnChange || _lastFaceData != null && !_lastFaceData.Equals(fd))
            {
                OnDataUpdated?.Invoke(this, fd);   
            }
            
            var t = Time.realtimeSinceStartupAsDouble;

            foreach (var e in emotions)
            {
                var was = e.IsActive;
                e.Evaluate(fd, t);
                if (was != e.IsActive)
                {
                    OnEmotionChanged?.Invoke(this, new EmotionChangedEvent
                    {
                        Emotion = e,
                        IsActive = e.IsActive,
                        Intensity = e.CurrentIntensity,
                        FaceData = FaceData
                    });
                }
            }

            _lastFaceData = FaceData;
        }
    }
}