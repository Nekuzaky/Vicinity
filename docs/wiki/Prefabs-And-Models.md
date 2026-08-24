# Prefabs and Models

Two ways to hand an asset to Vicinity: drop it straight into the scene, or drop it on the dashboard.

---

## Dropping straight into the scene

In a scene that has a **Vicinity Manager**, drag a model from the Project window into the Scene view and it lands as a managed object. Nothing else to do.

Behind that, Vicinity makes the `<name> (Vicinity)` prefab the first time and reuses it afterwards, so a hundred trees dropped one by one all share the same one.

| Dragged in | What lands |
| :--- | :--- |
| A model or prefab, in a Vicinity scene | A managed object |
| Something that draws nothing | Placed as it is, untouched |
| A prefab Vicinity already manages | Placed as it is — it is already done |
| Anything, in a scene with no manager | Placed as it is |

A short notice appears in the Scene view saying what happened. Undo takes it back in one step.

> [!TIP]
> This is what makes Vicinity invisible to an artist: they keep dragging models into the level exactly as before. Turn it off with **Take over what I drop into the scene** in the dashboard header — it is a per-user setting, so it never changes what a teammate sees.

> [!NOTE]
> The manager is the signal. A scene without one is not a streaming scene, so Vicinity stays out of the way entirely.

---

## Dropping on the dashboard

The drop zone at the top of the dashboard converts assets without placing them — the right tool for preparing a library in bulk, before anyone opens a level.

---

## What you can drop

| Dropped | Result |
| :--- | :--- |
| A prefab | One `<name> (Vicinity)` prefab |
| An imported 3D model (`.fbx`, `.obj`, `.blend`, …) | One `<name> (Vicinity)` prefab |
| Several at once | One each |
| A folder | One for every prefab and model inside it |

Anything Unity imports with a GameObject as its main asset is accepted. Prefabs Vicinity itself produced are skipped, so sweeping a folder twice does not nest them.

> [!TIP]
> Prefer not to drag? Select assets in the Project window and use the **Take over the selected…** button that appears under the zone.

---

## What it works out on its own

| Measured | Used for |
| :--- | :--- |
| How big the model is | The distance it loads at, rounded to a readable number |
| How much memory it takes | Reporting in the dashboard, and the memory budget |
| Whether Addressables is installed | How the model is named — and so whether memory actually drops |

The loading distance grows with the object's real size and is clamped at both ends, so a pebble never loads at arm's length and a mountain never asks for the whole world. The releasing distance is always pushed out beyond it, which is what stops an object from flickering at a threshold.

---

## What comes out

A new prefab, beside the original, carrying a **[Vicinity Object](Reference#vicinity-object)** and nothing else. It draws nothing of its own: the model appears only once the player is close enough.

When you convert from the dashboard, place that prefab in your scene **instead of** the original. Dropping into the Scene view does this for you. Either way, keep the original where it is — the new one points at it.

> [!NOTE]
> Because the produced prefab draws nothing, it remembers how big the model it stands for is. Rules that ask about size in a **[Residency Graph](Residency-Graph)** therefore still judge it correctly.

A model whose root carries an axis conversion keeps it, so nothing arrives lying on its side.

---

## Dropping the same asset again

Re-dropping refreshes the measurements — useful after you rework a model and its memory cost changes.

Distances you set by hand are **kept**. Only the measurements are redone. If you never touched the distances, they are recomputed from the new size.

---

## Reading the result

Each converted asset gets a row under the drop zone: its size, how big it is across, the distances it was given, and how its model is named. **Show in Project** selects it.

If a row says the model is *pointed at directly*, a warning appears above the list explaining that memory will not drop, with a button to install Addressables. This is not a failure — the prefab works — but it is worth understanding before you ship. See **[Asset Sources](Asset-Sources)**.

---

## When an asset is refused

| Reason | What to do |
| :--- | :--- |
| It lives in a scene | Drop the prefab or model from the Project window instead |
| It is already managed by Vicinity | Nothing — it is already done |
| It draws nothing | Nothing to save; leave it as it is |

---

#### ◀ **[Getting Started](Getting-Started)**  ·  Next: **[Distances and Steps ▶](Distances-And-Steps)**
