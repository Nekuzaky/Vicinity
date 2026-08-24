# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - Unreleased

First release. Vicinity decides which assets stay in memory based on distance to the player, and
leaves the question of which mesh is drawn to `LODGroup` and Mesh LOD.

### Added

**Setting a scene up without writing code**

- `Window > Vicinity > Dashboard` with three tabs.
- **Setup** — checks URP, the SRP Batcher, the GPU Resident Drawer, Mipmap Streaming and
  Addressables, each with a one-line explanation and a fix button where a fix exists. Scans the
  scene, lists candidates heaviest first, and equips the selected ones in one undoable step.
- **Validation** — lists what is wrong in the scene with a plain explanation and a fix button:
  managed objects without a model, releasing distances that leave no margin, missing manager or
  viewpoint, overlapping volumes with contradictory profiles, and objects excluded from the GPU
  Resident Drawer. Double-clicking a row selects the object.
- **Live** — per-state counts, resident memory against the profile budget, a graph over the last
  300 frames, the count of objects excluded from GPU instancing, and CSV export.
- Scene view gizmos drawn as ground rings rather than filled spheres, coloured by state in Play
  Mode, with draggable handles showing the distance in meters while dragging. A single toggle in
  the dashboard hides all of them.

**Components**

- `VicinityManager`, `VicinityVolume`, `VicinityObject`, `VicinityTarget`, each with a plain
  language help box, tooltips on every field, defaults that behave correctly untouched, and
  inspector validation that offers a fix rather than a passive warning.
- `VicinityProfile`, a reusable set of distances and budgets, assignable to a manager or to a
  volume. Three presets ship with the sample: `Interior Dense`, `Open World`, `Mobile`.

**Memory behaviour**

- A bounded pool keeps released instances aside for reuse instead of destroying them.
- The memory ceiling now releases the objects furthest from the player when it is reached, instead
  of only being reported.
- Entry slots are recycled when objects are unregistered, so churn no longer grows the table.
- Load priority takes the size of the model into account: at equal distance, the cheap one first.

**Handing over from the stand-in**

- The loaded model inherits the scene transform of the stand-in, scale included.
- Baked lighting is carried over: a runtime instance has no valid lightmap binding, so the one baked
  for the stand-in is copied onto it.
- The stand-in's colliders step aside only when the loaded model brings its own, and are handed back
  on release.

**Streaming engine**

- Uniform spatial grid evaluated per cell. Cells that are both out of range and hold nothing are
  never visited, which is what makes a large scene affordable.
- Distances, hysteresis and visibility evaluated in Burst jobs over native arrays. No allocation in
  the evaluation loop once running.
- Explicit state machine per quality step: `Unloaded → Queued → Loading → Resident → Unloaded`, plus
  `Failed` with a bounded number of attempts and a single log line per object.
- Several quality steps per object. Each step covers a band of distance, and the bands overlap by the
  hysteresis margin so the outgoing step stays loaded until the incoming one is ready. An object with
  a single step behaves exactly as before and takes its distances from the profile.
- Evaluation is skipped entirely while the viewpoint has not travelled a configurable minimum
  distance, so a player standing still costs nothing.
- Two independent distances with a forced margin. A releasing distance below the loading distance
  is rejected in the inspector, fixable from the dashboard, and corrected at registration.
- Priority queue ordered by predicted distance and visibility, with a configurable number of
  simultaneous loads.
- Evaluation runs on `position + velocity × look-ahead` rather than the raw position, with teleport
  detection so a jump does not produce a nonsense prediction.
- The lightweight stand-in stays visible until the detailed model is genuinely loaded; the swap
  never happens speculatively.
- An asset is never released while its instantiation is still running. A release requested
  mid-load is recorded and applied the moment the instance exists.
- Streaming measured from `VicinityTarget`, falling back to the active camera. `Camera.main` is
  never used.
- Visibility tested by hand inside the Burst job. Vicinity uses no `MaterialPropertyBlock` and no
  per-instance render callbacks, so the objects it manages stay eligible for the GPU Resident Drawer.
- Full GPU Resident Drawer eligibility check against Unity's official exclusion list: renderers that
  are not Mesh Renderers, material property blocks, Light Probe Proxy Volumes, `OnRenderObject`,
  `OnWillRenderObject`, `OnBecameVisible`, `OnBecameInvisible`, the `Disallow GPU Driven Rendering`
  component, and realtime global illumination at the scene level.

**Asset sources**

- `IAssetProvider` with `DirectReferenceProvider` and `ResourcesProvider`, needing no extra
  package, and `AddressablesProvider` compiled only when Addressables is installed.
- Providers are resolved automatically from the asset source. A project never picks one by hand.

**Profiling**

- A `Vicinity` Profiler module, four phase markers (`Evaluate`, `Schedule`, `Load`, `Integrate`)
  and counters for managed, loaded, loading, waiting and abandoned objects plus resident memory.

**Package**

- Optional Addressables support through a `versionDefines` entry that sets `VICINITY_ADDRESSABLES`
  and a matching `defineConstraints` entry on the same assembly. Verified in both cases: with the
  package absent the assembly is skipped silently, with it present the assembly is built.
- Serialized data carries a version field and a migration path, so a package update never breaks
  an existing scene.
- Static state is reset on entering Play Mode, so a disabled domain reload cannot carry a stale
  manager, viewpoint or volume into the next session.

[0.1.0]: https://github.com/Nekuzaky/Vicinity/releases/tag/v0.1.0
