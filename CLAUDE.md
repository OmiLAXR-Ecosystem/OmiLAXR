# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Working rules (versioning, CHANGELOG, `.meta` files, licenses, compatibility) are in @AGENTS.md. Follow them.

## What this is

`com.rwth.unity.omilaxr`: Core Unity package of the OmiLAXR learning-analytics framework for XR: the pipeline pattern (listeners → filters → tracking behaviours → composers → hooks → endpoints). It is format-agnostic; `OmiLAXR.xAPI` adds the xAPI implementation.

## Commands

There is no command-line build or linter: the package compiles inside a Unity project. Paths below are relative to the workspace root (`..`).

- **Develop:** open `OmiLAXR_Dev/` in Unity 6000.3.13f1. Its `Packages/manifest.json` links `com.rwth.unity.omilaxr` from disk (`file:`), so edits recompile live.
- **Tests** (`Tests/`, assembly `OmiLAXR.Tests.Runtime`): the test asmdef is a runtime assembly, so the tests are PlayMode-only. Run them in the Unity Test Runner (PlayMode tab).
- **CLI test run.** Batch mode fails while the editor has the project open. In that case, run against an APFS copy (`cp -c -R OmiLAXR_Dev <scratch>/Dev`).

  ```bash
  /Applications/Unity/Hub/Editor/6000.3.13f1/Unity.app/Contents/MacOS/Unity -batchmode \
    -projectPath OmiLAXR_Dev -runTests -testPlatform PlayMode \
    -assemblyNames OmiLAXR.Tests.Runtime -testResults results.xml
  # single test: replace -assemblyNames … with -testFilter OmiLAXR.Tests.<Class>.<Method>
  ```

## Architecture

Everything is a `MonoBehaviour` that finds its collaborators in the scene hierarchy when enabled. Wiring is done in scenes and prefabs (`Resources/Prefabs/OmiLAXR.prefab`, `Pipelines/`, `Data Providers/`), not in code.

**Pipeline side** (`Runtime/Pipeline.cs`, with subclasses `LearnerPipeline` and `SystemPipeline` in `Runtime/Pipelines/`)
- On start, the pipeline collects its child `Listener`s (`IObjectSource`).
- Found objects run through the `Filter`s (`IObjectFilter`, `ObjectFilters.Apply`).
- `TrackingBehaviour`s attach to the remaining objects and raise `TrackingBehaviourEvent`s.
- `[Action]`/`[Gesture]`-tagged properties are collected into `Pipeline.Actions`/`Gestures`.
- An `Actor`/`ActorGroup` and an `Instructor` (`IActorProvider`) say whose data it is.
- `PipelineExtension<TPipeline>` on another GameObject registers extra listeners, filters and tracking behaviours in `Awake`. This is how modules plug in without touching the core.

**Data side** (`Runtime/DataProvider.cs`). Data providers are found scene-wide and own child components:
- `Composer<TTrackingBehaviour, TStatement>` subscribes to all tracking behaviours of its type and turns events into `IStatement`s.
- `HigherComposer`s (`Runtime/HigherComposers/`) watch composed statements and build aggregate statements.
- Hooks (`IStatementHook.AfterCompose`) run in order and can change or `Discard()` a statement.
- Endpoints (`IStatementSink`) send it: `LocalFileEndpoint`, `CsvFileEndpoint`, `BasicAuthEndpoint`, …
- `DataProvider.AddHook`/`AddSink` also accept plain C# objects, which tests use.

**Statements.** `IStatement` is composed of small interfaces: `IStatementIdentity`, `IStatementProvenance`, `IDiscardable`, `IJsonSerializableStatement` and `ICsvSerializableStatement`. Depend on the smallest one you need.

**Endpoints** (`Runtime/Endpoints/Endpoint.cs`)
- Statements are queued and sent in batches by an `ISendWorker` (`ThreadSendWorker`, `CoroutineSendWorker`, `ManualSendWorker`), chosen by `protected virtual CreateSendWorker()` (default depends on `useThreads`).
- Lifecycle is `ISendingLifecycle`: Start/Pause/Stop. Pausing keeps accepting and queueing statements.
- Subclasses implement `HandleSending(IStatement)` and may override the batch overload or `HandleQueue`. All return a `TransferCode`, and `SendWorkerPolicy` decides whether to continue or wait.

**Scenario context** (`Runtime/Context/`)
- The components are `Registration`, `ScenarioLanguage`, `PlatformInformation`, `FpsMonitor` and `GlobalSettings`.
- They are reached through `ScenarioContext.Registration/Language/Platform/Fps/TrackingNames`. Each property defaults to the scene component and can be overridden. `ScenarioContext.Reset()` restores the defaults.

**Other runtime pieces**
- `Runtime/Schedules`: `Scheduler` (`ITicker`) and the interval/timeout tickers used by tracking behaviours.
- `Runtime/Components`: gaze, saccade, fixation and pursuit detection, facial and heart-rate helpers.
- `Runtime/Utils`: `BufferedUtf8Writer`, CSV helpers, `Clock`, `FileSystem`, `KeyValueStore`.
- `PipelineComponent.FindObject`/`FindObjects` wrap the Unity-version-specific find APIs. Use them instead of `FindObjectsByType`/`FindObjectOfType`.

**Editor** (`Editor/`): inspectors for `Pipeline`, `DataProvider` and `LocalFileEndpoint`, property drawers, menu items that add the prefabs to a scene, and `DefineUtility` for scripting defines.

**Tests** (`Tests/Runtime`)
- Fakes live in `Tests/Runtime/Mocks`: `MockStatement`, `FixedClock`, `CapturingLogWriter` and `InMemoryFileSystem`.
- A test that swaps a default restores it in `finally`/`TearDown`, for example `Clock.Current = null`, `FileSystem.Current = null`, `DebugLog.Writer = null` or `ScenarioContext.Reset()`.
