# Vicinity

**Vicinity decides which assets stay in memory, based on how far they are from the player.**

Distant objects are never loaded at all. They load as the player approaches, and are released as the
player walks away. Built to be used **without writing a single line of C#** — components, scene view
handles, and an editor dashboard, aimed at artists and level designers.

[![License](https://img.shields.io/badge/licence-PolyForm%20Noncommercial-4c7fbe)](LICENSE.md)
[![Unity](https://img.shields.io/badge/Unity-6000.3%2B-black?logo=unity)](https://unity.com)
[![Pipeline](https://img.shields.io/badge/pipeline-URP-1a7f9c)](https://docs.unity3d.com/Manual/urp/urp-introduction.html)

---

## What it is not

Unity already answers *"which mesh is drawn"* with `LODGroup` and Mesh LOD. Both keep **every** level
resident in memory — the ultra-detailed rock 800 m away costs the same RAM as the one at your feet.

Vicinity answers the other question: *"which asset exists in memory at all"*.

| Question | Answered by |
| --- | --- |
| Which mesh is drawn? | `LODGroup` / Mesh LOD |
| Which asset exists in memory? | **Vicinity** |

The two are independent and never interfere. Vicinity will never generate LODs or simplify meshes.

## Install

Unity Package Manager → **Install package from git URL**:

```
https://github.com/Nekuzaky/Vicinity.git?path=/Packages/com.nekuzaky.vicinity
```

## Use it

```
Tools > Vicinity > Set Up This Scene
```

That is the whole setup. One undo takes it all back.

## Read more

The full documentation lives with the package:

- **[Package README](Packages/com.nekuzaky.vicinity/README.md)** — components, distances, dashboard,
  GPU Resident Drawer, profiling, public API
- **[Manual](Packages/com.nekuzaky.vicinity/Documentation~/index.md)** — how it works inside
- **[Changelog](Packages/com.nekuzaky.vicinity/CHANGELOG.md)**

## Repository layout

This repository is both the development project and the distribution source.

```
Packages/com.nekuzaky.vicinity/   the package itself
Assets/                            the Unity project used to develop and test it
```

## Support the project

[![GitHub Sponsors](https://img.shields.io/badge/GitHub%20Sponsors-Nekuzaky-ea4aaa?logo=githubsponsors&logoColor=white)](https://github.com/sponsors/Nekuzaky)
[![Patreon](https://img.shields.io/badge/Patreon-Nekuzaky-f96854?logo=patreon&logoColor=white)](https://www.patreon.com/Nekuzaky)
[![Buy Me a Coffee](https://img.shields.io/badge/Buy%20Me%20a%20Coffee-Nekuzaky-ffdd00?logo=buymeacoffee&logoColor=black)](https://www.buymeacoffee.com/nekuzaky)

## License

Source-available, **not** open source, under the
[PolyForm Noncommercial License 1.0.0](https://polyformproject.org/licenses/noncommercial/1.0.0).

Free for personal projects, study, research, charities and public institutions. Commercial use
requires a licence, which comes with every purchase on the Unity Asset Store.

Full terms in [LICENSE.md](LICENSE.md).
