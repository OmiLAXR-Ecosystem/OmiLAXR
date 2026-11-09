# Changelog

## [2.2.5] - 2026-11-09

### Added
- Small interfaces that make core parts replaceable. Existing classes implement them and keep their public API, and the defaults behave as before:
  - `IStatement` is composed of `IStatementIdentity`, `IStatementProvenance`, `IDiscardable`, `IJsonSerializableStatement` and `ICsvSerializableStatement`.
  - `IEndpoint` is composed of `IStatementSink` and `ISendingLifecycle`. `IStatementTransport` covers synchronous batch sends.
  - `ISendWorker` with thread, coroutine and manual workers. Override `Endpoint.CreateSendWorker()` to choose one.
  - `IPipelineEvents`, `IObjectSource`, `IObjectFilter` (with `ObjectFilters.Apply`), `IActorProvider` and `ITicker`.
  - `ScenarioContext` with overridable scenario context providers and `Reset()`.
  - Replaceable defaults for time, file IO, persistent prefs and logging: `IClock`/`Clock`, `IFileSystem`/`FileSystem`, `IKeyValueStore`/`PlayerPrefsStore` and `ILogWriter`/`DebugLog.Writer`.
- `DataProvider.AddHook(IStatementHook)` and `DataProvider.AddSink(IStatementSink)` to add plain C# hooks and sinks next to the hook and endpoint components.

### Changed
- Statements are queued while an endpoint's sending is paused.
- `Endpoint.OnDestroy()` is now `protected virtual`.
- Updated package version metadata.

### Deprecated
- `TrackingBehaviourEvent.Bind(Action<TValue>, Action<TValue>)` and `Unbind(Action<TValue>)`. They never had an effect, because a passed delegate cannot be changed. Use `Bind(UnityEvent<TValue>, UnityAction<TValue>)` or subscribe the invoker directly.

### Fixed
- Restarting a pipeline no longer duplicates the callbacks of tracking behaviours, composers and endpoints, and no longer registers listeners, filters, data providers and extensions twice.
- `Pipeline` now recognizes tracking behaviour and extension types by their interfaces.
- An endpoint can start sending again after it was paused or stopped.
- On stop, endpoints stop their send worker before flushing the queue, so the flush no longer runs at the same time as the worker.
- Failed transfers are reported once through `OnFailedSendingStatement`. `CsvFileEndpoint` no longer reports and re-queues them a second time.
- `LocalFileEndpoint` and `CsvFileEndpoint` flush queued statements before closing their files on disable and destroy.
- `LocalFileEndpoint` keeps an identifier that is not in time format, and resolves the identifier again after its config changed.
- Endpoint configs with missing or mistyped keys keep the current values instead of throwing.
- `BufferedUtf8Writer` appends to existing files instead of overwriting their beginning, and writes lines that are larger than its buffer.
- CSV values are no longer escaped twice, and renamed headers refresh the header index cache.
- `ActorGroup.GetMembers()` returns the members before `Start()` has run.
- `CustomTrackingName` works for objects that are not `GameObject`s and without `GlobalSettings` in the scene.
- `FaceData.Equals()` compares with the passed object instead of always returning `false`.
- `PlatformInformation` handles missing prefixes and suffixes.
- `Hook.GetProvider<TS>()` finds enabled providers. Before, it only returned disabled ones, and only with `includeInactive`.
- `PipelineComponent.GetInstance<T>()` looks the instance up again after it was destroyed, e.g. after a scene change.
- `HeadUtils.NormalHeadMovement()` uses the requested axis instead of always the x axis.

---

## [2.2.4] - 2026-07-03

### Fixed
- Added Unity >6.5 support.

---

## [2.2.3] - 2026-04-22

### Changed
- Improved endpoint sending stability and throughput by refactoring multi-threaded processing in `Endpoint`.
- Improved scheduler behavior and runtime efficiency in `Scheduler` and `IntervalTicker`.
- Improved tracking performance across transform and composer internals, including updates to `TransformWatcher`, `TransformTrackingBehaviour`, and pipeline prefabs.
- Updated package version metadata.

### Fixed
- Added safeguards against invalid/null statement handling in `LocalFileEndpoint`.
- Fixed `Frustum` behavior related to issue #17.

---

## [2.2.2] - 2025-12-09

### Added
- Added `string version` to `ToDataStandardString()`, `ToJsonString()` and `ToCsvFormat()` in `IStatement`.

---

## [2.2.1] - 2025-12-04

### Added
- Added virtual overridable `IncludeInactive` property to `AutoListener<T>` to allow including inactive GameObjects in future lookups. Override with `protected override bool IncludeInactive => false;` to restrict searches to active GameObjects only.

