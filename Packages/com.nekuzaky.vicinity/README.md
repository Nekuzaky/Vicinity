# Vicinity

**Vicinity decides which assets stay in memory, based on how far they are from the player.**

Distant objects are never loaded at all. They load as the player approaches, and are released as
the player walks away. Vicinity is built to be used **without writing a single line of C#** —
components, scene view handles and an editor dashboard, aimed at artists and level designers.

- Unity 6000.3 or newer · Universal Render Pipeline
- Source-available: free for noncommercial use, commercial licence via the Unity Asset Store
- Addressables supported but **not** required

---

## Table of contents

- [What Vicinity is not](#what-vicinity-is-not)
- [Installation](#installation)
- [One step, no code](#one-step-no-code)
- [The four components](#the-four-components)
- [Distances and the margin between them](#distances-and-the-margin-between-them)
- [Profiles](#profiles)
- [Where assets come from](#where-assets-come-from)
- [The dashboard](#the-dashboard)
- [Scene view](#scene-view)
- [How it works inside](#how-it-works-inside)
- [The GPU Resident Drawer](#the-gpu-resident-drawer)
- [Textures](#textures)
- [Profiling](#profiling)
- [Public API](#public-api)
- [Sample](#sample)
- [Tests](#tests)
- [Support the project](#support-the-project)
- [License](#license)

---

## What Vicinity is not

Unity already answers *"which mesh is drawn"* with `LODGroup` and Mesh LOD. Vicinity answers a
different question: *"which asset exists in memory at all"*.

| Question | Answered by |
| --- | --- |
| Which mesh is drawn? | `LODGroup` / Mesh LOD |
| Which asset exists in memory? | **Vicinity** |

An object managed by Vicinity can keep its own `LODGroup`. The two systems are independent and
never interfere. Vicinity will never generate LODs, simplify meshes, or manage LOD transitions —
that is deliberate, not missing.

Vicinity also does not stream textures. Unity's built-in Mipmap Streaming does, and the dashboard
detects and enables it for you.

---

## Installation

In the Unity Package Manager, choose **Install package from git URL** and enter:

```
https://github.com/Nekuzaky/Vicinity.git?path=/Packages/com.nekuzaky.vicinity
```

To pin a version, append a tag — the path parameter always comes first:

```
https://github.com/Nekuzaky/Vicinity.git?path=/Packages/com.nekuzaky.vicinity#v0.1.0
```

### Dependencies

Installed automatically:

| Package | Why |
| --- | --- |
| `com.unity.burst` | Distance and transition evaluation |
| `com.unity.collections` | Native containers, no garbage in the loop |
| `com.unity.mathematics` | Vector maths inside the jobs |
| `com.unity.profiling.core` | Custom profiler counters |
| `com.unity.render-pipelines.universal` | URP is the only pipeline supported in v1 |

### Addressables is optional

Addressables is deliberately **not** in `dependencies`, so installing Vicinity never drags it into
a project that does not want it.

The `Nekuzaky.Vicinity.Addressables` assembly carries a `versionDefines` entry that sets
`VICINITY_ADDRESSABLES` when the package is present, and a `defineConstraints` entry requiring that
same symbol. When Addressables is absent Unity skips the assembly entirely — before it even tries
to resolve its references, so there is no error and not even a warning. Install Addressables later
and the provider appears on its own, with nothing to configure.

Both cases are verified on every release.

---

## One step, no code

```
Tools > Vicinity > Dashboard
```

### Drag a model into the scene

In a scene that has a manager, dragging a model from the Project window into the Scene view places it
as a managed object. Nothing else to do, and no new habit for an artist to learn — the `(Vicinity)`
prefab is made on the first drop and reused after.

Anything Vicinity cannot take over is placed untouched. A notice in the Scene view says what happened,
and one undo takes it back. **Take over what I drop into the scene** in the dashboard header turns it
off, per user.

### Drop a prefab in

The dashboard opens on a drop zone. Drag a prefab **or an imported 3D model** onto it — `.fbx`,
`.obj`, `.blend`, anything Unity imports as a model — or a pile of them, or a whole folder. Each one
comes back as `<name> (Vicinity).prefab`, sitting beside the original. Place that one in your scene
instead of the original and you are done.

A model whose root carries an axis conversion keeps it, so nothing arrives lying on its side.

What Vicinity works out on its own:

| Measured | Used for |
| --- | --- |
| How big the model is | The distance it loads at, rounded to something readable |
| How much memory it takes | The reporting in the dashboard and the memory budget |
| Whether Addressables is installed | How the model is named, and so whether memory actually drops |

Dropping the same prefab again re-measures it. Distances you set by hand survive that.

> **On memory, plainly.** A prefab that points *straight at* its model does not save memory: Unity
> loads anything a scene names directly, whether or not Vicinity has shown it yet. Only a model named
> through **Addressables** or **Resources** is genuinely absent until asked for. When Addressables is
> installed, the drop zone uses it automatically. When it is not, the produced prefab still works —
> Vicinity shows and hides it correctly — but the dashboard says so in as many words instead of
> letting you believe otherwise.

### Or set up a whole scene

The **Set up this scene** button at the top of the dashboard adds a manager and a viewpoint if the
scene has none, then hands every object that draws something over to Vicinity. One undo takes it all
back. This works in place, on objects already in the scene, so it carries the same memory caveat as
a direct reference.

### When you want to choose yourself

`Tools > Vicinity > Dashboard`, then **Scan Scene** to list every candidate heaviest first, tick the
ones you want, and **Apply to selected**.

Either way the operation is idempotent, fully undoable in one step, and never silently overwrites an
object you configured by hand — it asks first.

---

## The four components

| Component | What it does | Who touches it |
| --- | --- | --- |
| **Vicinity Manager** | Drives every managed object in the scene. One per scene. | Created for you |
| **Vicinity Volume** | Gives one area of the level its own distances | Level designer |
| **Vicinity Object** | Marks an object as managed, names its detailed model | Artist |
| **Vicinity Target** | The viewpoint distances are measured from | Designer |

Every serialized field has a plain-language tooltip. Every inspector opens with one sentence
explaining what the component does. Defaults behave correctly untouched. Multi-object editing,
undo/redo and prefab mode are all supported.

When a configuration is wrong, the inspector does not merely warn — it offers a button that fixes it.

### Vicinity Object

Whatever sits in the scene is the **stand-in** — keep it cheap. The **quality steps** are the
prefabs loaded as the player comes closer.

One step is the usual case: a single detailed model, loaded at the profile's distance.

Add more steps and each one covers a band of distance — a light model from 200 m in, a heavy one
from 60 m in. Bands overlap by the hysteresis margin, so the outgoing step stays loaded until the
incoming one is ready and the level never shows a hole. As soon as an object has two or more steps,
the steps carry the distances and the profile no longer sets them.

While a model loads, the stand-in stays visible. The swap happens only once the model genuinely
exists, never speculatively.

The loaded model takes over from the stand-in completely:

- **Scale** — it inherits the transform of the object in the scene, so a stand-in scaled to 3x
  produces a detailed model at the same world size.
- **Baked lighting** — a prefab instantiated at runtime carries no valid lightmap binding of its own,
  so Vicinity copies the one baked for the stand-in. Your detailed model must share the stand-in's
  lightmap UVs for this to look right, which is the same rule Unity imposes on LOD meshes.
- **Collision** — the stand-in's colliders are disabled only when the loaded model brings colliders
  of its own, and handed back the moment it is released. A model without collision never leaves a
  hole in the level's physics.

If the detailed model is the same prefab the object was made from, loading gains nothing. The
Validation tab flags exactly that.

### Vicinity Target

Vicinity measures from a `VicinityTarget`, **not** from `Camera.main` — in a project with several
cameras `Camera.main` is a trap. Without a target Vicinity falls back to the active camera and the
dashboard tells you.

A target also carries a **look-ahead**. Vicinity evaluates from `position + velocity × look-ahead`
rather than the raw position, so loading starts before the player arrives and disk latency stays
hidden. A teleport is detected and does not produce a nonsense prediction.

### Vicinity Volume

A volume covers a box and applies its profile to the managed objects inside — a cramped interior
inside an open landscape, for instance. Overlapping volumes with the same priority and different
profiles are ambiguous; the Validation tab flags it and offers to break the tie.

---

## Distances and the margin between them

Two distances, never one:

- **Loads at** — the player gets this close, the model starts loading
- **Releases at** — the player gets this far, the model is released

The gap between them is what stops an object from loading and releasing on every step near the
boundary. A player pacing across a single threshold would otherwise trigger a load/unload cycle per
frame.

Vicinity refuses a releasing distance that is not larger than the loading distance, in three places:

1. the inspector shows an error with a fix button,
2. the dashboard's Validation tab lists it with a fix button,
3. the engine forces a minimum margin at registration, even if a value slips through.

Distances resolve in this order: **object override → covering volume's profile → manager's profile
→ built-in defaults**.

---

## Profiles

A `VicinityProfile` is a ScriptableObject grouping distances and budgets, assignable to a manager
or to a volume. Create one with `Assets > Create > Vicinity > Profile`.

Three presets ship with the sample:

| Profile | Loads at | Releases at | Made for |
| --- | --- | --- | --- |
| Interior Dense | 25 m | 38 m | Tight interiors, many small props |
| Open World | 120 m | 170 m | Landscapes with long sight lines |
| Mobile | 45 m | 65 m | Limited memory and slow storage |

Pick a preset from a dropdown rather than inventing distance values.

---

## Where assets come from

| Source | Needs |
| --- | --- |
| Direct reference | nothing |
| Resources | nothing |
| Addressables | the Addressables package |

You never pick a provider. Vicinity registers the ones your project can support and resolves each
object by where its asset comes from. Provider selection is exposed only as an advanced setting.

---

## The dashboard

`Tools > Vicinity > Dashboard`. Built in UI Toolkit, readable down to 1280 px wide.

### Setup

Checks the project configuration — URP, SRP Batcher, GPU Resident Drawer, Mipmap Streaming,
Addressables. Each line gives a state, a one-sentence explanation, and a fix button where a fix
exists.

Then **Scan Scene** and **Apply**, described above.

### Validation

Everything wrong in the scene, most severe first, each with a plain explanation and a **Fix** button
where the fix can be automated. Double-click a row to select the object.

Detected: managed objects with no model, releasing distances with no margin, missing manager,
missing viewpoint, overlapping volumes with contradictory profiles, profiles with a broken margin,
detailed models identical to their stand-in, and objects excluded from the GPU Resident Drawer.

### Live

Active in Play Mode. Counts per state, resident memory against the profile budget, a graph of the
last 300 frames, a line reporting how many managed objects are excluded from GPU instancing and
why, and CSV export to compare two sessions.

---

## Scene view

Loading and releasing distances are drawn as **ground rings**, not filled spheres — filled spheres
become unreadable as soon as there are several.

- Both radii have **draggable handles**, showing the distance in meters while you drag, with undo
  on release.
- In Play Mode the centre marker is coloured by state: not loaded, waiting, loading, loaded, failed.
- One toggle in the dashboard header hides every gizmo at once.
- Drawing is clipped to the selection and to the scene view camera distance.

---

## How it works inside

**Spatial partition.** Managed objects are sorted into a uniform grid. Evaluation runs over
**cells**, not objects: a cell that is both out of range and currently holds nothing is never
visited. Cell contents are ordered deterministically, so the same scene always produces the same
layout.

**Two Burst jobs per evaluation.** The first computes, per cell, the squared distance from the
viewpoint to the cell's box and whether that cell still holds anything. The second walks only the
relevant cells, and for each object compares its predicted distance against its two thresholds,
emitting load and release candidates.

**Deterministic despite parallelism.** Candidates are gathered by parallel writers, whose order is
arbitrary, then sorted by priority and index. The same camera path always produces the same loading
decisions, which is what makes a bug reproducible.

**Priority.** Candidates are ordered by predicted distance, penalised when the object falls outside
the camera frustum, so what the player looks at loads first.

**Budget.** A configurable number of objects may load at once. The rest stay queued and start as
slots free up.

**State machine.** `Unloaded → Queued → Loading → Resident → Unloaded`, plus `Failed` with a bounded
number of attempts. A failure logs once per object, never once per frame.

**Nothing is released mid-instantiation.** A release requested while an object is still loading does
not release anything — it is recorded, the load is cancelled, and the instance is released the
moment it exists. Releasing an asset while Unity is still instantiating it produces lost material
and prefab references; the state machine makes that impossible by construction rather than by
convention.

**No garbage in the loop.** Native containers are allocated once and reused. The per-frame path
allocates no managed object: the jobs, the comparer and the view state are all structs.

**Released models are kept aside, not destroyed.** A small pool holds recently released instances,
so a player walking back into a room reuses what was already built instead of loading it again. The
pool is bounded and counts against memory; set it to 0 to disable it.

**Evaluation runs off the main thread.** Jobs are scheduled at the end of one evaluation and
harvested at the start of the next, so the main thread never waits on workers. Decisions arrive one
interval later, which the movement look-ahead already absorbs.

**Objects that move are followed.** Scenery is measured once, at its registered position, which is
what makes the grid cheap. Tick "moves at runtime" on an object and it is re-measured every
evaluation instead, outside the grid.

**The memory ceiling is enforced, not decorative.** When loaded models exceed the budget, Vicinity
releases the ones furthest from the player until it is back under, and says so once.

**Slots are recycled.** Objects that are enabled and disabled repeatedly reuse their table slot,
so a long session does not grow the evaluation cost.

**Nothing is evaluated while the player stands still.** Beyond the time interval, Vicinity also
requires the viewpoint to have travelled a minimum distance before it looks again. A player reading
a map costs nothing.

**Frame-rate independent.** Evaluation runs on a time interval, never on a frame count. The core
takes its delta time as a parameter and knows nothing about `Time.deltaTime` or `Camera.main`,
which is what makes it testable outside Play Mode.

**Domain reload safe.** Static state is reset on entering Play Mode, so disabling domain reload
cannot carry a stale manager, viewpoint or volume into the next session.

**Versioned serialization.** Serialized data carries a version field and a migration path, so a
package update never breaks an existing scene.

---

## The GPU Resident Drawer

Unity silently excludes an object from GPU instancing if it carries a `MaterialPropertyBlock`, or
a script implementing `OnBecameVisible`, `OnBecameInvisible` or `OnWillRenderObject`.

**Vicinity uses none of them.** Visibility is tested by hand inside the Burst job, from positions it
already has. Using `OnBecameVisible` would have been the natural reflex, and it would have quietly
pulled every managed object out of your GPU batching — an invisible, undiagnosable regression.

Your own scripts might still do it, and so might your lighting. Vicinity checks the full official
list: renderers that are not Mesh Renderers, material property blocks, Light Probe Proxy Volumes,
per-instance callbacks (`OnRenderObject`, `OnWillRenderObject`, `OnBecameVisible`,
`OnBecameInvisible`), the `Disallow GPU Driven Rendering` component, and realtime global
illumination at the scene level. The Validation tab names every managed object that is excluded and
why; the Live tab shows the running count.

The GPU Resident Drawer itself is never assumed to be on. It requires Forward+ or Deferred+, the
SRP Batcher, BatchRendererGroup Variants on *Keep All*, and a graphics API with compute shaders.
The Setup tab reports whether your project qualifies and whether it is actually enabled.

---

## A trap when using Addressables

Releasing an instance does not free its asset until the bundle it belongs to is also unloaded. If
every building in your level sits in one bundle, releasing a single building frees nothing. Group
your bundles the way the player travels, not the way your project folders are organised — this is
the single most common reason a streaming system appears to save no memory at all.

## Textures

Vicinity manages models, not textures. Unity's Mipmap Streaming handles those, and the Setup tab
can enable it. Two details:

- Objects Vicinity loads stay ordinary renderers, so Unity computes the right mip for them.
- A mesh generated by script needs `Mesh.RecalculateUVDistributionMetrics()`, otherwise Unity picks
  the wrong mip and the object stays blurry up close.

---

## Profiling

A **Vicinity** module ships for the Profiler window, with four phase markers —
`Vicinity.Evaluate`, `Vicinity.Schedule`, `Vicinity.Load`, `Vicinity.Integrate` — and counters for
managed, loaded, loading, waiting and abandoned objects, plus resident memory.

A studio should be able to diagnose Vicinity without reading its source.

---

## Public API

Vicinity is `internal` by default. The supported surface is small on purpose.

```csharp
// Components
VicinityManager   // ActiveManager, Profile, Statistics, SetProfile, GetState
VicinityObject    // DetailedModel, State, LoadDistance, UnloadDistance,
                  // SetDetailedModel, SetEstimatedMemoryBytes
VicinityVolume    // Profile, Priority, WorldBounds, Contains, SetBox, FindCovering
VicinityTarget    // Position, Velocity, Priority, LookAheadSeconds, ViewCamera
VicinityProfile   // LoadDistance, UnloadDistance, MemoryBudgetMegabytes, ToSettings

// Assets
AssetKey                // FromDirectReference, FromResourcesPath, FromAddress
AssetSourceKind         // DirectReference, Resources, Addressables
IAssetProvider          // SourceKind, LoadAsync, Release
AssetProviderRegistry   // Register, Supports, Resolve, LoadAsync, Release, CreateDefault
VicinityProviders       // RegisterFactory, IsRegistered
AssetLoadException

// State
ResidencyState        // Unloaded, Queued, Loading, Resident, Unloading, Failed
ResidencyStatistics   // Managed, Unloaded, Queued, Loading, Resident, Failed, ResidentMemoryBytes
ResidencySettings     // distances, budgets and defaults
```

Reading the current state of an object:

```csharp
ResidencyState state = myVicinityObject.State;
```

Reporting what the scene holds:

```csharp
ResidencyStatistics stats = VicinityManager.ActiveManager.Statistics;
Debug.Log($"{stats.Resident} loaded, {stats.ResidentMemoryBytes} bytes");
```

Adding a provider of your own:

```csharp
VicinityProviders.RegisterFactory(AssetSourceKind.Resources, () => new MyProvider());
```

---

## Sample

Import **Streaming Demo** from the Package Manager, then
`Tools > Vicinity > Build the Streaming Demo Scene`.

It generates a field of 5,000 managed objects and a viewpoint that walks back and forth on its own.
Press Play and watch the Live tab: the loaded count and memory graph rise as the viewpoint
approaches and fall as it leaves. Press **T** to teleport to the far end — everything loaded is
released at once, which is the case that breaks naive streaming systems.

The sample also contains the three preset profiles.

---

## Tests

| Suite | Covers |
| --- | --- |
| EditMode | Grid, state machine, hysteresis, priority, budget, prediction, determinism, failure handling |
| PlayMode | Real loading, stand-in swap, release, teleport away and back, destruction mid-load |

The core has no dependency on `Time.deltaTime` or `Camera.main`, which is what allows the logic to
be tested without entering Play Mode.

---

## Support the project

Vicinity is free for noncommercial use. If it saves you time, you can support its development:

[![License](https://img.shields.io/badge/licence-PolyForm%20Noncommercial-4c7fbe)](LICENSE.md)
[![GitHub Sponsors](https://img.shields.io/badge/GitHub%20Sponsors-Nekuzaky-ea4aaa?logo=githubsponsors&logoColor=white)](https://github.com/sponsors/Nekuzaky)
[![Patreon](https://img.shields.io/badge/Patreon-Nekuzaky-f96854?logo=patreon&logoColor=white)](https://www.patreon.com/Nekuzaky)
[![Buy Me a Coffee](https://img.shields.io/badge/Buy%20Me%20a%20Coffee-Nekuzaky-ffdd00?logo=buymeacoffee&logoColor=black)](https://www.buymeacoffee.com/nekuzaky)

- **GitHub Sponsors** — https://github.com/sponsors/Nekuzaky
- **Patreon** — https://www.patreon.com/Nekuzaky
- **Buy Me a Coffee** — https://www.buymeacoffee.com/nekuzaky

Reporting a reproducible bug, or a scene where Vicinity behaves badly, helps just as much.

---

## License

Vicinity is **source-available**, not open source, under the
[PolyForm Noncommercial License 1.0.0](https://polyformproject.org/licenses/noncommercial/1.0.0).

| Use | Allowed |
| --- | --- |
| Personal projects, hobby work, study, research | **Yes, free** |
| Charities, schools, public institutions | **Yes, free** |
| Commercial projects, studios, paid games | Requires a commercial licence |

A commercial licence comes with every purchase of Vicinity on the Unity Asset Store. For a
commercial licence outside the Asset Store, contact the licensor.

The full terms are in [LICENSE.md](LICENSE.md). The summary above is a convenience, not the licence.
