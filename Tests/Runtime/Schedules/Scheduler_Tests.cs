using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using OmiLAXR.Schedules;
using UnityEngine;

namespace OmiLAXR.Tests.Schedules
{
    public class Scheduler_Tests
    {
        public class SchedulerOwner : MonoBehaviour
        {
        }

        public class MockScheduler : Scheduler
        {
            protected override IEnumerator Run()
            {
                while (isActive)
                    yield return null;
            }
        }

        private GameObject _ownerObject;
        private SchedulerOwner _owner;
        private readonly List<string> _calls = new List<string>();
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _ownerObject = new GameObject("SchedulerOwner");
            _owner = _ownerObject.AddComponent<SchedulerOwner>();
            _calls.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
                Object.DestroyImmediate(o);
            _created.Clear();
            Object.DestroyImmediate(_ownerObject);
        }

        private T Track<T>(T scheduler) where T : Scheduler
        {
            _created.Add(scheduler);
            return scheduler;
        }

        private MockScheduler CreateMock(bool runImmediate = false)
        {
            var scheduler = Track(ScriptableObject.CreateInstance<MockScheduler>());
            scheduler.Init(_owner,
                onTick: () => _calls.Add("tick"),
                onTickStart: () => _calls.Add("start"),
                onTickEnd: () => _calls.Add("end"),
                runImmediate: runImmediate);
            scheduler.OnStop += () => _calls.Add("stop");
            return scheduler;
        }

        [Test]
        public void Scheduler_Tests_Start_Triggers_Start_And_Immediate_Tick()
        {
            // Arrange
            var scheduler = CreateMock();

            // Act
            scheduler.Start();

            // Assert
            Assert.IsTrue(scheduler.IsRunning);
            Assert.IsTrue(scheduler.isActive);
            CollectionAssert.AreEqual(new[] { "start", "tick" }, _calls);
            scheduler.Stop();
        }

        [Test]
        public void Scheduler_Tests_Start_Without_Immediate_Tick()
        {
            // Arrange
            var scheduler = CreateMock();
            scheduler.tickImmediate = false;

            // Act
            scheduler.Start();

            // Assert
            CollectionAssert.AreEqual(new[] { "start" }, _calls);
            scheduler.Stop();
        }

        [Test]
        public void Scheduler_Tests_Start_Twice_Is_Ignored()
        {
            // Arrange
            var scheduler = CreateMock();

            // Act
            scheduler.Start();
            scheduler.Start();

            // Assert
            CollectionAssert.AreEqual(new[] { "start", "tick" }, _calls);
            scheduler.Stop();
        }

        [Test]
        public void Scheduler_Tests_Stop_Triggers_End_And_Stop_Once()
        {
            // Arrange
            var scheduler = CreateMock();
            scheduler.Start();
            _calls.Clear();

            // Act
            scheduler.Stop();
            scheduler.Stop();

            // Assert
            Assert.IsFalse(scheduler.IsRunning);
            Assert.IsFalse(scheduler.isActive);
            CollectionAssert.AreEqual(new[] { "end", "stop" }, _calls);
        }

        [Test]
        public void Scheduler_Tests_Start_Without_Owner_Does_Nothing()
        {
            // Arrange
            var scheduler = Track(ScriptableObject.CreateInstance<MockScheduler>());
            scheduler.OnTickStart += () => _calls.Add("start");

            // Act
            scheduler.Start();

            // Assert
            Assert.IsFalse(scheduler.IsRunning);
            CollectionAssert.IsEmpty(_calls);
        }

        [Test]
        public void Scheduler_Tests_Init_RunImmediate_Starts()
        {
            // Act
            var scheduler = CreateMock(runImmediate: true);

            // Assert
            Assert.IsTrue(scheduler.IsRunning);
            Assert.IsTrue(scheduler.runImmediate);
            scheduler.Stop();
        }