### Fixed
- Fixed an error that occurred when CSV files of the (CSV) File Endpoint were flushed.
- Removed various compiler warnings.

---

## [2.2.0] - 2025-10-13

### Added
- **Area Detection**:
  - Neue Komponenten `Area` und `Room` zur Detektion von Bereichen mittels `Collider`, inklusive `OnEnter` und `OnLeave` Events.
  - `AreaTrackingBehaviour` implementiert zur Verwaltung von Area-bezogenen Events.

- **Schedulers**:
  - Einführung von `ScheduledTrackingBehaviour`, dessen Lebenszyklus durch auswählbare Scheduler-Komponenten (`Realtime`, `Interval`, `Timeout`) bestimmt wird.
  - Standardmäßig wird der `RealtimeScheduler` (Unity Update Cycle) verwendet.

- **Gaze Tracking (GT)**:
  - `GazeDetector`-Komponente zur effizienten Strahlverfolgung und `GazeHit`-Erkennung.
  - Abstrakte `GazeTrackingBehaviour` mit Unterstützung für Ereignisse wie `OnFixated`, `OnGazeEntered`, `OnGazeLeft`.
  - Austauschbare `FixationLogic` als `ScriptableObject`.
  - Implementierung eines `<Camera> Gaze Tracking Behaviour` für Blickerkennung über die Hauptkamera.

- **Eye Tracking (ET)**:
  - Erweiterung der Gaze Tracking-Funktionalität.
  - Abstrakte Klasse `EyeCalibrator` zur Implementierung eigener Kalibrierungen.
  - SDK-unabhängige Datenstrukturen für Augen- und Blickdaten.
  - `EyeGazeTrackingBehaviour` mit austauschbarer `Saccade`- und `Pursuit`-Logik.
  - Zusätzliche Events: `OnSaccaded`, `OnPursuit` (für links, rechts oder beide Augen).
  - Verwendet zwei `GazeDetector`-Komponenten zur Repräsentation beider Augen.
  - *Hinweis: Diese Funktion befindet sich in der Beta-Phase und wird in einem bevorstehenden Forschungsprojekt evaluiert (getestet mit Meta Quest Pro und MetaXR).*

- **Facial Tracking**:
  - Abstrakte `FacialTrackingBehaviour` zur Verarbeitung von Gesichtsdaten.
  - Detektion von Emotionen durch austauschbare `ScriptableObjects` (z. B. `Anger`, `Disgust`, `Fear`, `Happiness`, `Sadness`, `Smile`, `Surprise`).
  - *Hinweis: Proof of Concept (Alpha), bisher keine Evaluation erfolgt.*

- **Heart Rate**:
  - Erste Implementierung der Herzfrequenz-Varianz.

- **InteractableTrackingBehaviour**:
  - Neues Event: `OnPointed`.

- **UiTrackingBehaviour**:
  - Neue Events: `PressStart`, `PressEnd`, `HoverStart`, `HoverEnd`.

- Hinzugefügter `SdkProvider` zur Bereitstellung von SDK-Informationen für Datenexport.
- Neue generische Typen wie `Frustum`, `Duration`, etc.

### Changed
- Kleinere Optimierungen an mehreren Komponenten und Verhaltensweisen.

### Fixed
- [Windows] Behebung von URI-Problemen beim Einsatz von Flatten CSV.
- Behebung eines `IOException` (Sharing violation on path) bei paralleler Nutzung mehrerer OmiLAXR-Instanzen.

---

## [2.1.2] - 2025-08-25

### Fixed
- Made `GetHeartRate()` virtual to restore backwards compatibility for custom Heart Rate modules.

---

## [2.1.1] - 2025-08-17

### Added
- `StressLevelTrackingBehaviour` and `StressLevelProvider` for aggregating multiple `IStressInputProvider` sources (e.g. `HeartRateProvider`, future `FacialExpression`, or custom).
- Example implementation: `StressLevelEstimator`.
- Support for storing and restoring statements with `Composer.StoreStatement(string key, IStatement statement)` and `RestoreStatement(string key)`.
- `AsyncEndpoint` base class for using `async/await` in endpoints, with `HandleSendingAsync` and `HandleSendingBatchAsync` overrides.
- `BearerEndpoint` (based on `AsyncEndpoint`) using bearer tokens for authentication.
- Unified authentication workflow via `AuthLoader<TEndpoint, TConfig>`:
    - **WebGL:** checks remote JSON config via query (`?{filename}=https://...`), URL query parameters (`endpoint` and `token`), and `Application.streamingAssetsPath`.
    - **Non-Web:** checks command-line args, local `{filename}.json`, `Application.persistentDataPath`, and `Application.streamingAssetsPath`.
    - Includes `BearerAuthLoader` and `BasicAuthCredentialsLoader`.
