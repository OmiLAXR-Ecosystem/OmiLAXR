using System;
using System.Collections.Generic;
using NUnit.Framework;
using OmiLAXR.Components;
using OmiLAXR.Components.Facial.Emotion;
using OmiLAXR.Components.Gaze;
using OmiLAXR.Components.Gaze.Fixation;
using OmiLAXR.Components.Gaze.Saccade;
using OmiLAXR.Types;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OmiLAXR.Tests.Components
{
    public class GazeAndFace_Tests
    {
        public class TestFaceData : FaceData
        {
            public TestFaceData(float[] weights, float[] confidences) : base(weights, confidences)
            {
            }

            public TestFaceData Set(FaceActionUnit au, float value, float confidence = 1f)
            {
                AuValues[au] = value;
                AuConfidences[au] = confidence;
                return this;
            }
        }

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
                Object.DestroyImmediate(o);
            _created.Clear();
        }

        private T Track<T>(T obj) where T : Object
        {
            _created.Add(obj);
            return obj;
        }

        private static Vector3 Direction(float yawDegrees) => Quaternion.Euler(0, yawDegrees, 0) * Vector3.forward;

        [Test]
        public void SaccadeLogic_Default_Is_Velocity_Threshold()
        {
            Assert.IsInstanceOf<SaccadeLogicVelocityThreshold>(Track(SaccadeLogic.GetDefault()));
        }

        [Test]
        public void SaccadeLogic_Ignores_Slow_Movements()
        {
            // Arrange
            var logic = Track(ScriptableObject.CreateInstance<SaccadeLogicVelocityThreshold>());

            // Act (1 degree in 100ms = 10 deg/s)
            var changed = logic.TryUpdateSaccade(null, null, Direction(0), Direction(1), 0.1f, null, out var isStart, out var data);

            // Assert
            Assert.IsFalse(changed);
            Assert.IsFalse(isStart);
            Assert.IsNull(data);
        }

        [Test]
        public void SaccadeLogic_Detects_Start_And_End_With_Amplitude()
        {
            // Arrange
            var logic = Track(ScriptableObject.CreateInstance<SaccadeLogicVelocityThreshold>());

            // Act: 5 degrees in 10ms = 500 deg/s -> start
            var started = logic.TryUpdateSaccade(null, null, Direction(0), Direction(5), 0.01f, 3.5f, out var isStart, out var startData);
            // 5 more degrees, still fast -> no state change
            var running = logic.TryUpdateSaccade(null, null, Direction(5), Direction(10), 0.01f, 4f, out _, out _);
            // slow down -> end
            var ended = logic.TryUpdateSaccade(null, null, Direction(10), Direction(10.1f), 0.01f, 4f, out var isEndStart, out var endData);

            // Assert
            Assert.IsTrue(started);
            Assert.IsTrue(isStart);
            Assert.AreEqual(0f, startData.SaccadeAmplitudeDegrees);
            Assert.AreEqual(3.5f, startData.PupilDiameterMillimeters);
            Assert.IsNull(startData.EndTime);
            Assert.IsFalse(running);
            Assert.IsTrue(ended);
            Assert.IsFalse(isEndStart);
            Assert.AreEqual(10.1f, endData.SaccadeAmplitudeDegrees, 0.01f);
            Assert.AreEqual(3.5f, endData.PupilDiameterMillimeters);
            Assert.IsNotNull(endData.EndTime);
            Assert.AreEqual(Vector3.zero, endData.EndGazeCoordinates);
        }

        [Test]
        public void SaccadeLogic_Reset_Forgets_Running_Saccade()
        {
            // Arrange
            var logic = Track(ScriptableObject.CreateInstance<SaccadeLogicVelocityThreshold>());
            logic.TryUpdateSaccade(null, null, Direction(0), Direction(5), 0.01f, null, out _, out _);

            // Act
            logic.ResetLogic();
            var changed = logic.TryUpdateSaccade(null, null, Direction(5), Direction(5), 0.01f, null, out _, out _);

            // Assert
            Assert.IsFalse(changed);
        }

        private RaycastHit RaycastAt(Collider target, Vector3 origin)
        {
            Physics.SyncTransforms();
            var direction = (target.bounds.center - origin).normalized;
            Assert.IsTrue(target.Raycast(new Ray(origin, direction), out var hit, 100f));
            return hit;
        }

        [Test]
        public void FixationLogic_Detects_Stable_Gaze_On_New_Target_Once()
        {
            // Arrange
            var logic = Track(ScriptableObject.CreateInstance<FixationLogicAngularStability>());
            logic.requiredStableFrames = 3;
            var hmd = Track(new GameObject("HMD")).transform;
            var target = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            target.transform.position = new Vector3(0, 0, 5);
            var hit = RaycastAt(target.GetComponent<Collider>(), hmd.position);

            // Act
            var first = logic.TryUpdateFixation(hit, hmd, out var firstIsNew);
            var second = logic.TryUpdateFixation(hit, hmd, out _);
            var third = logic.TryUpdateFixation(hit, hmd, out var thirdIsNew);
            var afterwards = logic.TryUpdateFixation(hit, hmd, out _);

            // Assert
            Assert.IsFalse(first);
            Assert.IsFalse(firstIsNew);
            Assert.IsFalse(second);
            Assert.IsTrue(third);
            Assert.IsTrue(thirdIsNew);
            Assert.IsFalse(afterwards);
        }

        [Test]
        public void FixationLogic_Rejects_Unstable_Gaze()
        {
            // Arrange
            var logic = Track(ScriptableObject.CreateInstance<FixationLogicAngularStability>());
            logic.requiredStableFrames = 2;
            logic.maxAnglePerFrame = 0.5f;
            var hmd = Track(new GameObject("HMD")).transform;
            var near = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            near.transform.position = new Vector3(0, 0, 5);
            var far = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            far.transform.position = new Vector3(5, 0, 5);

            // Act
            logic.TryUpdateFixation(RaycastAt(near.GetComponent<Collider>(), hmd.position), hmd, out _);
            var result = logic.TryUpdateFixation(RaycastAt(far.GetComponent<Collider>(), hmd.position), hmd, out var isNew);

            // Assert
            Assert.IsFalse(result);
            Assert.IsFalse(isNew);
            Assert.IsInstanceOf<FixationLogicAngularStability>(Track(FixationLogic.GetDefault()));
            Assert.IsInstanceOf<FixationLogicAngularStability>(Track(logic.Clone()));
        }

        [Test]
        public void GazeHit_Without_Collider_Is_Invalid()
        {
            // Act
            var hit = new GazeHit(null, default, Vector3.forward, Vector3.zero);

            // Assert
            Assert.IsFalse(hit.IsValid);
            Assert.IsTrue(float.IsNaN(hit.ViewingAngleDeg));
            Assert.IsTrue(float.IsNaN(hit.IncidenceAngleDeg));
        }

        [Test]
        public void GazeHit_Computes_Viewing_And_Incidence_Angles()
        {
            // Arrange
            var target = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            target.transform.position = new Vector3(0, 0, 5);
            var rayHit = RaycastAt(target.GetComponent<Collider>(), Vector3.zero);

            // Act
            var hit = new GazeHit(null, rayHit, Vector3.forward, Vector3.zero);

            // Assert
            Assert.IsTrue(hit.IsValid);
            Assert.AreSame(target, hit.Target);
            Assert.AreEqual(0f, hit.ViewingAngleDeg, 0.01f);
            Assert.AreEqual(0f, hit.IncidenceAngleDeg, 0.01f);
        }

        [Test]
        public void Gaze_Data_Computes_Durations()
        {
            // Arrange
            var start = new DateTime(2025, 1, 1, 0, 0, 0);
            var end = start.AddMilliseconds(250);

            // Act
            var fixation = new FixationData(null, Vector3.one, 2, start, end);
            var saccade = new SaccadeData(null, Vector3.zero, Vector3.one, 12f, null, start, end);
            var open = new FixationData(null, Vector3.one, 1, start, null);

            // Assert
            Assert.AreEqual(Duration.FromMilliseconds(250), fixation.Duration);
            Assert.AreEqual(2, fixation.TargetFixationCounts);
            Assert.AreEqual(Duration.FromMilliseconds(250), saccade.Duration);
            Assert.AreEqual(12f, saccade.SaccadeAmplitudeDegrees);
            Assert.AreEqual(0d, open.Duration.Value);
        }

        [Test]
        public void FaceData_Defaults_And_Indexer()
        {
            // Arrange
            var face = new TestFaceData(new[] { 0.2f, 0.4f }, new[] { 0.9f, 0.8f })
                .Set(FaceActionUnit.AU12_LipCornerPuller, 0.7f, 0.5f);

            // Assert
            Assert.AreEqual(2, face.Count);
            Assert.AreEqual(0.4f, face[1]);
            Assert.AreEqual(0.8f, face.GetConfidence(1));
            Assert.AreEqual(0.7f, face.GetAU(FaceActionUnit.AU12_LipCornerPuller));
            Assert.AreEqual(0.5f, face.GetConfidence(FaceActionUnit.AU12_LipCornerPuller));
            Assert.AreEqual(0f, face.GetAU(FaceActionUnit.AU6_CheekRaiser));
            Assert.AreEqual(1f, face.GetConfidence(FaceActionUnit.AU6_CheekRaiser));
        }

        [Test]
        public void FaceData_Equality_Uses_Weights_And_Confidences()
        {
            // Arrange
            var a = new TestFaceData(new[] { 0.2f }, new[] { 1f });
            var b = new TestFaceData(new[] { 0.2f }, new[] { 1f });
            var c = new TestFaceData(new[] { 0.3f }, new[] { 1f });

            // Assert
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.IsTrue(a.Equals(a));
            Assert.IsTrue(a.Equals(b));
            Assert.IsFalse(a.Equals(c));
            Assert.IsFalse(a.Equals(null));
        }

        [Test]
        public void HappinessLogic_Activates_After_Onset_And_Deactivates_After_Offset()
        {
            // Arrange
            var logic = Track(ScriptableObject.CreateInstance<HappinessLogic>());
            logic.emaAlpha = 1f; // no smoothing to make timing deterministic
            var smiling = new TestFaceData(new float[0], new float[0])
                .Set(FaceActionUnit.AU12_LipCornerPuller, 1f)
                .Set(FaceActionUnit.AU6_CheekRaiser, 1f);
            var neutral = new TestFaceData(new float[0], new float[0]);

            // Act & Assert
            logic.Evaluate(smiling, 0.0);
            Assert.IsFalse(logic.IsActive);
            Assert.AreEqual(1f, logic.CurrentIntensity);
            logic.Evaluate(smiling, 0.1);
            Assert.IsFalse(logic.IsActive, "onset shorter than minOnsetMs");
            logic.Evaluate(smiling, 0.2);
            Assert.IsTrue(logic.IsActive);
            logic.Evaluate(neutral, 0.3);
            Assert.IsTrue(logic.IsActive, "offset shorter than minOffsetMs");
            logic.Evaluate(neutral, 0.6);
            Assert.IsFalse(logic.IsActive);
            Assert.AreEqual(Emotion.Happiness.ToString(), logic.EmotionType);
        }

        [Test]
        public void EmotionLogic_Smooths_With_Exponential_Moving_Average()
        {
            // Arrange
            var logic = Track(ScriptableObject.CreateInstance<HappinessLogic>());
            logic.emaAlpha = 0.5f;
            logic.cheekWeight = 0f;
            var smiling = new TestFaceData(new float[0], new float[0]).Set(FaceActionUnit.AU12_LipCornerPuller, 1f);
            var neutral = new TestFaceData(new float[0], new float[0]);

            // Act
            logic.Evaluate(neutral, 0);
            logic.Evaluate(smiling, 0.1);
            logic.Evaluate(smiling, 0.2);

            // Assert
            Assert.AreEqual(0.75f, logic.CurrentIntensity, 1e-5f);
        }
    }
}
