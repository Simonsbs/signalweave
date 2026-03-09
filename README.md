# SignalWeave

SignalWeave is a cross-platform `.NET 8` desktop and CLI alternative to BasicProp. The project is open source, runs on Windows, Linux, and macOS through Avalonia, and is built around a compatibility-first core so the original BasicProp workflows can be reproduced without locking the project to a single OS.

## Why this shape

Rebuilding every BasicProp screen and workflow in one pass would produce a brittle prototype. SignalWeave instead starts with the parts that matter for long-term parity:

- BasicProp-style text configuration parsing
- text pattern files with sequence resets for simple recurrent networks
- feed-forward and simple recurrent network execution
- training controls for learning rate, momentum, update mode, random range, and stopping rules
- saved weight snapshots
- output testing and hierarchical clustering over model activations
- a desktop shell plus a scriptable CLI

## Current parity snapshot

Implemented now:

- feed-forward and simple recurrent network definitions
- config parser with aliases for common BasicProp-style field names
- pattern parser with `reset` sequence markers
- training with online (`pattern`) and batch updates
- sum-squared-error and cross-entropy output losses
- error history suitable for plotting
- test-all and test-one execution
- weight save/load
- output and hidden-state hierarchical clustering
- desktop workbench with a BasicProp-style control panel, live network view, editor tabs, testing, clustering, and weight inspection
- dedicated desktop network-configuration dialog with feed-forward and SRN modes
- native desktop file workflows for network save/load, pattern load, and weight save/load
- utility views for weight maps, pattern/output inspection, projected 3D plotting, time-series plotting, and hidden-activation export
- BasicProp-style visual touches including weight-value legend, Hinton-like weight cells, and axis-based error plotting
- dedicated secondary windows for weights, patterns/outputs, and plot utilities from the top-level menus
- popup utility windows with BasicProp-style layer controls and axis labels
- dedicated JAR-aligned time-series plot window with `Output`, `Add plot`, and `Dismiss` controls

Still to build for full feature parity:

- exact BasicProp file-format compatibility for legacy weight files
- desktop plotting panes and matrix visualizations that mirror the original UX
- interactive weight editing
- import of original BasicProp examples if the retired artifacts are recovered

## Projects

- `src/SignalWeave.Core`: parsers, engine, clustering, sample assets
- `src/SignalWeave.Cli`: terminal workflow
- `src/SignalWeave.Desktop`: Avalonia desktop app
- `tests/SignalWeave.Core.Tests`: parser and trainer tests

## CLI examples

```bash
dotnet run --project src/SignalWeave.Cli -- summary --network samples/xor.swcfg --patterns samples/xor.pat
dotnet run --project src/SignalWeave.Cli -- train --network samples/xor.swcfg --patterns samples/xor.pat --weights weights.json --seed 42
dotnet run --project src/SignalWeave.Cli -- test-all --network samples/xor.swcfg --patterns samples/xor.pat --weights weights.json
dotnet run --project src/SignalWeave.Cli -- cluster --network samples/xor.swcfg --patterns samples/xor.pat --weights weights.json --mode outputs
```

## Desktop

```bash
dotnet run --project src/SignalWeave.Desktop
```

The desktop app ships with built-in XOR and SRN demos and now exposes a BasicProp-like workflow surface:

- top-level `Network`, `Weights`, `Patterns`, `Utilities`, and `Help` menus
- desktop dialogs for configuring networks and loading/saving SignalWeave files
- right-side training/test control panel with BasicProp defaults
- live network diagram colored by weight sign and magnitude
- console/config/pattern/weight tabs
- test-one gating that follows the original `< 24 patterns` behavior
- utility tabs for weight maps, pattern/output tables, and plots
- weight legend and error plot styling that tracks the original BasicProp layout more closely
- menu-driven pop-up windows for weights, patterns/outputs, time-series plots, and 3D plots
- hidden-activation export to CSV from the desktop app

## Samples

The `samples/` directory contains starter network and pattern files:

- `xor.swcfg` and `xor.pat`
- `echo-srn.swcfg` and `echo-srn.pat`

## Compatibility note

SignalWeave targets the BasicProp feature model described on `basicprop.wordpress.com` as of March 9, 2026. The original downloadable JAR and weight-format reference were not publicly reachable during implementation, so legacy binary-level compatibility is the remaining gap.

## Delivery docs

- `docs/basicprop-compatibility.md`: scope and compatibility basis
- `docs/basicprop-engine-notes.md`: observed BasicProp 1.3 runtime semantics
- `docs/reference-assets.md`: local BasicProp reference bundle inventory
- `docs/parity-checklist.md`: parity status by engine, UI, and utilities
- `docs/e2e-backlog.md`: milestone backlog and implementation tasks