- Web compatibility utilities:
    - Threads can be disabled via `useThreads = false` or `OMILAXR_THREADS_DISABLED` (default disabled on WebGL).
    - `WebEndpoint` utilities for browser integration.
    - `AsyncEndpoint` supports `UnityWebRequests`.
- `virtual ComposerGroup GetGroup() => ComposerGroup.Other` for grouping composers by enum (`Other`, `System`, `Attention`, `Movement`, `Environment`, `Ppm`).
- XML `<summary>` comments for documentation.
- Menu item `OmiLAXR / Create 'bearer.json' file` to create configuration in `StreamingAssets`.
- `Pipeline.OnQuit` event and `TrackingBehaviour.OnQuitPipeline` method.
- `OnAppQuit` hook for pipeline components; shutdown order managed by `ApplicationShutdownManager` via `ShutdownOrder` attribute.
- Endpoints configurable via `Consume(MapData config)` and retrievable via `Provide()`.

### Changed
- Asset view now refreshes automatically after using `Create '...' file` menu.
- `Description` sections in the UI are collapsible.
- License headers added to all files.

### Fixed
- `StatementPrinter` now works on WebGL.
- Added `OnReleasedAnyButton` event to `InputSystemTrackingBehaviour`.
- Corrected `Pipeline.GetAll()` return type to `Pipeline[]`.

---

## [2.1.0] - 2025-06-12

### Added
- **Utils**
  - `CsvFormat` to manage CSV structures in memory.
  - `CsvFile` for improved CSV writing.
  - `Scheduler` system with `IntervalTicker` and `TimeoutTicker`.
    - Includes `SetTimeout(Action, float)` and `SetInterval(Action, float)` for scheduling logic in tracking behaviours.
  - `InteractionEventHandler` to detect `OnPress`, `OnRelease`, `OnHover`, and `OnUnhover`.

- **Listeners**
  - Global threshold settings from `TransformWatcher` added to the main camera's `Transform Listener`.

- **Tracking Behaviours**
  - `HeartRateTrackingBehaviour` added, based on interval logic.
  - New options for `TransformTrackingBehaviour`:
    - `interval`: for interval-based triggering.
    - `detectOnChange`: flag for delta-based detection.
    - `ignore`: selectively ignore transform axes.
  - `TrackingBehaviourEvent.Bind()` now supports `Action<T>` delegates.
  - Added `Hand` information to `TeleportationTrackingBehaviour` and `InteractableTrackingBehaviour`.

- **Endpoints**
  - **Buffered batch sending** introduced:
    - `MaxBatchSize` can be overridden to control batch limits.
  - `LocalFileEndpoint` renamed to **(JSON) File Endpoint**, now writes `.jsonl` instead of `.txt`.
  - Output now supports:
    - Browsable folder path for statement output (via Unity Editor).
    - One file per composer via `oneFilePerComposer = true`.
    - File/folder naming via configurable `identifier`.
    - Custom `FormatLine(IStatement)` override for each log line.
  - Statement preview UI added for all endpoints.
  - `CsvLocalFileEndpoint` added for CSV exports:
    - Shares all features of `LocalFileEndpoint`.
    - Optionally flattens headers (`flatten = true`) for wide compatibility.
    - Added `PrintStatement` types (Default, Short, CSV, CSVFlat, JSON).
  - Make Endpoints work with Threads instead of BackgroundWorker.
  - Added `FlushQueue()`.

- **Inspector & Preview**
  - Statement previews added to endpoints.
  - Visual component tree previews added to pipelines and data providers.

### Changed
- `Composers<T>` now supports multiple `TrackingBehaviours`.
  - Use `SendStatement(ITrackingBehaviour owner, IStatement statement, bool immediate = false)` instead of `SendStatement(IStatement, bool)` if needed.

- `Mouse` and `Keyboard` composers are now disabled by default.
- Prefab setup updated:
  - Added `(JSON) File Endpoint` to default prefab.
- Improved UI for `LocalFileEndpoint`, including folder selection and data previews.
- Enhanced fallback behaviour if xAPI registry is missing or invalid.

### Fixed
- Threshold comparison logic in `TransformWatcher`.
- Composers now process multiple `TrackingBehaviours` correctly.
- Incorrect context version metadata has been corrected.
- UI components no longer trigger change events unless directly interacted with (e.g., sliders).
- `BeforeStoppedPipeline` errors resolved.
- Optimized pipeline lifecycle performance.

