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
- screenshot-verified feed-forward and SRN configuration tabs with BasicProp-style layer-count and bias controls
- 4-layer feed-forward topology now flows through the live desktop summary, network diagram, and weight-inspection utilities
- native desktop file workflows for network save/load, pattern load, and weight save/load
- utility views for weight maps, pattern/output inspection, projected 3D plotting, time-series plotting, and hidden-activation export
- BasicProp-style visual touches including weight-value legend, Hinton-like weight cells, and axis-based error plotting
- dedicated secondary windows for weights, patterns/outputs, and plot utilities from the top-level menus
- popup utility windows with BasicProp-style layer controls and axis labels
- dedicated JAR-aligned time-series plot window with `Output`, `Add plot`, and `Dismiss` controls
- dedicated JAR-aligned `Plot Setup` window for 3D/surface plotting with `X`, `Y`, `Z`, `Show Plot`, and `Dismiss`
- dedicated JAR-aligned `PatternPlot` window with per-pattern selector and stacked `Outputs` / `Targets` / `Inputs` charts
- JAR decompile verification for `PatternPlot`, `TimeSeriesPlotter`, and `SurfacePlotter` now matches the implemented utility-window sizes and control surfaces
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
dotnet run --project src/SignalWeave.Cli -- pack-project --network samples/xor.swcfg --patterns samples/xor.pat --output xor.swproj.json
dotnet run --project src/SignalWeave.Cli -- pack-checkpoint --network samples/xor.swcfg --patterns samples/xor.pat --weights weights.json --output xor.swcheckpoint.json --cycles 5000
dotnet run --project src/SignalWeave.Cli -- summary --project xor.swproj.json
dotnet run --project src/SignalWeave.Cli -- train --project xor.swproj.json --checkpoint-out xor.swcheckpoint.json
dotnet run --project src/SignalWeave.Cli -- summary --checkpoint xor.swcheckpoint.json
```

## Desktop

```bash
dotnet run --project src/SignalWeave.Desktop
```

The desktop app ships with built-in XOR and SRN demos and now exposes a BasicProp-like workflow surface:

- top-level `Network`, `Weights`, `Patterns`, `Utilities`, and `Help` menus
- startup with a default feed-forward network, no loaded patterns, and the original BasicProp-style prompt to load patterns before running simulations
- desktop dialogs for configuring networks and loading/saving SignalWeave files
- native desktop workflows for loading/saving `signalweave-project/v1` and `signalweave-checkpoint/v1` documents from the `Network` menu
- progress state now follows cumulative completed training cycles more closely across repeated `Train` runs
- per-step training progress now updates the progress display during active learning instead of only changing at the start and end of a run
- the error-progress plot now updates live during active learning instead of only after the run completes
- right-side training/test control panel with BasicProp-like defaults, button layout, and BasicProp-style `continue` text only during active training
- explicit controller activity states now keep `Train` / `continue` aligned with BasicProp, so test actions no longer flip the train button to `continue`
- train/test actions now enter a real busy state so the control-panel labels, combos, Batch Update checkbox, pattern selector, and run buttons disable during active controller work more like BasicProp’s `SimControl.checkControls()`
- the `X-entropy` checkbox now stays enabled during controller activity, matching the BasicProp 1.3 control surface more closely
- `SimControl.checkControls()` has now been checked directly against the BasicProp JAR decompile for the controller-managed enable/disable set and `Test one` threshold behavior
- screenshot-verified four-panel main shell matching the original BasicProp layout more closely: `Network`, `Control panel`, `Console`, and `Error progress`
- visible top-level menus now match the BasicProp 1.3 shell more closely, with only the original `Network`, `Weights`, `Patterns`, `Utilities`, and single-item `Help` surface
- the four-panel main desktop workbench is now treated as complete parity surface; remaining gaps there are fidelity items, not missing shell workflow
- main window title and weight legend now follow the BasicProp shell more closely instead of using SignalWeave-specific defaults
- live network diagram colored by weight sign and magnitude, now verified from an actual rendered desktop screenshot
- main network nodes now render as BasicProp-like unlabeled boxes instead of SignalWeave-style annotated editor nodes
- 2-layer, 3-layer, and 4-layer-aware topology summaries, diagram layout, and weight-layer inspection
- BasicProp-style training console wording (`Training steps` / `Training finished`)
- BasicProp-style `Test All` / `Test One` console output wording and menu gating
- test-one gating that follows the original controller behavior: enabled through `24` patterns, then disabled above that while the combo collapses to the pattern-source name
- pattern loading bound to the currently configured network state rather than an editor reparse
- BasicProp-style `Failed to load patterns` note for bad pattern files
- network saving bound to the currently loaded configured network state rather than an editor reparse
- successful network/pattern/weight file actions now stay quiet in the main console, matching BasicProp more closely
- network-definition apply/load paths now clear stale console output instead of leaving old notes/results visible
- BasicProp-style `Batch Update` behavior in the desktop control panel: still visible/enabled on the surface, but automatically cleared for SRNs in the applied runtime
- BasicProp-style pattern selector text in the main control panel
- cycle-based progress-bar behavior that no longer invents a separate `Loaded weights` state or use the selected learning-step count as the idle maximum
- weight legend and error plot styling that tracks the original BasicProp layout more closely
- modal `Invalid value` dialogs plus BasicProp-style note routing into the detached messages window
- BasicProp-style note prefix formatting (`Note:`) in controller-driven desktop feedback
- note-style controller feedback now appends to the desktop console instead of replacing the current console text
- note-style controller feedback now renders with proper `Note: ...` spacing in the main console
- BasicProp-style `No can do!` modal feedback when train/test actions are attempted with zero loaded patterns
- split `Weights` menu entries for feed-forward and SRN loading, with BasicProp-style wrong-menu notes
- menu-driven pop-up windows for weights, patterns/outputs, time-series plots, and 3D plots
- patterns and outputs are viewable both as a detailed table window and through the dedicated per-pattern chart workflow
- visible utility-menu access to output clustering and hidden-state clustering through dedicated text-report windows
- desktop pattern loading and clustering utilities are now fully reachable through the visible controller/menu workflow
- the visible menu labels now mirror the BasicProp wording more closely by removing extra ellipses and non-reference top-level actions
- network configuration `Apply` now updates the live desktop state without closing the dialog
- network configuration styling now follows the BasicProp dialog more closely, with centered `OK` / `Apply` / `Cancel` buttons and tighter slider/bias layout
- weight display now uses a matrix-sized Hinton-style frame with numeric layer selectors plus `Rec` for SRN recurrent weights, a compact BasicProp-like bottom control strip, and a live `Refresh` action against the current engine weights
- utility-window launches stay quiet in the main console, matching BasicProp more closely
- in-place analysis/refresh actions also stay quiet unless they actually produce user-facing output or a note
- reset and hidden-activation export no longer emit synthetic console lines
- a detached message-log window under `Help`, mirroring the original BasicProp message-frame workflow
- restored `Help -> Messages` and `Help -> Clear Messages` actions so the detached message window is reachable from the visible desktop shell
- hidden-activation export from the desktop app through a save dialog using the BasicProp `getHiddenActs()` helper semantics and raw concatenated `.dat` rows
- screenshot-driven main-shell cleanup now trims some wasted network-pane space while keeping the existing diagram visible

## Release automation

- `.github/workflows/ci.yml` runs restore, release build, and core tests on Linux, Windows, and macOS.
- `.github/workflows/release.yml` publishes self-contained desktop bundles for `linux-x64`, `win-x64`, `osx-x64`, and `osx-arm64`, uploads them as workflow artifacts, and attaches them to GitHub releases for `v*` tags.
- `scripts/parity-signoff.sh` is the local release-gate command: it builds the solution, runs the core parity tests, compiles and executes every checked-in BasicProp probe, then publishes and archives the desktop bundles for `linux-x64`, `win-x64`, `osx-x64`, and `osx-arm64`.
- validated local publish examples:

```bash
dotnet publish src/SignalWeave.Desktop/SignalWeave.Desktop.csproj -c Release -r linux-x64 --self-contained true -o artifacts/signalweave-desktop-linux-x64
dotnet publish src/SignalWeave.Desktop/SignalWeave.Desktop.csproj -c Release -r win-x64 --self-contained true -o artifacts/signalweave-desktop-win-x64
dotnet publish src/SignalWeave.Desktop/SignalWeave.Desktop.csproj -c Release -r osx-x64 --self-contained true -o artifacts/signalweave-desktop-osx-x64
dotnet publish src/SignalWeave.Desktop/SignalWeave.Desktop.csproj -c Release -r osx-arm64 --self-contained true -o artifacts/signalweave-desktop-osx-arm64
```

Full local sign-off:

```bash
./scripts/parity-signoff.sh
```

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
- `docs/signalweave-schema.md`: native project and checkpoint schema definitions
