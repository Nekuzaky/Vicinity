# Dashboard

**Tools ▸ Vicinity ▸ Dashboard.** Three tabs, and everything Vicinity does from the editor is in one of them.

---

## Setup

**The drop zone.** Drag prefabs and models in, get streaming prefabs out. Covered in **[Prefabs and Models](Prefabs-And-Models)**.

**Project configuration.** Checks URP, the SRP Batcher, the GPU Resident Drawer, Mipmap Streaming and Addressables. Each line gives a state, a one-sentence explanation of why it matters, and a fix button where a fix exists. Checks that already pass are folded away so only the ones needing attention are on screen.

**Set up this scene.** Adds a manager and a viewpoint if the scene has none, then hands every object that draws something over to Vicinity, in one undoable step.

**Scan Scene / Apply to selected.** Lists every candidate heaviest first, with what each would cost. Tick the ones you want. Objects you configured by hand are marked as such and are never overwritten without asking.

---

## Validation

Everything wrong in the scene, most severe first, each with a plain explanation and a **Fix** button where the fix can be automated. Double-click a row to select the object.

Detected:

- managed objects with no model named,
- releasing distances with no margin,
- a missing manager or viewpoint,
- overlapping volumes with contradictory profiles,
- profiles with a broken margin,
- detailed models identical to their stand-in,
- objects excluded from the GPU Resident Drawer.

> [!TIP]
> Run this before every milestone. It is faster than discovering the same problems from a profiler capture.

---

## Live

Active in Play Mode.

| Shown | Why it matters |
| :--- | :--- |
| Counts per state | Where objects actually are: waiting, loading, loaded, failed |
| Resident memory against the budget | Whether you are inside what the profile allows |
| A graph of the last 300 frames | Whether memory drops as the player moves, or never does |
| Objects excluded from GPU instancing | Rendering cost you did not intend to pay |
| CSV export | Comparing two sessions, or two builds |

> [!NOTE]
> The memory figure is Vicinity's own estimate of the models it manages. It is a guide for tuning, not a substitute for Unity's Memory Profiler.

---

## Scene view

Loading and releasing distances are drawn as **ground rings**, not filled spheres — filled spheres become unreadable as soon as there are several.

- Both radii have **draggable handles**, showing the distance in meters while you drag, with undo on release.
- In Play Mode the centre marker is coloured by state.
- One toggle in the dashboard header hides every gizmo at once.
- Drawing is clipped to the selection and to the scene view camera distance.

---

## Profiling

Vicinity ships a **Vicinity** module for Unity's Profiler window, with four phase markers — `Evaluate`, `Schedule`, `Load`, `Integrate` — and counters for managed, loaded, loading, waiting and abandoned objects, plus resident memory.

Use the module when you want to know *where* the time goes. Use the Live tab when you want to know *whether it is working*.

---

#### ◀ **[Residency Graph](Residency-Graph)**  ·  Next: **[Reference ▶](Reference)**