### Deprecated
- `ObjectlessTrackingBehaviour` is now obsolete — use `TrackingBehaviour` instead.
- `Composers<T>` usage pattern changed:
  - **If supporting multiple matching behaviours**, use  
    `SendStatement(ITrackingBehaviour statementOwner, IStatement statement)`  
    instead of the legacy method.
  - If not using multi-behaviour composers, your current implementation remains valid.

---

## [2.0.18] - 2025-06-06

### Changed
- Renamed `EventTrackingBehaviour` to `ObjectlessTrackingBehaviour` to avoid confusion with `TrackingBehaviourEvent`.
  - **Need for action:** If your project extends `EventTrackingBehaviour`, update it to use `ObjectlessTrackingBehaviour`.
- Made existing `TrackingBehaviour` classes more easily extendable by marking key methods as `protected` and `virtual`.
- Ensured full compatibility with Unity ≥2020.3.15f — the package now appears in the Unity Package Manager for Unity 2020.

---

## [2.0.13 - 2.0.17] - 2025-05-21

### Added
- `InputSystemTrackingBehaviour` and `TeleportationTrackingBehaviour` components.
- `SessionManager`, `SessionTrackingBehaviour`, and `SessionComposer` for managing actor-specific session lifecycle.
- Generalized internal structures in preparation for `OmiLAXR.UnityXR`.

### Changed
- Removed the need to manually set InputSystem to "Both".
- Reorganized the `Prefabs` folder for better structure.
- Installation order no longer affects package behavior.

### Fixed
- `LrsCredentialsLoader` no longer starts if it is disabled.

---

## [2.0.12] - 2024-12-02

### Added
- `LICENSE` file (AGPL-3.0).

### Removed
- Dependency on `com.unity.nuget.newtonsoft-json`.

---

## [2.0.11] - 2024-11-25

### Added
- `[Global Settings]` component added to the OmiLAXR prefab.
- Support for path-based tracking names via `obj.GetTrackingName()` when enabled in `[Global Settings]`.

### Changed
- `LocalEndpoint` now allows selection of storage location: `persistentDataPath`, `temporaryCachePath`, or a custom path.

### Fixed
- Fixed incorrect path for `example.credentials.json`.

### Deprecated
- None.

---

## [2.0.10] - 2024-11-11

### Fixed
- Fixed compatibility errors for Unity 2021.

### Changed
- Updated README files.

### Added
- None.

### Deprecated
- None.

---

## [2.0.9] - 2024-11-11

### Added
- `ActorDataProvider` for storing global actor data (e.g. heart rate results).
- `Detect<T>()` method in tracking behaviours uses `FindObjectsOfType<T>` and passes objects to `Found()`.
- `AutoListener<T>` base class automatically uses `Detect<T>()` — calling `StartListening()` is no longer required.
- Short `Bind()` notation for `TrackingBehaviorEvent` with default invoker: e.g. `OnClickedButton.Bind(button.onClick, this, button)`.
- `HeartRateMonitor` component for adding heart rate to context via an `ActorDataProvider`. Extend `HeartRateProvider` and override `int GetHeartRate()` for your heart rate system.
- Example `Simulator` classes for heart rate.
- `ExcludeFromTracking` component and corresponding pipeline filter to skip marked GameObjects.
- Menu item **OmiLAXR / Create credentials.json file** to auto-generate credentials in `StreamingAssets`.

### Changed
- `protected Pipeline` field renamed to `public Pipeline`.
  - Need for action: Rename usages if affected by this visibility change.
- Renamed internal field access patterns in tracking components for better support of `enabled` flag.
- Ensured compatibility with Unity 2020, 2021, 2022, 2023, and Unity 6 (6000).

### Fixed
- Tracking components now respect the `enabled` property consistently.
- `HigherComposer` now includes a `SendStatement` method.

### Deprecated
- None.

### Notes
- Need for action: Ensure you're using the latest xAPI Registry JSON files. See:  
  https://gitlab.com/learntech-rwth/xapi/-/merge_requests/43/diffs

---

## [2.0.8] - 2024-10-25

### Added
- `Instructor` component, which a pipeline uses to identify the instructor.
- `FpsMonitor` and `Registration` context components.
- `LocalEndpoint`, which writes statements to local files.
- `GameObjectStateWatcher` with `GameObjectStateWatcherListener` and `GameObjectStateTrackingBehaviour`.
- `PipelinesTrackingBehaviour` and `EventTrackingBehaviour`.
- `[ReadOnly]` attribute and a class description drawer. Pipeline components show a description in the inspector.

