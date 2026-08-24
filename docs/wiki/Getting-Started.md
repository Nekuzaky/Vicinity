# Getting Started

Two ways in, depending on what you have: a library of prefabs, or a scene already built.

---

## Start from a prefab

1. Open **Tools ▸ Vicinity ▸ Dashboard**.
2. Drag a prefab — or an imported model, or a whole folder — onto the drop zone at the top.
3. A new prefab appears beside the original, named `<name> (Vicinity)`.
4. Place **that one** in your scene instead of the original.

That is the whole setup. The new prefab already knows how big the model is, how much memory it takes, how close the player must be before it loads, and how far away it is released.

> [!TIP]
> Vicinity works out the loading distance from the object's real size, so a cathedral loads from much further away than a crate — without you typing a number.

Details in **[Prefabs and Models](Prefabs-And-Models)**.

---

## Start from a scene you already have

1. Open **Tools ▸ Vicinity ▸ Dashboard**.
2. Click **Set up this scene**.

It adds a manager and a viewpoint if the scene has none, then hands every object that draws something over to Vicinity. One undo takes it all back.

If you would rather choose yourself, use **Scan Scene** to list every candidate heaviest first, tick the ones you want, and **Apply to selected**.

> [!NOTE]
> The operation is idempotent and never silently overwrites an object you configured by hand — it asks first.

---

## What you just created

| Object | What it is for |
| :--- | :--- |
| **Vicinity Manager** | Drives every managed object in the scene. One per scene. |
| **Vicinity Target** | The viewpoint distances are measured from. Put it on your player or camera rig. |
| **Vicinity Object** | On each managed object. Names the model to load and, optionally, its own distances. |

You do not create the manager or the viewpoint by hand — the dashboard adds them when they are missing.

---

## Seeing it work

Enter Play Mode with the **Live** tab open. It shows how many objects are loaded, how many are waiting, and how much memory they hold, sampled over the last 300 frames.

In the Scene view, selected objects draw two ground rings: the inner one is where they load, the outer one where they are released. Drag either ring to change the distance directly.

> [!WARNING]
> If the Live tab's memory never drops as you walk away, that is expected for a model referenced **directly**. Read **[Asset Sources](Asset-Sources)** — it is the single most important page in this manual.

---

## Trying it without your own assets

The package ships a sample. In **Package Manager ▸ Vicinity ▸ Samples**, import **Streaming Demo**, then run **Tools ▸ Vicinity ▸ Build the Streaming Demo Scene**. The menu entry only exists once the sample is imported.

---

#### ◀ **[Installation](Installation)**  ·  Next: **[Prefabs and Models ▶](Prefabs-And-Models)**
