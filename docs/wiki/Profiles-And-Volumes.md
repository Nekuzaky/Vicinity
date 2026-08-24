# Profiles and Volumes

---

## Profiles

A **profile** is an asset grouping distances and budgets, assignable to a manager or to a volume. Create one with **Assets ▸ Create ▸ Vicinity ▸ Profile**.

It is an ordinary asset: commit it with your project, and every teammate gets the same behaviour.

Three presets ship with the sample:

| Profile | Loads at | Releases at | Made for |
| :--- | :--- | :--- | :--- |
| **Interior Dense** | 25 m | 38 m | Tight interiors, many small props |
| **Open World** | 120 m | 170 m | Landscapes with long sight lines |
| **Mobile** | 45 m | 65 m | Limited memory and slow storage |

> [!TIP]
> Pick a preset rather than inventing distance values. The numbers above are a better starting point than a guess, and you can tune one profile instead of a thousand objects.

A profile also carries a **memory budget**. When resident models exceed it, Vicinity releases the objects furthest from the player until it fits again — rather than only reporting the overrun.

---

## Volumes

A **volume** covers a box and applies its profile to the managed objects inside it. A cramped interior inside an open landscape is the usual reason: the landscape wants 120 m, the corridor wants 25 m.

Add one with **GameObject ▸ Vicinity ▸ Volume**, then resize the box in the Scene view.

### Priority

Volumes can overlap. The one with the higher **priority** wins.

> [!WARNING]
> Overlapping volumes with the **same** priority and **different** profiles are ambiguous — there is no correct answer. The **Validation** tab flags it and offers to break the tie.

---

## Targets

Vicinity measures distances from a **Vicinity Target**, not from `Camera.main`. In a project with several cameras, `Camera.main` is a trap.

Put the target on your player, or on your camera rig. Without one, Vicinity falls back to the active camera and the dashboard tells you so.

A target carries a **look-ahead**: Vicinity evaluates from `position + velocity × look-ahead` rather than the raw position, so loading starts before the player arrives and disk latency stays hidden.

Several targets can exist — a split-screen game, or a camera that matters more than the player. Each carries a priority.

---

## How it all resolves

For any managed object, the distances used come from the first of these that applies:

1. the object's own override,
2. the covering volume's profile,
3. the manager's profile,
4. built-in defaults — 60 m and 85 m.

The inspector states which one applied, in words.

---

#### ◀ **[Distances and Steps](Distances-And-Steps)**  ·  Next: **[Asset Sources ▶](Asset-Sources)**
