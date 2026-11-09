using System.Collections.Generic;
using NUnit.Framework;
using OmiLAXR.Actors.HeartRate;
using OmiLAXR.Composers;
using OmiLAXR.Endpoints;
using OmiLAXR.Hooks;
using OmiLAXR.Tests.Endpoints;
using OmiLAXR.Tests.Mocks;
using OmiLAXR.TrackingBehaviours;
using UnityEngine;

namespace OmiLAXR.Tests
{
    public class DataProvider_Tests
    {
        public class MockComposer : Composer<Pipeline_Tests.MockTrackingBehaviour, MockStatement>
        {
            public override Author GetAuthor() => new Author("Tester", "tester@omilaxr.dev");

            protected override void Compose(Pipeline_Tests.MockTrackingBehaviour tb)
            {
            }

            public void Emit(ITrackingBehaviour owner, MockStatement statement) => SendStatement(owner, statement);
        }

        public class MockHook : Hook
        {
            public bool DiscardStatements;
            public readonly List<IStatement> Seen = new List<IStatement>();

            public override IStatement AfterCompose(IStatement statement)
            {
                Seen.Add(statement);
                if (DiscardStatements)
                    statement.Discard();
                return statement;
            }

            public T Provider<T>(IStatement statement, bool includeInactive = false) where T : ActorDataProvider
                => GetProvider<T>(statement, includeInactive);
        }

        public class MockHeartRateProvider : HeartRateProvider
        {
            public void Set(int hr) => SetHeartRate(hr);
        }

        private GameObject _root;
        private DataProvider _dataProvider;
        private MockComposer _composer;
        private MockHook _hook;
        private Endpoint_Tests.RecordingEndpoint _endpoint;

        [SetUp]
        public void SetUp()
        {
            Endpoint_Tests.RecordingEndpoint.UseThreads = false;
            _root = new GameObject("Data Provider");
            _root.SetActive(false);
            _dataProvider = _root.AddComponent<DataProvider>();
            _composer = AddChild<MockComposer>("Composer");
            _hook = AddChild<MockHook>("Hook");
            _endpoint = AddChild<Endpoint_Tests.RecordingEndpoint>("Endpoint");
            _root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        private T AddChild<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            return go.AddComponent<T>();
        }

        [Test]
        public void DataProvider_Tests_Collects_Children()
        {
            // Assert
            CollectionAssert.AreEqual(new IComposer[] { _composer }, _dataProvider.Composers);
            CollectionAssert.AreEqual(new Hook[] { _hook }, _dataProvider.Hooks);
            CollectionAssert.AreEqual(new Endpoint[] { _endpoint }, _dataProvider.Endpoints);
            Assert.AreSame(_composer, _dataProvider.GetComposer<MockComposer>());
            Assert.AreEqual("Mock", _composer.GetName());
            Assert.AreEqual(ComposerGroup.Other, _composer.GetGroup());
            Assert.IsFalse(_composer.IsHigherComposer);
        }

        [Test]
        public void DataProvider_Tests_Routes_Statement_Through_Hooks_To_Endpoints()
        {
            // Arrange
            var statement = new MockStatement();

            // Act
            _composer.Emit(null, statement);
            _endpoint.StopSending();

            // Assert
            CollectionAssert.AreEqual(new[] { statement }, _hook.Seen);
            CollectionAssert.AreEqual(new[] { statement }, _endpoint.Handled);
        }

        [Test]
        public void DataProvider_Tests_Discarded_Statements_Do_Not_Reach_Endpoints()
        {
            // Arrange
            _hook.DiscardStatements = true;

            // Act
            _composer.Emit(null, new MockStatement());
            _endpoint.StopSending();

            // Assert
            Assert.AreEqual(1, _hook.Seen.Count);
            Assert.IsEmpty(_endpoint.Handled);
        }

        [Test]
        public void DataProvider_Tests_Disabled_Hooks_And_Endpoints_Are_Skipped()
        {
            // Arrange
            _hook.enabled = false;
            _endpoint.enabled = false;

            // Act
            _composer.Emit(null, new MockStatement());

            // Assert
            Assert.IsEmpty(_hook.Seen);
            Assert.IsEmpty(_endpoint.Handled);
        }

        [Test]
        public void DataProvider_Tests_Disable_Unsubscribes_From_Composers()
        {
            // Arrange
            _root.SetActive(false);
            _composer.gameObject.transform.SetParent(null);
            _composer.gameObject.SetActive(true);

            // Act
            _composer.Emit(null, new MockStatement());

            // Assert
            Assert.IsEmpty(_hook.Seen);
            Assert.IsEmpty(_dataProvider.Composers);
            Object.DestroyImmediate(_composer.gameObject);
        }

        [Test]
        public void Composer_Tests_Queues_Statements_Until_A_Handler_Subscribes()
        {
            // Arrange
            var go = new GameObject("Standalone Composer");
            var composer = go.AddComponent<MockComposer>();
            composer.dropStatementsIfNoHandlers = false;
            composer.maxQueuedStatements = 1;
            var received = new List<IStatement>();
            var first = new MockStatement("first");

            // Act
            composer.Emit(null, first);
            composer.Emit(null, new MockStatement("dropped because of cap"));
            composer.AfterComposed += (_, s) => received.Add(s);

            // Assert
            CollectionAssert.AreEqual(new[] { first }, received);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Composer_Tests_Drops_Statements_Without_Handlers_By_Default()
        {
            // Arrange
            var go = new GameObject("Standalone Composer");
            var composer = go.AddComponent<MockComposer>();
            var received = new List<IStatement>();

            // Act
            composer.Emit(null, new MockStatement());
            composer.AfterComposed += (_, s) => received.Add(s);

            // Assert
            Assert.IsEmpty(received);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Composer_Tests_Stores_And_Restores_Statements()
        {
            // Arrange
            var statement = new MockStatement();

            // Act
            _composer.StoreStatement("key", statement);
            _composer.StoreStatement(7, statement);

            // Assert
            Assert.AreSame(statement, _composer.RestoreStatement("key"));
            Assert.AreSame(statement, _composer.RestoreStatement("key", erase: true));
            Assert.IsNull(_composer.RestoreStatement("key"));
            Assert.AreSame(statement, _composer.RestoreStatement(7, erase: true));
            Assert.IsNull(_composer.RestoreStatement(7));
        }

        [Test]
        public void Hook_Tests_GetProvider_Finds_Enabled_Actor_Data_Provider()
        {
            // Arrange
            var pipelineObject = new GameObject("Pipeline");
            pipelineObject.SetActive(false);
            var pipeline = pipelineObject.AddComponent<Pipeline_Tests.MockPipeline>();
            pipelineObject.AddComponent<Actor>();
            var provider = pipelineObject.AddComponent<MockHeartRateProvider>();
            provider.Set(90);
            pipeline.StartPipeline();
            var statement = new MockStatement();
            statement.SetPipelineInfo(new PipelineInfo(pipeline));

            // Act
            var found = _hook.Provider<HeartRateProvider>(statement);

            // Assert
            Assert.AreSame(provider, found);
            Assert.AreEqual(90, found.GetHeartRate());
            pipeline.StopPipeline();
            Object.DestroyImmediate(pipelineObject);
        }
    }
}
