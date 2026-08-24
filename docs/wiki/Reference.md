# Reference

---

## Menus

| Menu | Does |
| :--- | :--- |
| **Tools ▸ Vicinity ▸ Dashboard** | Opens the dashboard |
| **Tools ▸ Vicinity ▸ Residency Graph** | Opens the last graph, or offers to create one |
| **Assets ▸ Create ▸ Vicinity ▸ Profile** | Creates a profile asset |
| **Assets ▸ Create ▸ Vicinity ▸ Residency Graph** | Creates a graph, already wired |
| **GameObject ▸ Vicinity ▸ Volume** | Adds a volume to the scene |
| **Tools ▸ Vicinity ▸ Build the Streaming Demo Scene** | Only after importing the sample |

---

## Components

### Vicinity Manager

Drives every managed object in the scene. One per scene, created for you.

Holds the scene's profile, and reports what the scene is doing through `Statistics`.

### Vicinity Object

Marks an object as managed, and names the model to load.

Whatever sits in the scene is the **stand-in** — keep it cheap. The **quality steps** are the prefabs loaded as the player comes closer.

The loaded model takes over from the stand-in completely:

- **Scale** — it inherits the transform of the object in the scene, so a stand-in scaled to 3× produces a detailed model at the same world size.
- **Baked lighting** — a prefab instantiated at runtime carries no valid lightmap binding of its own, so Vicinity copies the one baked for the stand-in. Your detailed model must share the stand-in's lightmap UVs for this to look right, which is the same rule Unity imposes on LOD meshes.
- **Colliders** — the stand-in's colliders step aside only when the loaded model brings its own, and are handed back on release.

While a model loads, the stand-in stays visible. The swap happens only once the model genuinely exists, never speculatively.

### Vicinity Volume

Covers a box and applies its profile to the managed objects inside. See **[Profiles and Volumes](Profiles-And-Volumes)**.

### Vicinity Target

The viewpoint distances are measured from. Carries a look-ahead and a priority. See **[Profiles and Volumes](Profiles-And-Volumes)**.

---

## Object states

| State | Meaning |
| :--- | :--- |
| `Unloaded` | Nothing in memory |
| `Queued` | Waiting its turn to load |
| `Loading` | Being loaded and instantiated |
| `Resident` | In memory and visible |
| `Unloading` | Being released |
| `Failed` | Gave up after a bounded number of attempts, with one log line |

---

## The GPU Resident Drawer

Vicinity tests visibility by hand inside its Burst job. It uses no `MaterialPropertyBlock` and no per-instance render callbacks, so the objects it manages stay eligible for the GPU Resident Drawer.

The **Validation** tab runs Unity's full exclusion list against your scene: renderers that are not Mesh Renderers, material property blocks, Light Probe Proxy Volumes, `OnRenderObject`, `OnWillRenderObject`, `OnBecameVisible`, `OnBecameInvisible`, the *Disallow GPU Driven Rendering* component, and realtime global illumination at the scene level.

---

## Public API

Vicinity is `internal` by default. The supported surface is small on purpose.

```csharp
// Components
VicinityManager   // ActiveManager, Profile, Statistics, SetProfile, GetState
VicinityObject    // DetailedModel, State, LoadDistance, UnloadDistance,
                  // SetDetailedModel, SetEstimatedMemoryBytes
VicinityVolume    // Profile, Priority, WorldBounds, Contains, SetBox, FindCovering
VicinityTarget    // Position, Velocity, Priority, LookAheadSeconds, ViewCamera
VicinityProfile   // LoadDistance, UnloadDistance, MemoryBudgetMegabytes, ToSettings

// Assets
AssetKey                // FromDirectReference, FromResourcesPath, FromAddress
AssetSourceKind         // DirectReference, Resources, Addressables
IAssetProvider          // SourceKind, LoadAsync, Release
AssetProviderRegistry   // Register, Supports, Resolve, LoadAsync, Release, CreateDefault
VicinityProviders       // RegisterFactory, IsRegistered
AssetLoadException

// State
ResidencyState        // Unloaded, Queued, Loading, Resident, Unloading, Failed
ResidencyStatistics   // Managed, Unloaded, Queued, Loading, Resident, Failed, ResidentMemoryBytes
ResidencySettings     // distances, budgets and defaults
```

Reading the current state of an object:

```csharp
ResidencyState state = myVicinityObject.State;
```

Reporting what the scene holds:

```csharp
ResidencyStatistics stats = VicinityManager.ActiveManager.Statistics;
Debug.Log($"{stats.Resident} loaded, {stats.ResidentMemoryBytes} bytes");
```

Adding a provider of your own:

```csharp
VicinityProviders.RegisterFactory(AssetSourceKind.Resources, () => new MyProvider());
```

---

## Defaults

| Setting | Default |
| :--- | :--- |
| Loads at | 60 m |
| Releases at | 85 m |
| Evaluation interval | 0.1 s |
| Simultaneous loads | 6 |
| Look-ahead | 1 s |
| Load attempts before giving up | 3 |

---

## Support

If Vicinity saved you time, [Patreon](https://www.patreon.com/Nekuzaky) and [Buy Me a Coffee](https://www.buymeacoffee.com/nekuzaky) both help. Issues and questions go to the [repository](https://github.com/Nekuzaky/Vicinity).

---

#### ◀ **[Dashboard](Dashboard)**  ·  Next: **[License ▶](License)**
