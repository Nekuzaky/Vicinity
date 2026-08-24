# Vicinity manual — source

These pages are the source of truth for the Vicinity manual. They are written for a git host
(GitHub alert blocks, wiki-style links between page names) and are published in two places:

| Where | How |
| :--- | :--- |
| [nekuzaky.com/docs/vicinity](https://nekuzaky.com/docs/vicinity) | Copied into `frontend/src/content/vicinity/` in the `nekuzaky.com` repository |
| This repository | Read directly on GitHub |

`Changelog.md` on the website is a copy of `Packages/com.nekuzaky.vicinity/CHANGELOG.md`, not a
file kept here.

## Publishing a change

From the `nekuzaky.com` repository:

```sh
VICINITY=../Vicinity
cp "$VICINITY"/docs/wiki/*.md frontend/src/content/vicinity/
cp "$VICINITY"/Packages/com.nekuzaky.vicinity/CHANGELOG.md frontend/src/content/vicinity/Changelog.md
```

This `README.md` is deliberately not copied, and is never rendered.

## Adding a page

Add the file here, then add an entry to the `pages` array in
`frontend/src/pages/DocsVicinity.tsx` in the `nekuzaky.com` repository. The sidebar, the routes and
the wiki-link resolution are all built from that array — a file without an entry is invisible on
the website.

Keep the footer navigation line at the bottom of each page consistent with that order.
