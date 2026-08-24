# Streaming Demo

A field of 5,000 managed objects, built for one purpose: watching memory drop as you walk away.

## Run it

1. `Window > Vicinity > Build the Streaming Demo Scene`
2. Press Play.
3. `Window > Vicinity > Dashboard`, then the **Live** tab.

The viewpoint walks back and forth on its own. Watch the *Loaded* count and the memory graph rise
as it approaches the field and fall as it leaves.

Press **T** during play to teleport to the far end. Everything currently loaded is released at once,
which is the case that breaks naive streaming systems.

## Profiles

Three ready-made profiles sit in `Profiles/`:

| Profile | Loads at | Releases at | Made for |
| --- | --- | --- | --- |
| Interior Dense | 25 m | 38 m | Tight interiors, many small props |
| Open World | 120 m | 170 m | Landscapes with long sight lines |
| Mobile | 45 m | 65 m | Limited memory and slow storage |

Assign one to the manager, or to a volume covering part of the level.

## What the demo does not show

Vicinity decides what is **in memory**. It does not decide which mesh is **drawn** — that stays the
job of `LODGroup` and Mesh LOD, and the two work together without interfering.
