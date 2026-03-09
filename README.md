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
- dedicated desktop network-configuration dialog with feed-forward and SRN modes, including 2-layer, 3-layer, and 4-layer feed-forward paths
- native desktop file workflows for network save/load, pattern load, and weight save/load
- utility views for weight maps, pattern/output inspection, projected 3D plotting, time-series plotting, and hidden-activation export
- BasicProp-style visual touches including weight-value legend, Hinton-like weight cells, and axis-based error plotting
- dedicated secondary windows for weights, patterns/outputs, and plot utilities from the top-level menus
- popup utility windows with BasicProp-style layer controls and axis labels
- dedicated JAR-aligned time-series plot window with `Output`, `Add plot`, and `Dismiss` controls
- dedicated JAR-aligned `Plot Setup` window for 3D/surface plotting with `X`, `Y`, `Z`, `Show Plot`, and `Dismiss`
- dedicated JAR-aligned `PatternPlot` window with per-pattern selector and stacked `Outputs` / `Targets` / `Inputs` charts
- checked-in BasicProp golden fixtures for 2-layer, 3-layer, 3-layer batch, 3-layer cross-entropy, 3-layer momentum, stop-rule, and 4-layer feed-forward training parity
- checked-in BasicProp SRN fixtures for forward outputs, sequential-training weights, and batch-training weights
- reset-aware SRN trace capture in the BasicProp probe for consistent hidden-state parity checks

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
- right-side training/test control panel with BasicProp-like defaults, button layout, and `Train`/`continue` progress behavior
- live network diagram colored by weight sign and magnitude, now verified from an actual rendered desktop screenshot
- 2-layer, 3-layer, and 4-layer-aware topology summaries, diagram layout, and weight-layer inspection
- console/config/pattern/weight tabs
- BasicProp-style training console wording (`Training steps` / `Training finished`)
- BasicProp-style `Test All` / `Test One` console output wording and menu gating
- test-one gating that follows the original `< 24 patterns` behavior
- pattern loading bound to the currently configured network state rather than an editor reparse
- network saving bound to the currently loaded configured network state rather than an editor reparse
- utility tabs for weight maps, pattern/output tables, and plots
- weight legend and error plot styling that tracks the original BasicProp layout more closely
- modal `Invalid value` dialogs plus BasicProp-style note routing into the detached messages window
- split `Weights` menu entries for feed-forward and SRN loading, with BasicProp-style wrong-menu notes
- menu-driven pop-up windows for weights, patterns/outputs, time-series plots, and 3D plots
- a detached message-log window under `Help`, mirroring the original BasicProp message-frame workflow
- hidden-activation export to CSV from the desktop app

## Samples

The `samples/` directory contains starter network and pattern files:

- `xor.swcfg` and `xor.pat`
- `echo-srn.swcfg` and `echo-srn.pat`

## Compatibility note

SignalWeave targets BasicProp 1.3 behavioral parity using the local reference JAR in `/home/simon/temp/BasicProp/basicProp-1.3.jar`. Legacy BasicProp file compatibility is intentionally out of scope.

## Delivery docs

- `docs/basicprop-compatibility.md`: scope and compatibility basis
- `docs/basicprop-engine-notes.md`: observed BasicProp 1.3 runtime semantics
- `docs/reference-assets.md`: local BasicProp reference bundle inventory
- `docs/parity-checklist.md`: parity status by engine, UI, and utilities
- `docs/e2e-backlog.md`: milestone backlog and implementation tasks
