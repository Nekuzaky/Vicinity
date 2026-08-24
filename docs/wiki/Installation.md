# Installation

Vicinity is distributed as a Unity package, installed from a Git URL.

---

## From a Git URL *(recommended)*

1. In Unity, open **Window ▸ Package Manager**.
2. Click **+**, then **Install package from git URL**.
3. Paste:

```
https://github.com/Nekuzaky/Vicinity.git?path=/Packages/com.nekuzaky.vicinity
```

Open **Tools ▸ Vicinity ▸ Dashboard** to get started.

To pin a version, append a tag. The `path` parameter always comes first:

```
https://github.com/Nekuzaky/Vicinity.git?path=/Packages/com.nekuzaky.vicinity#v0.1.0
```

> [!NOTE]
> Installing from a Git URL requires Git on your machine and reachable from Unity. Package Manager reports a clear error if it is not.

---

## Requirements

| Requirement | Detail |
| :--- | :--- |
| **Unity version** | 6000.3 or newer |
| **Render pipeline** | Universal Render Pipeline |
| **Scope** | Editor tooling **and** a runtime engine — Vicinity ships in your build |

---

## Dependencies

Installed automatically with the package:

| Package | Why |
| :--- | :--- |
| `com.unity.burst` | Distance and transition evaluation |
| `com.unity.collections` | Native containers, no garbage in the loop |
| `com.unity.mathematics` | Vector maths inside the jobs |
| `com.unity.profiling.core` | Custom profiler counters |
| `com.unity.render-pipelines.universal` | URP is the only pipeline supported in v1 |

---

## Addressables is optional

Addressables is deliberately **not** a dependency, so installing Vicinity never drags it into a project that does not want it.

The Addressables support lives in its own assemblies, gated behind a `versionDefines` symbol. When the package is absent, Unity skips those assemblies entirely — before it even tries to resolve their references, so there is no error and not even a warning. Install Addressables later and the support appears on its own, with nothing to configure.

> [!IMPORTANT]
> Addressables is optional, but it is also the only thing that makes memory genuinely drop. See **[Asset Sources](Asset-Sources)** for why, and decide with your eyes open.

---

## Where your settings live

Vicinity stores nothing global. Distances and budgets live in **[profiles](Profiles-And-Volumes)** you create yourself, as ordinary assets committed with your project.

---

> [!TIP]
> Once installed, continue to **[Getting Started](Getting-Started)**.

---

#### ◀ **[Home](Home)**  ·  Next: **[Getting Started ▶](Getting-Started)**
