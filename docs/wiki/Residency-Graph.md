# Residency Graph

A profile gives every object the same distances. A **residency graph** gives each object its own, worked out from what that object actually is — its size, its memory cost, its tag.

It is optional. Most projects never need one.

---

## When you want one

| Instead of | Say this once |
| :--- | :--- |
| Setting a distance on hundreds of props by hand | *"Load at fifteen times the object's size."* |
| Tuning heavy assets one by one | *"Anything over 20 MB loads twice as late."* |
| Special-casing a category | *"Objects tagged `Landmark` load from 400 m."* |

---

## Creating and opening one

**Assets ▸ Create ▸ Vicinity ▸ Residency Graph** creates a graph already wired to an output, producing exactly Vicinity's built-in behaviour. Edit it from there.

Double-click it, or use **Tools ▸ Vicinity ▸ Residency Graph**. Assign it to a **[profile](Profiles-And-Volumes)** for it to take effect.

> [!NOTE]
> A graph is never left in a state that says nothing. Opening one that somehow has no nodes fills it in first, so you always start from something readable.

---

## The nodes

| Menu | Node | What it gives you |
| :--- | :--- | :--- |
| **Value** | Number | A constant you type |
| **Object** | Size | How big this object is, in meters |
| **Object** | Memory | How much this object's models weigh, in MB |
| **Object** | Has Tag | 1 when the object carries the tag, 0 otherwise |
| **Maths** | Maths | Add, subtract, multiply, and so on |
| **Maths** | Keep Between | Clamps a value between a floor and a ceiling |
| **Logic** | Compare | Larger than, or smaller than |
| **Logic** | Choose | Picks between two values based on a condition |
| **Output** | Residency Output | Loading distance, releasing distance, priority |

Drag from one port to another to connect them. To add a node, right-click the canvas and choose **Create Node**, or press **Space**.

## Working on the canvas

| Right-click gives you | What it is for |
| :--- | :--- |
| **Create Group** | Boxes a set of nodes together, and moves them as one |
| **Create Sticky Note** | Leaves a note for whoever opens the graph next |

A **minimap** sits in the corner for graphs too large to see at once, and nodes copy and paste between graphs.

> [!NOTE]
> The editor is built on [NodeGraphProcessor](https://github.com/alelievr/NodeGraphProcessor) by Antoine Lelievre, included under MIT — see [License](License). Only the editing side comes from it; what runs in your game is the compiled program described below.

---

## Reading the result as you build

The toolbar carries a sample object — a size in meters, a memory figure in MB, and whether it matches the tag. Every node shows the value it would produce for that sample, live.

The status line at the bottom states what the sample object would do: where it loads, where it is released, its priority, and how many instructions the graph compiled to.

When the graph cannot compile, that line says why in plain words instead of a stack trace.

---

## Rules that cannot be built wrong

Whatever the graph computes, the releasing distance is forced beyond the loading distance before it reaches the engine. A graph cannot produce an object that flickers at a threshold.

A graph containing a loop is refused before it runs, with an explanation, rather than hanging.

> [!IMPORTANT]
> One graph can ask about **one** tag. Two `Has Tag` nodes naming different tags is refused at compile time, with both names in the message.

---

## What it costs

Nothing measurable. The graph is compiled once into a flat program of instructions, then evaluated inside the same Burst job as everything else — no reflection, no allocation, no per-object graph traversal at runtime.

---

## In the Scene view and the inspector

Gizmos and the distance readout in the inspector run the graph too, so what you see on an object is what the engine will do — not the profile's raw values.

---

#### ◀ **[Asset Sources](Asset-Sources)**  ·  Next: **[Dashboard ▶](Dashboard)**
