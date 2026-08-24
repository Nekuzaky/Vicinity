# License

Vicinity is **source-available**, not open source. It is distributed under the [PolyForm Noncommercial License 1.0.0](https://polyformproject.org/licenses/noncommercial/1.0.0/).

| You want to | Allowed |
| :--- | :--- |
| Use it in a personal, student or hobby project | Yes |
| Read, fork and modify the source | Yes |
| Share your changes, under the same terms | Yes |
| Use it in a project that makes money | **Not under this licence** |

For commercial use, a licence is available through the Unity Asset Store.

> [!NOTE]
> "Noncommercial" is about *your* use of Vicinity, not about whether your game is free. A funded studio prototyping internally is commercial use; a student shipping a free portfolio project is not.

---

## Third-party content

Vicinity ships no third-party code, fonts, artwork or audio. It builds on the Unity Editor and engine APIs and on official Unity packages ([listed under Installation](Installation)), and carries its own assembly definitions so it never leaks into your own assemblies.

---

## What Vicinity writes to your project

| Path | What it is |
| :--- | :--- |
| `Packages/com.nekuzaky.vicinity/` | The package itself, managed by Package Manager — do not edit files here. |
| `<name> (Vicinity).prefab` | Produced beside each asset you drop in. Yours: edit and commit freely. |
| Profiles and graphs you create | Ordinary assets, wherever you put them. Commit them to share settings with your team. |

When Addressables is installed, dropping an asset in also creates an Addressables entry for it, and Addressables' own settings asset on first use.

---

## Runtime

Unlike an editor-only extension, Vicinity **ships in your build**: the residency engine has to run while the player plays. The editor tooling — dashboard, graph window, gizmos, validation — is editor-only and is stripped.

---

## Support

Questions, bug reports and feature requests: the [repository](https://github.com/Nekuzaky/Vicinity), or [contact@nekuzaky.com](mailto:contact@nekuzaky.com).

---

#### ◀ **[Reference](Reference)**  ·  **[Home](Home)**
