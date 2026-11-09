using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OmiLAXR.TrackingBehaviours.Learner.HeadTracking;
using UnityEngine;

namespace OmiLAXR.Tests.TrackingBehaviours
{
    public class HeadUtils_Tests
    {
        private static readonly DateTime T0 = new DateTime(2025, 1, 1);

        private static List<HmdTimedPosition> Positions(params (double seconds, float x, float y)[] samples)
            => samples.Select(s => new HmdTimedPosition
            {
                Timestamp = T0.AddSeconds(s.seconds),
                Position = new Vector3(s.x, s.y, 0)
            }).ToList();

        [Test]
        public void HeadUtils_Counts_Oscillations_Above_Min_Amplitude()
        {
            // Arrange: two nods with amplitude 1, plus jitter that must be ignored
            var signal = new List<float> { 0f, 0.01f, 1f, 0f, 1f, 0f, 0.02f };

            // Act
            var count = HeadUtils.GetNumOfGestures(signal, 0.5);

            // Assert
            Assert.AreEqual(2, count);
        }

        [Test]
        public void HeadUtils_Counts_Gestures_On_Selected_Axis()
        {
            // Arrange: y oscillates, x stays still
            var positions = Positions((0, 0, 0), (0.1, 0, 1), (0.2, 0, 0), (0.3, 0, 1), (0.4, 0, 0));

            // Assert
            Assert.AreEqual(2, HeadUtils.GetNumOfGestures(positions, 0.5, "y"));
            Assert.AreEqual(0, HeadUtils.GetNumOfGestures(positions, 0.5, "x"));
        }

        [Test]
        public void HeadUtils_EnoughAmplitude()
        {
            // Arrange
            var positions = Positions((0, 0, 0), (0.1, 0.3f, 1));

            // Assert
            Assert.IsTrue(HeadUtils.EnoughAmplitude(positions, 0.5, "y"));
            Assert.IsFalse(HeadUtils.EnoughAmplitude(positions, 0.5, "x"));
            Assert.IsFalse(HeadUtils.EnoughAmplitude(new List<float>(), 0.1));
        }

        [Test]
        public void HeadUtils_GestureFinished_When_Tail_Is_Still()
        {
            // Arrange
            var positions = Positions((0, 0, 1), (0.1, 0, 0), (0.2, 0, 0.01f), (0.3, 0, 0));

            // Assert
            Assert.IsTrue(HeadUtils.GestureFinished(positions, 1, 0.1, "y"));
            Assert.IsFalse(HeadUtils.GestureFinished(positions, 0, 0.1, "y"));
        }

        [Test]
        public void HeadUtils_GetPositionContinuousGesture_Finds_Last_Sample_Outside_Window()
        {
            // Arrange
            var positions = Positions((0, 0, 0), (1, 0, 0), (2, 0, 0), (2.5, 0, 0));

            // Assert
            Assert.AreEqual(1, HeadUtils.GetPositionContinuousGesture(positions, TimeSpan.FromSeconds(1)));
            Assert.AreEqual(-1, HeadUtils.GetPositionContinuousGesture(positions, TimeSpan.FromSeconds(10)));
        }

        [Test]
        public void HeadUtils_EnoughSamplesCollected_Clears_On_Application_Start()
        {
            // Arrange
            var positions = Positions((0, 0, 0), (3, 0, 0));
            var startApplication = true;
            var reached = false;

            // Act
            HeadUtils.EnoughSamplesCollected(ref positions, TimeSpan.FromSeconds(2), ref startApplication, ref reached);

            // Assert
            Assert.IsTrue(reached);
            Assert.IsFalse(startApplication);
            Assert.IsEmpty(positions);

            // Act
            positions = Positions((0, 0, 0), (1, 0, 0));
            HeadUtils.EnoughSamplesCollected(ref positions, TimeSpan.FromSeconds(2), ref startApplication, ref reached);

            // Assert
            Assert.IsFalse(reached);
            Assert.AreEqual(2, positions.Count);
        }

        [Test]
        public void HeadUtils_ClearAllItems_Empties_All_Lists()
        {
            // Arrange
            var positions = Positions((0, 0, 0));
            var manipulated = Positions((0, 0, 0));
            var distances = new List<float> { 1f };

            // Act
            HeadUtils.ClearAllItems(ref positions, ref manipulated, ref distances);

            // Assert
            Assert.IsEmpty(positions);
            Assert.IsEmpty(manipulated);
            Assert.IsEmpty(distances);
        }

        [Test]
        public void HeadUtils_NormalHeadMovement_Uses_Requested_Axis()
        {
            // Arrange: large movement on y only
            var positions = Positions((0, 0, 0), (0.1, 0, 2), (0.2, 0, 0));

            // Assert
            Assert.IsTrue(HeadUtils.NormalHeadMovement("y", positions, 1.0));
            Assert.IsFalse(HeadUtils.NormalHeadMovement("x", positions, 1.0));
        }
    }
}
