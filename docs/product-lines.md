# Product Lines

SignalWeave now ships as two desktop product lines that share the same engine and general logic:

- `Classic`
  - preserves the BasicProp-style workflow and UI direction
  - desktop project: `src/SignalWeave.Classic.Desktop`
  - release tags: `classic-vX.Y.Z`

- `Modern`
  - provides a separate UI/workflow line for the next-generation product surface
  - desktop project: `src/SignalWeave.Modern.Desktop`
  - release tags: `modern-vX.Y.Z`
  - current direction: one project-file workflow with in-window `Network` and `Control` tabs instead of separate network/settings popups and per-item load/save actions
  - backlog: `docs/modern-ui-todo.md`

## Shared code

Both desktop apps share:

- `src/SignalWeave.Core`
- `src/SignalWeave.Cli`

This keeps parsers, training logic, checkpoints, clustering, and general engine behavior in one place.

## Recommended branch usage

The product split is implemented with two desktop app projects in one repo, not with permanent product-only branches.

Recommended branch roles:

- `main`
  - integration branch for shared engine work and cross-product changes

- `classic-ui`
  - optional long-lived working branch for Classic-specific UI changes

- `modern-ui`
  - optional long-lived working branch for Modern-specific UI changes

Releases should still be driven by product tag prefixes, not by branch names alone.

## Release/versioning model

Use separate tag prefixes so each UI has its own version stream:

- `classic-v1.2.0`
- `classic-v1.2.1`
- `modern-v0.1.0`
- `modern-v0.2.0`

The GitHub release workflow resolves the product from the tag prefix and publishes only that app’s bundles.
