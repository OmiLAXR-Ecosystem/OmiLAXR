using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OmiLAXR.Filters;
using OmiLAXR.Listeners;
using OmiLAXR.TrackingBehaviours;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OmiLAXR.Tests
{
    public class Pipeline_Tests
    {
        public class MockPipeline : Pipeline
        {
        }

        public class MockListener : Listener
        {
            public int StartCount { get; private set; }
            public Object[] ObjectsToFind = new Object[0];

            public override void StartListening()
            {
                StartCount++;
                Found(ObjectsToFind);
            }
        }

        public class MockFilter : Filter
        {
            public Object Rejected;

            public override Object[] Pass(Object[] objects)
                => objects.Where(o => o != Rejected).ToArray();
        }

        public class MockTrackingBehaviour : TrackingBehaviour
        {
            [Action("MockAction")]
            public TrackingBehaviourEvent MockAction = new TrackingBehaviourEvent();

            [Gesture("MockGesture")]
            public TrackingBehaviourEvent MockGesture = new TrackingBehaviourEvent();

            protected override void AfterFilteredObjects(Object[] objects)
            {
            }
        }

        private GameObject _pipelineObject;
        private MockPipeline _pipeline;
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _pipelineObject = new GameObject("TestPipeline");
            _pipeline = _pipelineObject.AddComponent<MockPipeline>();
            _pipelineObject.AddComponent<Actor>();
        }

        [TearDown]
        public void TearDown()
        {
            // Pipelines register themselves in a static list, so stop before destroying.
            _pipeline.StopPipeline();
            Object.DestroyImmediate(_pipelineObject);
            foreach (var o in _created)
                Object.DestroyImmediate(o);
            _created.Clear();
        }

        private GameObject CreateObject(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        // Works in edit and play mode: in edit mode OnEnable is not invoked automatically.
        private void Restart()
        {
            _pipeline.StopPipeline();
            _pipeline.StartPipeline();
        }

        [Test]
        public void Pipeline_Tests_Start_Assigns_Actor_From_Same_GameObject()
        {
            // Act
            Restart();

            // Assert
            Assert.IsTrue(_pipeline.actor);
            Assert.AreEqual(_pipelineObject.GetComponent<Actor>(), _pipeline.actor);
        }

        [Test]
        public void Pipeline_Tests_Start_Activates_GameObject_And_Fires_Events()
        {
            // Arrange
            _pipeline.StopPipeline();
            var before = 0;
            var after = 0;
            _pipeline.BeforeStartedPipeline += _ => before++;
            _pipeline.AfterStartedPipeline += _ => after++;

            // Act
            _pipeline.StartPipeline();

            // Assert
            Assert.IsTrue(_pipelineObject.activeSelf);
            Assert.IsTrue(_pipeline.IsRunning);
            Assert.AreEqual(1, before);
            Assert.AreEqual(1, after);
            CollectionAssert.Contains(Pipeline.GetAllRunningPipelines(), _pipeline);
        }

        [Test]
        public void Pipeline_Tests_Stop_Deactivates_GameObject_And_Fires_Events()
        {
            // Arrange
            Restart();
            var before = 0;
            var after = 0;
            _pipeline.BeforeStoppedPipeline += _ => before++;
            _pipeline.AfterStoppedPipeline += _ => after++;

            // Act
            _pipeline.StopPipeline();
            _pipeline.StopPipeline();

            // Assert
            Assert.IsFalse(_pipelineObject.activeSelf);
            Assert.IsFalse(_pipeline.IsRunning);
            Assert.AreEqual(1, before);
            Assert.AreEqual(1, after);
            CollectionAssert.DoesNotContain(Pipeline.GetAllRunningPipelines(), _pipeline);
        }

        [Test]
        public void Pipeline_Tests_Start_Collects_Child_Components()
        {
            // Arrange
            var child = CreateObject("Child");
            child.transform.SetParent(_pipelineObject.transform);
            var listener = child.AddComponent<MockListener>();
            var filter = child.AddComponent<MockFilter>();
            var tb = child.AddComponent<MockTrackingBehaviour>();

            // Act
            Restart();

            // Assert
            Assert.AreEqual(listener, _pipeline.GetListener<MockListener>());
            Assert.AreEqual(filter, _pipeline.GetFilters<MockFilter>());
            Assert.AreEqual(tb, _pipeline.GetTrackingBehaviour<MockTrackingBehaviour>());
            Assert.AreEqual(1, _pipeline.Listeners.Count);
            Assert.AreEqual(1, _pipeline.Filters.Count);
            Assert.AreEqual(1, _pipeline.TrackingBehaviours.Count);
        }

        [Test]
        public void Pipeline_Tests_Restart_Does_Not_Duplicate_Components()
        {
            // Arrange
            var listener = _pipelineObject.AddComponent<MockListener>();
            _pipelineObject.AddComponent<MockFilter>();
            _pipelineObject.AddComponent<MockTrackingBehaviour>();

            // Act
            Restart();
            Restart();

            // Assert
            Assert.AreEqual(1, _pipeline.Listeners.Count);
            Assert.AreEqual(1, _pipeline.Filters.Count);
            Assert.AreEqual(1, _pipeline.TrackingBehaviours.Count);
            Assert.AreEqual(2, listener.StartCount);
        }

        [Test]
        public void Pipeline_Tests_Add_Registers_Listener_And_Filter_Once()
        {
            // Arrange
            var listener = _pipelineObject.AddComponent<MockListener>();
            var filter = _pipelineObject.AddComponent<MockFilter>();

            // Act
            _pipeline.Add(listener);
            _pipeline.Add(listener);
            _pipeline.Add(filter);
            _pipeline.Add(filter);

            // Assert
            Assert.AreEqual(1, _pipeline.Listeners.Count);
            Assert.AreEqual(1, _pipeline.Filters.Count);
            Assert.Contains(listener, _pipeline.Listeners);
            Assert.Contains(filter, _pipeline.Filters);
        }

        [Test]
        public void Pipeline_Tests_Add_Registers_TrackingBehaviour()
        {
            // Arrange
            var tb = _pipelineObject.AddComponent<MockTrackingBehaviour>();

            // Act
            _pipeline.Add(tb);

            // Assert
            Assert.AreEqual(1, _pipeline.TrackingBehaviours.Count);
            Assert.Contains(tb, _pipeline.TrackingBehaviours);
        }

        [Test]
        public void Pipeline_Tests_Start_Runs_Listeners_And_Applies_Filters()
        {
            // Arrange
            var kept = CreateObject("Kept");
            var rejected = CreateObject("Rejected");
            var listener = _pipelineObject.AddComponent<MockListener>();
            listener.ObjectsToFind = new Object[] { kept, rejected };
            _pipelineObject.AddComponent<MockFilter>().Rejected = rejected;

            Object[] found = null;
            Object[] filtered = null;
            _pipeline.AfterFoundObjects += objects => found = objects;
            _pipeline.AfterFilteredObjects += objects => filtered = objects;

            // Act
            Restart();

            // Assert
            Assert.AreEqual(1, listener.StartCount);
            CollectionAssert.AreEqual(new Object[] { kept, rejected }, found);
            CollectionAssert.AreEqual(new Object[] { kept }, filtered);
            CollectionAssert.AreEqual(new Object[] { kept }, _pipeline.TrackingObjects);
        }

        [Test]
        public void Pipeline_Tests_Disabled_Components_Are_Skipped()
        {
            // Arrange
            var rejected = CreateObject("Rejected");
            var disabledListener = _pipelineObject.AddComponent<MockListener>();
            disabledListener.enabled = false;
            var listener = _pipelineObject.AddComponent<MockListener>();
            listener.ObjectsToFind = new Object[] { rejected };
            var filter = _pipelineObject.AddComponent<MockFilter>();
            filter.Rejected = rejected;
            filter.enabled = false;

            // Act
            Restart();

            // Assert
            Assert.AreEqual(0, disabledListener.StartCount);
            CollectionAssert.AreEqual(new Object[] { rejected }, _pipeline.TrackingObjects);
        }

        [Test]
        public void Pipeline_Tests_Collects_Gestures_And_Actions()
        {
            // Arrange
            var tb = _pipelineObject.AddComponent<MockTrackingBehaviour>();

            // Act
            Restart();

            // Assert
            Assert.AreSame(tb.MockAction, _pipeline.Actions["MockAction"].Single());
            Assert.AreSame(tb.MockGesture, _pipeline.Gestures["MockGesture"].Single());
        }

        [Test]
        public void Pipeline_Tests_SetDisabledActions_And_Gestures()
        {
            // Arrange
            var tb = _pipelineObject.AddComponent<MockTrackingBehaviour>();
            Restart();

            // Act
            _pipeline.SetDisabledActions(true, new[] { "MockAction" });
            _pipeline.SetDisabledGestures(true, new[] { "Other" });

            // Assert
            Assert.IsTrue(tb.MockAction.IsDisabled);
            Assert.IsFalse(tb.MockGesture.IsDisabled);

            // Act
            _pipeline.SetDisabledActions(false);
            _pipeline.SetDisabledGestures(true);

            // Assert
            Assert.IsFalse(tb.MockAction.IsDisabled);
            Assert.IsTrue(tb.MockGesture.IsDisabled);
        }

        [Test]
        public void Pipeline_Tests_Component_Finds_Pipeline_In_Parent()
        {
            // Arrange
            var child = CreateObject("Child");
            child.transform.SetParent(_pipelineObject.transform);
            var listener = child.AddComponent<MockListener>();
            Restart();

            // Assert
            Assert.AreEqual(_pipeline, listener.Pipeline);
            Assert.AreEqual(_pipeline.actor, listener.GetActor());
        }
    }
}
