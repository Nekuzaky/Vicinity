# Vicinity

> **Unity package that decides which 3D assets stay in memory, based on how far they are from the player — with no code to write.**

Distant objects are never loaded at all. They load as the player approaches, and are released as the player walks away. Everything is done through components, scene-view handles and an editor dashboard, so **artists and level designers use it without writing a single line of C#**.

> [!TIP]
> New here? Go straight to **[Installation](Installation)**, then **[Getting Started](Getting-Started)** — drop one prefab and you have a streaming object.

---

## Is this for you?

Vicinity solves exactly one problem: **your level holds more asset than your memory budget allows.**

It is worth adopting when:

- you are building an open world, a large level, or anything with long sight lines,
- you ship to a console or a phone with a memory ceiling you actually hit,
- your artists are being asked to downgrade source assets to fit the budget.

It is **not** worth adopting when:

- your scenes already fit comfortably in memory — Vicinity then costs a step and buys nothing,
- your problem is frame rate rather than memory. That is `LODGroup`, culling and draw calls, not this.

> [!IMPORTANT]
> Vicinity does not make anything look better. It makes it *affordable to keep things looking good further away*, by not paying for what the player cannot see yet.

### What it changes for an artist

The usual conversation is *"your 4K rock blows the budget, halve the textures."* That rock costs the same memory at 800 m as at 3 m, which is what forces the downgrade in the first place.

With Vicinity it is only in memory when someone is close enough to look at it. Same budget, far more detail across the level. On top of that, an artist working in a Vicinity scene:

- drags a model into the scene and it streams — no ticket, no waiting on a programmer,
- sees the distances as two rings on the ground, draggable, in meters,
- cannot set them wrongly: the margin that stops an object flickering is enforced by the engine, and anything invalid is flagged with a button that repairs it.

There is no new habit to learn: in a Vicinity scene, dragging a model in from the Project window already gives a managed object. See **[Prefabs and Models](Prefabs-And-Models)**.

---

## Documentation

| Page | What you'll find |
| :--- | :--- |
| **[Installation](Installation)** | Add Vicinity from a Git URL, and what it depends on. |
| **[Getting Started](Getting-Started)** | Your first streaming object, by dragging a prefab. |
| **[Prefabs and Models](Prefabs-And-Models)** | The drop zone: what it measures, what it produces, folders in bulk. |
| **[Distances and Steps](Distances-And-Steps)** | The two distances, the margin between them, and quality steps. |
| **[Profiles and Volumes](Profiles-And-Volumes)** | Reusable settings, and giving one area of the level its own. |
| **[Asset Sources](Asset-Sources)** | Direct, Resources, Addressables — and which of them actually frees memory. |
| **[Residency Graph](Residency-Graph)** | Rules per object, built as a node graph instead of numbers. |
| **[Dashboard](Dashboard)** | Setup, Validation and Live, tab by tab. |
| **[Reference](Reference)** | Components, menus, public API, requirements, support. |

---

## What Vicinity is not

Unity already answers *"which mesh is drawn"* with `LODGroup` and Mesh LOD. **Both keep every level resident in memory** — the ultra-detailed rock 800 m away costs the same RAM as the one at your feet.

Vicinity answers the other question: *"which asset exists in memory at all"*.

| Question | Answered by |
| :--- | :--- |
| Which mesh is drawn? | `LODGroup` / Mesh LOD |
| Which asset exists in memory? | **Vicinity** |

An object managed by Vicinity can keep its own `LODGroup`. The two systems are independent and never interfere.

> [!NOTE]
> Vicinity will never generate LODs, simplify meshes, or manage LOD transitions. That is deliberate, not missing. It also does not stream textures — Unity's built-in Mipmap Streaming does, and the dashboard detects and enables it for you.

---

## Why Vicinity?

- **No code required** — components, gizmos and a dashboard. A public API exists if you want it.
- **Drop a prefab in** — it comes back measured, configured, and ready to place.
- **Two distances, never one** — a forced margin means an object cannot flicker at a threshold.
- **Burst-compiled** — distances and transitions are evaluated in jobs over native arrays, with no allocation in the loop.
- **Honest about memory** — when a setup cannot actually free memory, the dashboard says so instead of letting you believe otherwise.
- **Addressables optional** — supported, never required, and never dragged into your project.

---

## In short

> [!NOTE]
> - Unity 6000.3 or newer, Universal Render Pipeline.
> - Editor tooling plus a runtime engine — unlike a pure editor extension, this one ships in your build.
> - Free for noncommercial use ([License](License)).

---

#### Next: **[Installation ▶](Installation)**