### Fixed
- `Select<T>()` in `TrackingBehaviour` also matches objects of exactly type `T`.
- The "resumed" system statement is no longer sent before "started".

### Removed
- The **OmiLAXR / Create new pipeline** menu item.

---

## [2.0.7] - 2024-10-14

### Added
- Custom timestamp support via `WithTimestamp(DateTime timestamp)` in `xApiStatement`.
- `Dispatch(Action action)` method to `Endpoint` for main thread dispatching.
- `SendImmediate` method to `Endpoint` for high-priority xAPI statements.
- `StatementPrinter` endpoint added to `OmiLAXR Data Provider` prefab (disabled by default).
- `UnityComponentFilter` added by default to `LearnerPipeline` prefab.
- `enabled` checkbox added to: `BasicAuthFileLoader`, `TrackingBehaviour`, and `Listener`.

### Changed
- Inspector menu paths updated for several components.
- `"System start"`, `"pause"`, `"resumed"`, and `"quit"` statements are now sent immediately.
- `Found(Object[] objects)` in `Listener` now accepts a single object instead of requiring an array.
- Need for action: `TransferStatement()` in `Endpoint` renamed to `HandleQueue()`. A method named `TransferStatement()` still exists for other purposes.
- Need for action: Capitalization of some event names — update event handler method names accordingly.
- Need for action: `GetAuthor()` must now be `public` instead of `protected`.

### Fixed
- Incorrect `"System started Game"` statement has been fixed.

### Deprecated
- Need for action: `BindedComposer` removed and replaced by `IComposer` interface.
- Need for action: `AfterComposed` now includes an additional property: `bool immediate`.

---

## [2.0.6] - 2024-10-09

### Added
- Context information: `PlatformInformation`, `ScenarioLanguage` with the `Languages` enum, and `LearningContext`.
- Team support in `ActorGroup`.

---

## [2.0.5] - 2024-10-09

### Added
- `TransferCode.Busy`.
- `Endpoint.IsSending` and `Endpoint.IsTransferring`.

### Changed
- The endpoint queue is a `ConcurrentQueue` and is no longer locked while a statement is sent.
- The credentials config file is loaded from `StreamingAssets`.
- Changed the execution order of the LRS endpoint.

### Fixed
- `KeyboardTrackingBehaviour`.

---

## [2.0.4] - 2024-10-08

### Removed
- A stale component entry from the `OmiLAXR` prefab.

---

## [2.0.3] - 2024-10-08

### Changed
- Lowered the `com.unity.textmeshpro` dependency to 3.0.6.

---

## [2.0.2] - 2024-10-05

### Added
- `BasicAuthEndpoint`.
- `BasicAuthCredentialsFileLoader.targetEndpoint`. The loader only takes the endpoint on its own GameObject if none is assigned.

### Fixed
- Editor-only code is wrapped in `#if UNITY_EDITOR`, so player builds compile.

---

## [2.0.1] - 2024-10-02

### Added
- `LearnerPipeline` and `SystemPipeline`.
- `PipelineExtension` and `DataProviderExtension` to extend pipelines and data providers from other GameObjects.
- `Composer`, `HigherComposer`, `Hook` and `Endpoint` base classes, with `TransferCode`, `LocalFallback` and `BasicAuthCredentialsFileLoader`.
- `TrackingBehaviourEvent`, `TrackingBehaviourAction`, and the `[Action]` and `[Gesture]` attributes.
- `Discard()` for statements, and author information on statements.
- `KeyboardTrackingBehaviour`, a first `HeadTrackingBehaviour` (nodding and shaking), `GameObjectsStateTrackingBehaviour`, and dropdown tracking in `UiTrackingBehaviour`.
- `SceneGameObjectsListener` and `UnityComponentsFilter`.
- Pipeline tool and statistics toolbar in the editor.
- Runtime tests for the pipeline, pipeline extensions, filters and `CustomTrackingName`.

### Fixed
- `UnityComponentsFilter`, `CustomTrackingName`, and the example scripts and prefabs.

---

## [2.0.0] - 2024-08-15

First release of OmiLAXR v2, built on the pipeline pattern.

### Added
- `Pipeline` with listeners (`SelectableObjectsListener`, `ColliderGameObjectsListener`), filters (`BlacklistFilter`) and tracking behaviours (`MouseTrackingBehaviour`, `UiTrackingBehaviour`, `GameTrackingBehaviour`, `SystemTrackingBehaviour`).
- `DataProvider` with statement composers, hooks and endpoints, and `BasicAuthCredentials`.
- `Actor` and `ActorGroup`.
- `CustomTrackingName` and `TrackingRestriction`.
