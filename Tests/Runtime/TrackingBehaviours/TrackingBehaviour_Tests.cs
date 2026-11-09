using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OmiLAXR.Schedules;
using OmiLAXR.TrackingBehaviours;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace OmiLAXR.Tests.TrackingBehaviours
{
    public class TrackingBehaviour_Tests
    {
        public class LifecycleTrackingBehaviour : TrackingBehaviour<BoxCollider>
        {
            public readonly List<string> Calls = new List<string>();
            public BoxCollider[] Filtered = new BoxCollider[0];

            public TrackingBehaviourEvent<int> OnValue = new TrackingBehaviourEvent<int>();

            protected override void BeforeStartedPipeline(Pipeline pipeline) => Calls.Add("beforeStart");
            protected override void OnStartedPipeline(Pipeline pipeline) => Calls.Add("started");
            protected override void BeforeStoppedPipeline(Pipeline pipeline) => Calls.Add("beforeStop");
            protected override void OnStoppedPipeline(Pipeline pipeline) => Calls.Add("stopped");
            protected override void AfterFoundObjects(BoxCollider[] objects) => Calls.Add("found:" + objects.Length);

            protected override void AfterFilteredObjects(BoxCollider[] objects)
            {
                Calls.Add("filtered:" + objects.Length);
                Filtered = objects;
            }

            public IntervalTicker Interval(Action onTick) => SetInterval(onTick, 10f);
            public TimeoutTicker Timeout(Action onTick) => SetTimeout(onTick, 10f);
            public int SchedulerCount => Schedulers.Count;
            public void Clear() => ClearSchedules();
            public TS[] DoSelect<TS>(params Object[] objects) where TS : Object => Select<TS>(objects);
            public TS[] DoExtract<TS>(params Object[] objects) where TS : Component => Extract<TS>(objects);
            public TS[] DoExtractOrSelect<TS>(params Object[] objects) where TS : Object => ExtractOrSelect<TS>(objects);
        }

        private GameObject _pipelineObject;
        private Pipeline _pipeline;
        private LifecycleTrackingBehaviour _tb;
        private GameObject _withCollider;
        private GameObject _plain;

        [SetUp]
        public void SetUp()
        {
            _withCollider = new GameObject("With Collider");
            _withCollider.AddComponent<BoxCollider>();
            _plain = new GameObject("Plain");

            _pipelineObject = new GameObject("Pipeline");
            _pipelineObject.SetActive(false);
            _pipelineObject.AddComponent<Actor>();
            // Added before the pipeline so it subscribes before the pipeline starts.
            _tb = _pipelineObject.AddComponent<LifecycleTrackingBehaviour>();
            _pipelineObject.AddComponent<Pipeline_Tests.MockListener>().ObjectsToFind = new Object[] { _withCollider, _plain };
            _pipeline = _pipelineObject.AddComponent<Pipeline>();
        }

        [TearDown]
        public void TearDown()
        {
            _pipeline.StopPipeline();
            Object.DestroyImmediate(_pipelineObject);
            Object.DestroyImmediate(_withCollider);
            Object.DestroyImmediate(_plain);
        }

        [Test]
        public void TrackingBehaviour_Receives_Pipeline_Lifecycle_In_Order()
        {
            // Act
            _pipeline.StartPipeline();
            _pipeline.StopPipeline();

            // Assert
            CollectionAssert.AreEqual(
                new[] { "found:0", "filtered:1", "beforeStart", "started", "beforeStop", "stopped" }, _tb.Calls);
            CollectionAssert.AreEqual(new[] { _withCollider.GetComponent<BoxCollider>() }, _tb.Filtered);
            Assert.AreSame(_pipelineObject.GetComponent<Actor>(), _tb.GetActor());
            Assert.IsNull(_tb.GetInstructor());
        }

        [Test]
        public void TrackingBehaviour_Restart_Does_Not_Duplicate_Callbacks()
        {
            // Act
            _pipeline.StartPipeline();
            _pipeline.StopPipeline();
            _tb.Calls.Clear();
            _pipeline.StartPipeline();

            // Assert
            Assert.AreEqual(1, _tb.Calls.Count(c => c == "started"));
        }

        [Test]
        public void TrackingBehaviour_Disabled_Ignores_Pipeline_Events()
        {
            // Arrange
            _pipeline.StartPipeline();
            _tb.Calls.Clear();

            // Act
            _tb.enabled = false;
            _pipeline.StopPipeline();

            // Assert
            CollectionAssert.IsEmpty(_tb.Calls);
        }

        [Test]
        public void TrackingBehaviour_Schedules_Are_Registered_And_Cleared_On_Stop()
        {
            // Arrange
            _pipeline.StartPipeline();
            var ticks = 0;

            // Act
            var interval = _tb.Interval(() => ticks++);
            var timeout = _tb.Timeout(() => ticks++);

            // Assert
            Assert.AreEqual(2, _tb.SchedulerCount);
            Assert.IsTrue(interval.IsRunning);
            Assert.IsTrue(timeout.IsRunning);
            Assert.AreEqual(1, ticks, "interval ticks immediately, timeout does not");

            // Act
            _pipeline.StopPipeline();

            // Assert
            Assert.AreEqual(0, _tb.SchedulerCount);
            Assert.IsFalse(interval.IsRunning);
            Assert.IsFalse(timeout.IsRunning);
            Object.DestroyImmediate(interval);
            Object.DestroyImmediate(timeout);
        }

        [Test]
        public void TrackingBehaviour_Select_Extract_Helpers()
        {
            // Arrange
            var collider = _withCollider.GetComponent<BoxCollider>();

            // Assert
            CollectionAssert.AreEqual(new Object[] { _withCollider, _plain }, _tb.DoSelect<GameObject>(_withCollider, collider, _plain));
            CollectionAssert.AreEqual(new[] { collider }, _tb.DoSelect<Collider>(_withCollider, collider));
            CollectionAssert.AreEqual(new[] { collider, collider }, _tb.DoExtract<BoxCollider>(_withCollider, collider, _plain));
            CollectionAssert.AreEqual(new[] { collider }, _tb.DoExtractOrSelect<BoxCollider>(_withCollider, _plain));
            CollectionAssert.AreEqual(new Object[] { _plain }, _tb.DoExtractOrSelect<GameObject>(_plain, collider));
        }

        [Test]
        public void TrackingBehaviour_Lists_Tracking_Event_Fields()
        {
            // Act
            var fields = _tb.GetTrackingBehaviourEvents();

            // Assert
            CollectionAssert.AreEqual(new[] { "OnValue" }, fields.Select(f => f.Name));
        }

        [Test]
        public void TrackingBehaviour_Disable_Unbinds_Unity_Events()
        {
            // Arrange
            _pipeline.StartPipeline();
            var unityEvent = new UnityEvent();
            var received = 0;
            _tb.OnValue.AddHandler((_, v) => received += v);
            _tb.OnValue.Bind(unityEvent, _tb, 5);

            // Act
            unityEvent.Invoke();
            _tb.enabled = false;
            unityEvent.Invoke();

            // Assert
            Assert.AreEqual(5, received);
        }
    }

    public class TrackingBehaviourEventGeneric_Tests
    {
        [Test]
        public void TwoArg_Event_Invoke_Mute_And_Clear()
        {
            // Arrange
            var ev = new TrackingBehaviourEvent<string, int>();
            var received = new List<(string, int)>();
            ev.AddHandler((_, sender, value) => received.Add((sender, value)));

            // Act
            ev.Invoke(null, "a", 1);
            ev.Mute();
            ev.Invoke(null, "b", 2);
            ev.Unmute();
            ev.Invoke(null, "c", 3);
            ev.ClearActions();
            ev.Invoke(null, "d", 4);

            // Assert
            Assert.AreEqual(0, ev.HandlerCount);
            CollectionAssert.AreEqual(new[] { ("a", 1), ("c", 3) }, received);
        }

        [Test]
        public void TwoArg_Event_Binds_UnityEvent_Once_And_Unbinds()
        {
            // Arrange
            var ev = new TrackingBehaviourEvent<string, int>();
            var unityEvent = new UnityEvent<int>();
            var received = 0;
            UnityAction<int> invoker = v => received += v;

            // Act
            ev.Bind(unityEvent, invoker);
            ev.Bind(unityEvent, invoker);
            unityEvent.Invoke(1);
            ev.Unbind(unityEvent);
            unityEvent.Invoke(10);
            ev.Bind(unityEvent, invoker);
            ev.Dispose();
            unityEvent.Invoke(100);

            // Assert
            Assert.AreEqual(1, received);
        }

        [Test]
        public void TwoArg_Event_Bind_Action_Forwards_Calls()
        {
            // Arrange
            var ev = new TrackingBehaviourEvent<string, int>();
            var received = 0;
            Action<int> source = _ => { };

            // Act
            ev.Bind(source, v => received += v);
            source(7);

            // Assert
            Assert.AreEqual(7, received);
        }

        [Test]
        public void ThreeArg_Event_Invoke_And_Mute()
        {
            // Arrange
            var ev = new TrackingBehaviourEvent<string, int, bool>();
            var received = new List<(string, int, bool)>();
            ev.AddHandler((_, sender, value, args) => received.Add((sender, value, args)));

            // Act
            ev.Invoke(null, "a", 1, true);
            ev.Mute();
            ev.Invoke(null, "b", 2, false);
            ev.Unmute();
            ev.Invoke(null, "c", 3, false);
            ev.Dispose();
            ev.Invoke(null, "d", 4, true);

            // Assert
            CollectionAssert.AreEqual(new[] { ("a", 1, true), ("c", 3, false) }, received);
        }
    }
}
