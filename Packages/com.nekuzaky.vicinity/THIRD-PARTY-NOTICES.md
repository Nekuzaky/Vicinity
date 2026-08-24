# Third-party notices

Vicinity includes the following third-party component. Its licence is reproduced in full at the path
given below, and travels with every copy of this package.

---

## NodeGraphProcessor

- **Author:** Antoine Lelievre ([alelievr](https://github.com/alelievr))
- **Source:** https://github.com/alelievr/NodeGraphProcessor
- **Version:** 1.3.1
- **Licence:** MIT — `ThirdParty/NodeGraphProcessor/LICENSE.md`
- **Included at:** `ThirdParty/NodeGraphProcessor/`

Provides the node graph editor behind Vicinity's **[Residency Graph](https://nekuzaky.com/docs/vicinity/residency-graph)**:
the canvas, the ports, the node creation menu, groups, sticky notes and the minimap.

### What was changed

The source is included as published, with two mechanical edits:

| Change | Why |
| :--- | :--- |
| Namespace `GraphProcessor` renamed to `Nekuzaky.Vicinity.GraphProcessor` | So a project that also installs NodeGraphProcessor from OpenUPM does not collide with the copy inside Vicinity |
| Assemblies renamed to `Nekuzaky.Vicinity.NodeGraphProcessor[.Editor]`, and the matching `InternalsVisibleTo` updated | Same reason; duplicate assembly names break compilation |

No behaviour was altered. Vicinity does **not** use NodeGraphProcessor's runtime graph traversal: a
residency graph is compiled once into a flat instruction program that runs inside a Burst job, so no
node is visited while the game plays.
