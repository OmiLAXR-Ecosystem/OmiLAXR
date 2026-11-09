using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OmiLAXR.Composers;
using OmiLAXR.Endpoints;
using OmiLAXR.Tests.Mocks;
using OmiLAXR.Utils;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OmiLAXR.Tests.Endpoints
{
    public class Endpoint_Tests
    {
        public class RecordingEndpoint : Endpoint
        {
            public static bool UseThreads;
            protected override bool useThreads => UseThreads;

            public readonly List<IStatement> Handled = new List<IStatement>();
            public Func<IStatement, TransferCode> Handler;

            protected override TransferCode HandleSending(IStatement statement)
            {
                lock (Handled)
                    Handled.Add(statement);
                return Handler?.Invoke(statement) ?? TransferCode.Success;
            }

            public int QueuedCount
            {
                get { lock (QueuedStatements) return QueuedStatements.Count; }
            }

            public void RunOnMainThread(Action action) => Dispatch(action);
        }

        private GameObject _endpointObject;
        private RecordingEndpoint _endpoint;

        private RecordingEndpoint CreateEndpoint(bool threads)
        {
            RecordingEndpoint.UseThreads = threads;
            _endpointObject = new GameObject("Endpoint");
            _endpointObject.SetActive(false);
            _endpoint = _endpointObject.AddComponent<RecordingEndpoint>();
            return _endpoint;
        }

        [TearDown]
        public void TearDown()
        {
            if (_endpointObject)
                Object.DestroyImmediate(_endpointObject);
        }

        [Test]
        public void Endpoint_Tests_Starts_Sending_When_Enabled()
        {
            // Arrange
            var endpoint = CreateEndpoint(threads: false);
            var started = 0;
            endpoint.OnStartedSending += _ => started++;

            // Act
            _endpointObject.SetActive(true);

            // Assert
            Assert.IsTrue(endpoint.IsSending);
            Assert.AreEqual(1, started);
            Assert.AreSame(DataMap.empty, endpoint.ProvideDataMap());
        }

        [Test]
        public void Endpoint_Tests_Stop_Flushes_Queue_And_Fires_Events()
        {
            // Arrange
            var endpoint = CreateEndpoint(threads: false);
            _endpointObject.SetActive(true);
            var sending = 0;
            var sent = 0;
            var batches = 0;
            var stopped = 0;
            endpoint.OnSendingStatement += (_, __) => sending++;
            endpoint.OnSentStatement += (_, __) => sent++;
            endpoint.OnSentBatch += (_, __) => batches++;
            endpoint.OnStoppedSending += _ => stopped++;
            var statements = Enumerable.Range(0, 5).Select(i => new MockStatement("s" + i)).ToArray();

            // Act
            foreach (var s in statements)
                endpoint.SendStatement(s);
            endpoint.StopSending();

            // Assert
            CollectionAssert.AreEqual(statements, endpoint.Handled);
            Assert.AreEqual(5UL, endpoint.RecordedStatements);
            Assert.AreEqual(5, sending);
            Assert.AreEqual(5, sent);
            Assert.AreEqual(1, batches);
            Assert.AreEqual(1, stopped);
            Assert.IsFalse(endpoint.IsSending);
        }

        [Test]
        public void Endpoint_Tests_Ignores_Statements_After_Stop()
        {
            // Arrange
            var endpoint = CreateEndpoint(threads: false);
            _endpointObject.SetActive(true);
            endpoint.StopSending();

            // Act
            endpoint.SendStatement(new MockStatement());

            // Assert
            Assert.AreEqual(0, endpoint.QueuedCount);
        }

        [Test]
        public void Endpoint_Tests_Can_Restart_After_Stop()
        {
            // Arrange
            var endpoint = CreateEndpoint(threads: false);
            _endpointObject.SetActive(true);
            endpoint.StopSending();

            // Act
            endpoint.StartSending();

            // Assert
            Assert.IsTrue(endpoint.IsSending);
        }

        [Test]
        public void Endpoint_Tests_Can_Resume_After_Pause()
        {
            // Arrange
            var endpoint = CreateEndpoint(threads: false);
            _endpointObject.SetActive(true);
            var paused = 0;
            endpoint.OnPausedSending += _ => paused++;

            // Act
            endpoint.PauseSending();
            endpoint.StartSending();

            // Assert
            Assert.AreEqual(1, paused);
            Assert.IsTrue(endpoint.IsSending);
        }

        [Test]
        public void Endpoint_Tests_Stop_Without_Flush_Drops_Queue()
        {
            // Arrange
            var endpoint = CreateEndpoint(threads: false);
            SetPrivateField(endpoint, "flushOnStop", false);
            _endpointObject.SetActive(true);
            endpoint.SendStatement(new MockStatement());

            // Act
            endpoint.StopSending();

            // Assert
            Assert.IsEmpty(endpoint.Handled);
            Assert.AreEqual(0, endpoint.QueuedCount);
        }

        [Test]
        public void Endpoint_Tests_Stop_With_Budget_Flushes_In_Batches()
        {
            // Arrange
            var endpoint = CreateEndpoint(threads: false);
            SetPrivateField(endpoint, "flushOnStopSecondsBudget", 5f);
            _endpointObject.SetActive(true);
            var batches = 0;
            endpoint.OnSentBatch += (_, __) => batches++;

            // Act
            for (var i = 0; i < 120; i++)
                endpoint.SendStatement(new MockStatement());
            endpoint.StopSending();

            // Assert
            Assert.AreEqual(120UL, endpoint.RecordedStatements);
            Assert.AreEqual(3, batches);
        }

        [Test]
        public void Endpoint_Tests_Failed_Statement_Is_Reported()
        {
            // Arrange
            var endpoint = CreateEndpoint(threads: false);
            _endpointObject.SetActive(true);
            endpoint.Handler = _ => TransferCode.Error;
            var failed = 0;
            endpoint.OnFailedSendingStatement += (_, __) => failed++;

            // Act
            endpoint.SendStatement(new MockStatement());
            endpoint.StopSending();

            // Assert
            Assert.AreEqual(0UL, endpoint.RecordedStatements);
            Assert.AreEqual(1, failed);
        }

        [Test]
        public void Endpoint_Tests_Exception_Requeues_Batch()
        {
            // Arrange
            var endpoint = CreateEndpoint(threads: false);
            SetPrivateField(endpoint, "flushOnStop", true);
            _endpointObject.SetActive(true);
            var calls = 0;
            endpoint.Handler = _ => ++calls == 1 ? throw new InvalidOperationException("boom") : TransferCode.Success;
            var failedBatches = 0;
            endpoint.OnFailedSendingBatch += (_, __) => failedBatches++;
            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            var statement = new MockStatement();

            // Act
            endpoint.SendStatement(statement);
            endpoint.StopSending();

            // Assert
            Assert.AreEqual(1, failedBatches);
            Assert.AreEqual(1, endpoint.QueuedCount);
        }

        [UnityTest]
        public IEnumerator Endpoint_Tests_Coroutine_Worker_Sends_Queued_Statements()
        {
            // Arrange
            var endpoint = CreateEndpoint(threads: false);
            _endpointObject.SetActive(true);

            // Act
            endpoint.SendStatement(new MockStatement());
            endpoint.SendStatement(new MockStatement());
            yield return WaitUntil(() => endpoint.RecordedStatements == 2);

            // Assert
            Assert.AreEqual(2UL, endpoint.RecordedStatements);
            Assert.AreEqual(0, endpoint.QueuedCount);
        }

        [UnityTest]
        public IEnumerator Endpoint_Tests_Thread_Worker_Sends_All_Statements_In_Order()
        {
            // Arrange
            var endpoint = CreateEndpoint(threads: true);
            _endpointObject.SetActive(true);
            var statements = Enumerable.Range(0, 200).Select(i => new MockStatement("s" + i)).ToArray();

            // Act
            foreach (var s in statements)
                endpoint.SendStatement(s);
            yield return WaitUntil(() => endpoint.RecordedStatements == 200);
            endpoint.StopSending();

            // Assert
            lock (endpoint.Handled)
                CollectionAssert.AreEqual(statements, endpoint.Handled);
        }

        [UnityTest]
        public IEnumerator Endpoint_Tests_Dispatch_Runs_On_Main_Thread_Update()
        {
            // Arrange
            var endpoint = CreateEndpoint(threads: false);
            _endpointObject.SetActive(true);
            var mainThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            var executedOn = -1;

            // Act
            System.Threading.Tasks.Task.Run(() =>
                endpoint.RunOnMainThread(() => executedOn = System.Threading.Thread.CurrentThread.ManagedThreadId)).Wait();
            yield return null;

            // Assert
            Assert.AreEqual(mainThread, executedOn);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds = 5f)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private static void SetPrivateField(Endpoint endpoint, string name, object value)
            => typeof(Endpoint).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(endpoint, value);
    }
}
