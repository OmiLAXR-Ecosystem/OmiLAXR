using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OmiLAXR.Components;
using OmiLAXR.Components.Gaze.Saccade;
using OmiLAXR.Composers;
using OmiLAXR.Context;
using OmiLAXR.Endpoints;
using OmiLAXR.Filters;
using OmiLAXR.Hooks;
using OmiLAXR.Schedules;
using OmiLAXR.Tests.Mocks;
using OmiLAXR.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OmiLAXR.Tests
{
    public class Abstractions_Tests
    {
        public class ManualEndpoint : Endpoint
        {
            public readonly ManualSendWorker Worker = new ManualSendWorker();
            public readonly List<IStatement> Handled = new List<IStatement>();

            protected override ISendWorker CreateSendWorker() => Worker;

            protected override TransferCode HandleSending(IStatement statement)
            {
                Handled.Add(statement);
                return TransferCode.Success;
            }
        }

        private sealed class FakeHook : IStatementHook
        {
            public bool Discard;
            public int Calls;

            public IStatement AfterCompose(IStatement statement)
            {
                Calls++;
                if (Discard)
                    statement.Discard();
                return statement;
            }
        }

        private sealed class FakeSink : IStatementSink
        {
            public readonly List<IStatement> Received = new List<IStatement>();
            public void SendStatement(IStatement statement) => Received.Add(statement);
        }

        private sealed class FakeFilter : IObjectFilter
        {
            private readonly Func<Object[], Object[]> _pass;
            public FakeFilter(Func<Object[], Object[]> pass) => _pass = pass;
            public Object[] Pass(Object[] objects) => _pass(objects);
        }

        private sealed class FakeTrackingNames : ITrackingNameSettings
        {
            public GlobalSettings.TrackingNameBehaviour TrackingNameBehaviour { get; set; }
        }

        private readonly List<Object> _created = new List<Object>();
        private InMemoryFileSystem _fileSystem;
        private CapturingLogWriter _log;

        [SetUp]
        public void SetUp()
        {
            _fileSystem = new InMemoryFileSystem();
            FileSystem.Current = _fileSystem;
            _log = new CapturingLogWriter();
            DebugLog.Writer = _log;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
                Object.DestroyImmediate(o);
            _created.Clear();
            FileSystem.Current = null;
            DebugLog.Writer = null;
            Clock.Current = null;
            ScenarioContext.Reset();
        }

        private T Create<T>(string name = null, bool active = true) where T : Component
        {
            var go = new GameObject(name ?? typeof(T).Name);
            go.SetActive(active);
            _created.Add(go);
            return go.AddComponent<T>();
        }

        // --- Endpoint: send worker, transport and lifecycle ---------------------------------

        [Test]
        public void Endpoint_With_Manual_Worker_Sends_Only_When_Stepped()
        {
            // Arrange
            var endpoint = Create<ManualEndpoint>();
            var statements = Enumerable.Range(0, 60).Select(i => new MockStatement("s" + i)).ToArray();

            // Act
            foreach (var s in statements)
                endpoint.SendStatement(s);

            // Assert: nothing is sent until the worker steps; each step sends one batch of 50
            Assert.IsEmpty(endpoint.Handled);
            Assert.AreEqual(61, endpoint.Worker.Signals); // one on start + one per statement
            Assert.AreEqual(TransferCode.Success, endpoint.Worker.Step());
            Assert.AreEqual(50, endpoint.Handled.Count);
            Assert.AreEqual(TransferCode.Success, endpoint.Worker.Step());
            Assert.AreEqual(TransferCode.NoStatements, endpoint.Worker.Step());
            CollectionAssert.AreEqual(statements, endpoint.Handled);
        }

        [Test]
        public void Endpoint_Pause_Keeps_Queue_And_Resume_Continues()
        {
            // Arrange
            var endpoint = Create<ManualEndpoint>();

            // Act
            endpoint.PauseSending();
            endpoint.SendStatement(new MockStatement("during pause"));

            // Assert
            Assert.IsFalse(endpoint.Worker.IsRunning);
            Assert.AreEqual(TransferCode.NoStatements, endpoint.Worker.Step());

            // Act
            endpoint.StartSending();
            endpoint.Worker.Step();

            // Assert
            Assert.AreEqual("during pause", endpoint.Handled.Single().ToShortString());
        }

        [Test]
        public void Endpoint_As_Transport_Sends_Batch_Synchronously()
        {
            // Arrange
            var endpoint = Create<ManualEndpoint>();
            IStatementTransport transport = endpoint;
            var sent = 0;
            endpoint.OnSentStatement += (_, __) => sent++;

            // Act
            var result = transport.Send(new IStatement[] { new MockStatement(), new MockStatement() });

            // Assert
            Assert.AreEqual(TransferCode.Success, result);
            Assert.AreEqual(2, sent);
            Assert.AreEqual(TransferCode.NoStatements, transport.Send(new IStatement[0]));
        }

        [Test]
        public void SendWorkerPolicy_Waits_Longer_When_Idle_And_Stops_On_Invalid_Credentials()
        {
            Assert.AreEqual(100, SendWorkerPolicy.WaitMilliseconds(TransferCode.NoStatements));
            Assert.AreEqual(10, SendWorkerPolicy.WaitMilliseconds(TransferCode.Success));
            Assert.IsTrue(SendWorkerPolicy.ShouldContinue(TransferCode.Error));
            Assert.IsFalse(SendWorkerPolicy.ShouldContinue(TransferCode.InvalidCredentials));
        }

        // --- DataProvider routing through IStatementHook / IStatementSink ------------------

        [Test]
        public void DataProvider_Routes_Through_Registered_Plain_Hooks_And_Sinks()
        {
            // Arrange
            var dataProvider = Create<DataProvider>("Data Provider", active: false);
            var composer = new GameObject("Composer").AddComponent<DataProvider_Tests.MockComposer>();
            composer.transform.SetParent(dataProvider.transform);
            var hook = new FakeHook();
            var sink = new FakeSink();
            dataProvider.AddHook(hook);
            dataProvider.AddSink(sink);
            dataProvider.gameObject.SetActive(true);
            var statement = new MockStatement();

            // Act
            composer.Emit(null, statement);

            // Assert
            Assert.AreEqual(1, hook.Calls);
            CollectionAssert.AreEqual(new[] { statement }, sink.Received);

            // Act: re-enabling keeps registered hooks and sinks
            dataProvider.gameObject.SetActive(false);
            dataProvider.gameObject.SetActive(true);
            hook.Discard = true;
            composer.Emit(null, new MockStatement());

            // Assert
            Assert.AreEqual(2, hook.Calls);
            Assert.AreEqual(1, sink.Received.Count);
        }

        // --- Pipeline abstractions ---------------------------------------------------------

        [Test]
        public void ObjectFilters_Apply_Chains_Filters_And_Skips_Disabled_Components()
        {
            // Arrange
            var a = new GameObject("a");
            var b = new GameObject("b");
            _created.Add(a);
            _created.Add(b);
            var disabled = Create<Pipeline_Tests.MockFilter>();
            disabled.Rejected = a;
            disabled.enabled = false;
            var filters = new IObjectFilter[]
            {
                new FakeFilter(objects => objects.Where(o => o != b).ToArray()),
                disabled,
                new FakeFilter(_ => null)
            };

            // Act & Assert
            CollectionAssert.AreEqual(new Object[] { a }, ObjectFilters.Apply(filters.Take(2), new Object[] { a, b }));
            CollectionAssert.IsEmpty(ObjectFilters.Apply(filters, new Object[] { a }));
            CollectionAssert.IsEmpty(ObjectFilters.Apply(new IObjectFilter[0], null));
        }

        [Test]
        public void Pipeline_Exposes_Events_Through_Interface()
        {
            // Arrange
            var pipelineObject = new GameObject("Pipeline");
            _created.Add(pipelineObject);
            pipelineObject.SetActive(false);
            pipelineObject.AddComponent<Actor>();
            var pipeline = pipelineObject.AddComponent<Pipeline>();
            IPipelineEvents events = pipeline;
            var started = 0;
            events.AfterStartedPipeline += _ => started++;

            // Act
            pipeline.StartPipeline();

            // Assert
            Assert.AreEqual(1, started);
            pipeline.StopPipeline();
        }

        // --- Logging, clock, file system ---------------------------------------------------

        [Test]
        public void DebugLog_Writes_Through_Replaceable_Writer()
        {
            // Act
            DebugLog.OmiLAXR.Print("hello {0}", "world");
            DebugLog.OmiLAXR.Warning("careful");
            DebugLog.OmiLAXR.Error("broken");

            // Assert
            Assert.IsTrue(_log.Contains(LogType.Log, "[OmiLAXR]: hello world"));
            Assert.IsTrue(_log.Contains(LogType.Warning, "careful"));
            Assert.IsTrue(_log.Contains(LogType.Error, "broken"));

            // Act
            DebugLog.Writer = null;

            // Assert
            Assert.AreSame(UnityLogWriter.Instance, DebugLog.Writer);
        }

        [Test]
        public void SaccadeLogic_Uses_Clock_For_Start_And_End()
        {
            // Arrange
            var clock = new FixedClock(new DateTime(2025, 1, 1, 12, 0, 0));
            Clock.Current = clock;
            var logic = ScriptableObject.CreateInstance<SaccadeLogicVelocityThreshold>();
            var forward = Vector3.forward;
            var side = Quaternion.Euler(0, 5, 0) * Vector3.forward;

            // Act
            logic.TryUpdateSaccade(null, null, forward, side, 0.01f, null, out _, out var start);
            clock.Advance(TimeSpan.FromMilliseconds(120));
            logic.TryUpdateSaccade(null, null, side, side, 0.01f, null, out _, out var end);

            // Assert
            Assert.AreEqual(clock.Now.AddMilliseconds(-120), start.StartTime);
            Assert.AreEqual(OmiLAXR.Types.Duration.FromMilliseconds(120), end.Duration);
            Object.DestroyImmediate(logic);
        }

        [Test]
        public void LocalFileEndpoint_Writes_Into_File_System_And_Names_File_By_Clock()
        {
            // Arrange
            Clock.Current = new FixedClock(new DateTime(2025, 3, 4, 5, 6, 7));
            var endpoint = Create<LocalFileEndpoint>("Endpoint", active: false);
            endpoint.defaultFolder = LocalFileEndpoint.DefaultFolderPaths.TempFolder;
            endpoint.statementsFolder = "statements";
            endpoint.gameObject.SetActive(true);

            // Act
            endpoint.SendStatement(new MockStatement("a"));
            endpoint.SendStatement(new MockStatement("b"));
            endpoint.gameObject.SetActive(false);

            // Assert
            var path = Path.Combine(Path.GetTempPath(), "statements", "20250304050607.jsonl");
            Assert.IsTrue(_fileSystem.FileExists(path), string.Join(", ", _fileSystem.Files));
            CollectionAssert.AreEquivalent(new[] { "{\"text\":\"a\"}", "{\"text\":\"b\"}" },
                _fileSystem.ReadAllText(path).Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
        }

        [Test]
        public void CsvFileEndpoint_Merges_Header_And_Data_In_File_System()
        {
            // Arrange
            Clock.Current = new FixedClock(new DateTime(2025, 3, 4, 5, 6, 7));
            var endpoint = Create<CsvFileEndpoint>("Endpoint", active: false);
            endpoint.defaultFolder = LocalFileEndpoint.DefaultFolderPaths.TempFolder;
            endpoint.statementsFolder = "statements";
            endpoint.gameObject.SetActive(true);

            // Act
            endpoint.SendStatement(new MockStatement("a"));
            endpoint.gameObject.SetActive(false);

            // Assert
            var path = Path.Combine(Path.GetTempPath(), "statements", "20250304050607.csv");
            var lines = _fileSystem.ReadAllText(path).Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual("id,text", lines[0]);
            StringAssert.EndsWith(",a", lines[1]);
            Assert.IsFalse(_fileSystem.FileExists(path + ".data"));
        }

        [Test]
        public void BufferedUtf8Writer_Writes_To_Given_Stream()
        {
            // Arrange
            var stream = _fileSystem.OpenWrite("memory.txt", append: false);

            // Act
            using (var writer = new BufferedUtf8Writer(stream, "memory.txt"))
                writer.WriteLine("line");

            // Assert
            Assert.AreEqual("line" + Environment.NewLine, _fileSystem.ReadAllText("memory.txt"));
            Assert.Throws<ArgumentNullException>(() => new BufferedUtf8Writer((Stream)null, "x"));
        }

        // --- Scenario context ----------------------------------------------------------------

        [Test]
        public void ScenarioContext_Override_Controls_Tracking_Name()
        {
            // Arrange
            var parent = new GameObject("Parent");
            _created.Add(parent);
            var child = new GameObject("Child");
            child.transform.SetParent(parent.transform);

            // Act & Assert
            Assert.AreEqual("Child", child.GetTrackingName());
            ScenarioContext.TrackingNames = new FakeTrackingNames
                { TrackingNameBehaviour = GlobalSettings.TrackingNameBehaviour.HierarchyTreePath };
            Assert.AreEqual("Parent/Child", child.GetTrackingName());
            ScenarioContext.Reset();
            Assert.IsNull(ScenarioContext.TrackingNames);
        }

        [Test]
        public void ScenarioContext_Defaults_To_Scene_Components()
        {
            // Arrange
            var registration = Create<Registration>();
            var language = Create<ScenarioLanguage>();
            language.language = OmiLAXR.Types.Languages.de;
            var fps = Create<FpsMonitor>();

            // Assert
            Assert.AreSame(registration, ScenarioContext.Registration);
            Assert.IsNull(ScenarioContext.Registration.GetRegistration(), "empty uuid");
            registration.uuid = Guid.Empty.ToString();
            Assert.AreEqual(Guid.Empty, ScenarioContext.Registration.GetRegistration());
            registration.enabled = false;
            Assert.IsNull(ScenarioContext.Registration.GetRegistration());
            Assert.AreEqual("de", ScenarioContext.Language.GetLanguageCode());
            Assert.AreSame(fps, ScenarioContext.Fps);
            fps.enabled = false;
            Assert.IsNull(ScenarioContext.Fps.GetFps());
        }

        // --- Scheduler as ITicker -----------------------------------------------------------

        [Test]
        public void Scheduler_Is_Usable_As_Ticker()
        {
            // Arrange
            var owner = Create<Schedules.Scheduler_Tests.SchedulerOwner>();
            ITicker ticker = IntervalTicker.Create(owner, 10f, null);
            var ticks = 0;
            ticker.OnTick += () => ticks++;

            // Act
            ticker.Start();
            ticker.Stop();

            // Assert
            Assert.AreEqual(1, ticks);
            Assert.IsFalse(ticker.IsRunning);
            Object.DestroyImmediate((Object)ticker);
        }
    }
}