        [Test]
        public void Scheduler_Tests_Init_Resets_Handlers_By_Default()
        {
            // Arrange
            var scheduler = CreateMock();
            var newTicks = 0;

            // Act
            scheduler.Init(_owner, onTick: () => newTicks++);
            scheduler.Start();

            // Assert
            Assert.AreEqual(1, newTicks);
            CollectionAssert.IsEmpty(_calls);
            scheduler.Stop();
        }

        [Test]
        public void Scheduler_Tests_Event_Remove_Unsubscribes()
        {
            // Arrange
            var scheduler = CreateMock();
            var ticks = 0;
            void OnTick() => ticks++;
            scheduler.OnTick += OnTick;
            scheduler.OnTick -= OnTick;

            // Act
            scheduler.Start();

            // Assert
            Assert.AreEqual(0, ticks);
            scheduler.Stop();
        }

        [Test]
        public void Scheduler_Tests_Clone_Has_New_Owner_And_No_Handlers()
        {
            // Arrange
            var scheduler = CreateMock();
            scheduler.Start();
            var otherOwner = _ownerObject.AddComponent<SchedulerOwner>();
            _calls.Clear();

            // Act
            var clone = Track((MockScheduler)scheduler.Clone(otherOwner));

            // Assert
            Assert.AreEqual(otherOwner, clone.owner);
            Assert.IsFalse(clone.IsRunning);
            clone.Start();
            CollectionAssert.IsEmpty(_calls);
            clone.Stop();
            scheduler.Stop();
        }

        [Test]
        public void Scheduler_Tests_OnAfterDeserialize_Resets_Runtime_State()
        {
            // Arrange
            var scheduler = CreateMock();
            scheduler.Start();
            _calls.Clear();

            // Act
            scheduler.OnAfterDeserialize();

            // Assert
            Assert.IsFalse(scheduler.IsRunning);
            Assert.IsNull(scheduler.owner);
            CollectionAssert.IsEmpty(_calls);
        }

        [Test]
        public void IntervalTicker_Tests_Create_Configures_Ticker()
        {
            // Act
            var ticker = Track(IntervalTicker.Create(_owner, 0.5f, () => _calls.Add("tick"), tTLTicks: 3));

            // Assert
            Assert.AreEqual(0.5f, ticker.intervalSeconds);
            Assert.AreEqual(3, ticker.tTLTicks);
            Assert.AreEqual(_owner, ticker.owner);
            Assert.IsFalse(ticker.IsRunning);
        }

        [Test]
        public void IntervalTicker_Tests_Stops_When_Tick_Limit_Reached()
        {
            // Arrange
            var ticker = Track(IntervalTicker.Create(_owner, 1f, () => _calls.Add("tick"), tTLTicks: 1,
                onTickEnd: () => _calls.Add("end")));

            // Act
            ticker.Start();

            // Assert
            Assert.IsFalse(ticker.IsRunning);
            CollectionAssert.AreEqual(new[] { "tick", "end" }, _calls);
        }

        [Test]
        public void IntervalTicker_Tests_Restart_Resets_Tick_Count()
        {
            // Arrange
            var ticker = Track(IntervalTicker.Create(_owner, 1f, () => _calls.Add("tick"), tTLTicks: 1));
            ticker.Start();

            // Act
            ticker.Start();

            // Assert
            CollectionAssert.AreEqual(new[] { "tick", "tick" }, _calls);
        }

        [Test]
        public void TimeoutTicker_Tests_Does_Not_Tick_Immediately()
        {
            // Act
            var ticker = Track(TimeoutTicker.Create(_owner, 2f, () => _calls.Add("tick"),
                onTickStart: () => _calls.Add("start"), runImmediate: true));

            // Assert
            Assert.IsFalse(ticker.tickImmediate);
            Assert.AreEqual(2f, ticker.timeoutSeconds);
            Assert.IsTrue(ticker.IsRunning);
            CollectionAssert.AreEqual(new[] { "start" }, _calls);
            ticker.Stop();
        }
    }
}
