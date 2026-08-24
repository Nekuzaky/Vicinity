# Asset Sources

This is the most important page in the manual. It explains when Vicinity genuinely frees memory, and when it only appears to.

---

## The three sources

| Source | Needs | Frees memory? |
| :--- | :--- | :--- |
| **Direct reference** | nothing | **No** |
| **Resources** | nothing | Yes |
| **Addressables** | the Addressables package | Yes |

You never pick a provider. Vicinity registers the ones your project can support and resolves each object by where its asset comes from. Provider selection exists only as an advanced setting.

---

## Why a direct reference cannot free memory

> [!IMPORTANT]
> A scene that names an asset **directly** makes Unity load that asset, and everything it depends on, when the scene loads. It happens before Vicinity runs, and there is nothing Vicinity can do about it.

Hiding the object does not unload the mesh. Destroying the instance does not unload the mesh. The reference is serialized in the scene, so the asset is resident for as long as the scene is.

Vicinity still does real work with a direct reference — it shows and hides the model, it keeps instancing under control, it avoids instantiating hundreds of objects at once. What it does **not** do is reduce your memory high-water mark.

This is not a limitation of Vicinity. It is how Unity's asset loading works, and it applies to every streaming system built on direct references.

---

## What actually works

**Addressables.** The asset is named by an address, not held by a reference. Nothing loads until Vicinity asks. This is the intended setup.

**Resources.** The asset is named by a path. It also works, but everything under a `Resources` folder ships in your build whether it is used or not, and Unity discourages it for that reason.

---

## Letting Vicinity do it for you

When the Addressables package is installed, the **[drop zone](Prefabs-And-Models)** hands each model to Addressables automatically — creating its settings on first use, so you never have to open the Addressables window.

When it is not installed, the drop zone falls back to a direct reference, and says so in as many words, with a button to install Addressables. Drop the same assets again afterwards and they are upgraded.

> [!TIP]
> If you only remember one thing: **install Addressables before dropping your library in.** Everything else is automatic.

---

## A trap when using Addressables

Releasing an instance does not free its asset until the **bundle** it belongs to is also unloaded. If every building in your level sits in one bundle, releasing a single building frees nothing.

Group your bundles the way the player travels, not the way your project folders are organised.

> [!WARNING]
> This is the single most common reason a streaming system appears to save no memory at all. Vicinity can be working perfectly and your memory graph stay flat, purely because of bundle layout.

---

## Checking it for yourself

Open the **Live** tab, enter Play Mode, and walk away from a managed object. Resident memory should drop as objects are released.

If it does not:

1. Check whether the object's model is a direct reference. The dashboard says so.
2. If you use Addressables, check your bundle grouping against the trap above.
3. Confirm with Unity's own Memory Profiler — Vicinity reports its own estimate, which is a guide, not ground truth.

---

#### ◀ **[Profiles and Volumes](Profiles-And-Volumes)**  ·  Next: **[Residency Graph ▶](Residency-Graph)**
