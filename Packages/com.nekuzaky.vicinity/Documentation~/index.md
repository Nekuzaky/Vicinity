# Vicinity

Vicinity decides **which assets stay in memory**, based on how far they are from the player.

## The one distinction that matters

Unity already answers "which mesh is drawn" with `LODGroup` and Mesh LOD. Vicinity answers a
different question: "which asset exists in memory at all". An object can have both. They never
interfere, and Vicinity will never generate LODs, simplify meshes, or manage LOD transitions.

## Getting started without writing code

1. `Tools > Vicinity > Dashboard`
2. **Scan Scene** — lists everything Vicinity could take over, heaviest first
3. **Apply** — adds the components, creates a manager and a viewpoint if the scene has none

That is the whole setup. Everything below is optional tuning.

## The four components

| Component | What it does | Who touches it |
| --- | --- | --- |
| Vicinity Manager | Drives the scene. One per scene. | Created for you |
| Vicinity Volume | Gives one area its own distances | Level designer |
| Vicinity Object | Marks an object as managed | Artist |
| Vicinity Target | The point distances are measured from | Designer |

### Vicinity Object

An object can have one quality step or several. With several, each step covers a band of distance and
the bands overlap by the hysteresis margin, so the outgoing step stays loaded until the incoming one
is ready. With two or more steps, the steps carry the distances and the profile no longer sets them.

Whatever sits in the scene is the **stand-in** — keep it cheap. The **detailed model** is the
prefab loaded when the player comes close. While it loads, the stand-in stays visible, so there is
never a hole in the level. The swap happens only once the model is genuinely ready.

If the detailed model is the same prefab the object was made from, loading gains nothing. The
Validation tab flags that.

### Distances and the margin between them

Two distances, never one:

- **Loads at** — the player gets this close, the model starts loading
- **Releases at** — the player gets this far, the model is released

The gap between them is what stops an object from loading and releasing on every step near the
boundary. Vicinity refuses a releasing distance that is not larger than the loading distance: the
inspector flags it, the dashboard offers a fix, and the engine forces a minimum margin at runtime
even if a value slips through.

### Volumes

A volume covers a box of the level and applies its profile to the managed objects inside. Use it
when one area needs different distances from the rest — a cramped interior inside an open
landscape. Overlapping volumes with the same priority and different profiles are ambiguous; the
Validation tab flags that too.

### Targets

Vicinity measures from a `VicinityTarget`, not from `Camera.main`. In a project with several
cameras `Camera.main` is a trap. If no target exists, Vicinity falls back to the active camera and
the dashboard says so.

A target also carries a **look-ahead**: Vicinity evaluates from `position + velocity × look-ahead`
rather than the raw position, so loading starts before the player arrives. A teleport is detected
and does not produce a nonsense prediction.

## Profiles

A `VicinityProfile` groups distances and budgets. Three are shipped with the Streaming Demo sample:
`Interior Dense`, `Open World`, `Mobile`. Assign one to the manager or to a volume rather than
inventing numbers per object.

## Asset sources

| Source | Needs |
| --- | --- |
| Direct reference | nothing |
| Resources | nothing |
| Addressables | the Addressables package |

You never pick a provider. Vicinity registers the ones your project can support and resolves each
object by where its asset comes from. Addressables is an optional dependency: the assembly that
supports it is not compiled at all when the package is absent, so installing Vicinity never drags
Addressables into a project that does not want it.

## The GPU Resident Drawer

Unity silently excludes an object from GPU instancing if it carries a `MaterialPropertyBlock`, or
a script implementing `OnBecameVisible`, `OnBecameInvisible` or `OnWillRenderObject`.

Vicinity uses none of these. Visibility is tested by hand inside a Burst job, from positions it
already has. Objects managed by Vicinity stay eligible.

Your own scripts might not, and neither might your lighting. Vicinity checks Unity's full exclusion
list: renderers that are not Mesh Renderers, material property blocks, Light Probe Proxy Volumes,
the four per-instance render callbacks, the `Disallow GPU Driven Rendering` component, and realtime
global illumination. The Validation tab names every excluded object and why; the Live tab shows the
running count.

## Profiling

Vicinity ships a Profiler module (`Vicinity` in the Profiler's module list) and marks four phases:
`Vicinity.Evaluate`, `Vicinity.Schedule`, `Vicinity.Load`, `Vicinity.Integrate`. Counters report
managed, loaded, loading, waiting and abandoned objects, plus resident memory.

The evaluation loop allocates nothing once running. Distances and transitions are computed in a
Burst job over native arrays; cells that are both far away and hold nothing are never visited,
which is what keeps a 50,000 object scene affordable.

## Textures

Vicinity does not stream textures. Unity's Mipmap Streaming does, and the dashboard detects and
enables it. Two details matter:

- Objects Vicinity loads stay ordinary renderers, so Unity computes the right mip for them.
- A mesh generated by script needs `Mesh.RecalculateUVDistributionMetrics()`, otherwise Unity picks
  the wrong mip and the object stays blurry up close.

## Determinism

The same camera path produces the same loading decisions. Candidate lists are sorted by priority
then by index, so parallel evaluation never changes the outcome. Nothing in the engine depends on a
frame count; evaluation happens on a time interval and the core takes its delta time as a
parameter, which is what makes it testable outside Play Mode.
