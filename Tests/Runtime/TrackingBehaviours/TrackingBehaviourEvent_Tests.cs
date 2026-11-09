using NUnit.Framework;
using OmiLAXR.TrackingBehaviours;
using UnityEngine.Events;

namespace OmiLAXR.Tests.TrackingBehaviours
{
    public class TrackingBehaviourEvent_Tests
    {
        [Test]
        public void TrackingBehaviourEvent_Tests_Invoke_Calls_Handlers_With_Owner()
        {
            // Arrange
            var ev = new TrackingBehaviourEvent();
            ITrackingBehaviour received = null;
            var calls = 0;
            ev.AddHandler(sender => { received = sender; calls++; });

            // Act
            ev.Invoke(null);

            // Assert
            Assert.AreEqual(1, calls);
            Assert.IsNull(received);
            Assert.AreEqual(1, ev.Actions.Count);
        }

        [Test]
        public void TrackingBehaviourEvent_Tests_Mute_Suppresses_Invoke()
        {
            // Arrange
            var ev = new TrackingBehaviourEvent();
            var calls = 0;
            ev.AddHandler(_ => calls++);

            // Act
            ev.Mute();
            ev.Invoke(null);
            ev.Unmute();
            ev.Invoke(null);

            // Assert
            Assert.AreEqual(1, calls);
            Assert.IsFalse(ev.IsDisabled);
        }

        [Test]
        public void TrackingBehaviourEvent_Tests_ClearActions_Removes_Handlers()
        {
            // Arrange
            var ev = new TrackingBehaviourEvent();
            var calls = 0;
            ev.AddHandler(_ => calls++);

            // Act
            ev.ClearActions();
            ev.Invoke(null);

            // Assert
            Assert.AreEqual(0, calls);
            Assert.AreEqual(0, ev.Actions.Count);
        }

        [Test]
        public void TrackingBehaviourEvent_Tests_Bind_Forwards_UnityEvent_Once()
        {
            // Arrange
            var ev = new TrackingBehaviourEvent();
            var unityEvent = new UnityEvent();
            var calls = 0;
            ev.AddHandler(_ => calls++);

            // Act
            ev.Bind(unityEvent, (ITrackingBehaviour)null);
            ev.Bind(unityEvent, (ITrackingBehaviour)null);
            unityEvent.Invoke();

            // Assert
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void TrackingBehaviourEvent_Tests_Unbind_And_UnbindAll()
        {
            // Arrange
            var ev = new TrackingBehaviourEvent();
            var first = new UnityEvent();
            var second = new UnityEvent();
            var calls = 0;
            ev.AddHandler(_ => calls++);
            ev.Bind(first, (ITrackingBehaviour)null);
            ev.Bind(second, (ITrackingBehaviour)null);

            // Act
            ev.Unbind(first);
            first.Invoke();
            second.Invoke();
            ev.UnbindAll();
            second.Invoke();

            // Assert
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void TrackingBehaviourEvent_Tests_Dispose_Unbinds_And_Clears()
        {
            // Arrange
            var ev = new TrackingBehaviourEvent();
            var unityEvent = new UnityEvent();
            var calls = 0;
            ev.AddHandler(_ => calls++);
            ev.Bind(unityEvent, (ITrackingBehaviour)null);

            // Act
            ev.Dispose();
            unityEvent.Invoke();
            ev.Invoke(null);

            // Assert
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void TrackingBehaviourEvent_Generic_Tests_Invoke_Passes_Argument()
        {
            // Arrange
            var ev = new TrackingBehaviourEvent<int>();
            var received = 0;
            ev.AddHandler((_, value) => received = value);

            // Act
            ev.Invoke(null, 42);
            ev.Mute();
            ev.Invoke(null, 7);

            // Assert
            Assert.AreEqual(42, received);
        }

        [Test]
        public void TrackingBehaviourEvent_Generic_Tests_Bind_Passes_Fixed_Argument()
        {
            // Arrange
            var ev = new TrackingBehaviourEvent<string>();
            var unityEvent = new UnityEvent();
            string received = null;
            ev.AddHandler((_, value) => received = value);

            // Act
            ev.Bind(unityEvent, null, "bound");
            unityEvent.Invoke();

            // Assert
            Assert.AreEqual("bound", received);
        }
    }
}
