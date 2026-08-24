# Distances and Steps

---

## Two distances, never one

- **Loads at** — the player gets this close, the model starts loading.
- **Releases at** — the player gets this far, the model is released.

The gap between them is what stops an object from loading and releasing on every step near the boundary. A player pacing across a single threshold would otherwise trigger a load/unload cycle per frame.

Vicinity refuses a releasing distance that is not larger than the loading distance, in three places:

1. the inspector shows an error with a fix button,
2. the dashboard's **Validation** tab lists it with a fix button,
3. the engine forces a minimum margin at registration, even if a value slips through.

> [!IMPORTANT]
> This is why there is no single "distance" field anywhere in Vicinity. One number cannot be made safe.

---

## Where a distance comes from

Distances resolve in this order, first match wins:

1. the object's **own override**,
2. the **[volume](Profiles-And-Volumes)** covering it, through that volume's profile,
3. the **manager's profile**,
4. Vicinity's built-in defaults — **60 m** to load, **85 m** to release.

The inspector always states which of these applied, in words, under the values. So does the Scene view when you drag a ring.

A prefab produced by the **[drop zone](Prefabs-And-Models)** carries its own override, derived from the object's size. To hand it back to the shared settings, use **Go back to the shared distances** in its inspector — selecting many objects at once works.

---

## Quality steps

One model is the usual case. Add steps when you want a lighter model far away and a heavier one up close.

Each step covers a **band** of distance: a light model from 200 m in, a heavy one from 60 m in. The bands overlap by the hysteresis margin, so the outgoing step stays loaded until the incoming one is ready and the level never shows a hole.

> [!NOTE]
> As soon as an object has two or more steps, **the steps carry the distances** and the object's own load/release fields no longer apply. The inspector says so rather than leaving dead fields on screen.

Steps must go from closest to furthest, each distance larger than the one before. If they do not, the inspector shows an error with a **Space the steps out** button that repairs the ordering.

---

## Quality steps are not LODs

A quality step changes **which asset is in memory**. A `LODGroup` changes **which mesh is drawn** from assets already in memory.

They compose: a managed object can carry a `LODGroup` inside each of its steps. Vicinity never touches it.

---

## Objects that move

Scenery that never moves is cheap: Vicinity places it in a spatial grid once and re-checks only what the player approaches.

An object that moves during play — a platform, a vehicle — must be told so with **Moves at runtime** on its Vicinity Object. Moving objects are re-checked every evaluation, which costs more, so leave it off for anything static.

---

## Standing still costs nothing

Evaluation is skipped entirely while the viewpoint has not travelled a minimum distance. A player standing still does no work at all.

Evaluation also runs on `position + velocity × look-ahead` rather than the raw position, so an object is already loading by the time the player arrives. A teleport is detected and does not produce a nonsense prediction.

---

#### ◀ **[Prefabs and Models](Prefabs-And-Models)**  ·  Next: **[Profiles and Volumes ▶](Profiles-And-Volumes)**
