# SignalWeave

SignalWeave is a cross-platform `.NET 8` desktop and CLI alternative to BasicProp. The project is open source, runs on Windows, Linux, and macOS through Avalonia, and is built around a compatibility-first core so the original BasicProp workflows can be reproduced without locking the project to a single OS.

Current status: the checked-in BasicProp 1.3 parity checklist is fully closed, with local probe-backed sign-off and a verified remote GitHub publish-matrix run.

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
- full checked-in BasicProp probe coverage now runs through `scripts/parity-signoff.sh`, which builds the solution, executes the core regression suite, compiles the probe, and runs every committed `.bppr` experiment
- checked-in BasicProp golden fixtures for 2-layer, 3-layer, 3-layer batch, 3-layer cross-entropy, 3-layer momentum, stop-rule, and 4-layer feed-forward training parity
- checked-in BasicProp multi-step online feed-forward fixture to verify repeated pattern-mode updates against the reference JAR
- checked-in BasicProp SRN fixtures for forward outputs, sequential-training weights, and batch-training weights
- reset-aware SRN trace capture in the BasicProp probe for consistent hidden-state parity checks
- full JAR-backed parity sign-off across the checked-in probe suite and core regression fixtures
- screenshot-backed main-window and weight-window parity checks against the local BasicProp reference bundle
- successful remote GitHub `release` workflow publish-matrix run with bundled artifacts for all supported desktop targets

Deliberate differences:

- legacy BasicProp file compatibility is out of scope by project decision
- SignalWeave uses native `signalweave-project/v2` and `signalweave-checkpoint/v1` formats instead of the retired BasicProp file formats

## Product lines

- `Classic`: the current BasicProp-style desktop experience in `src/SignalWeave.Classic.Desktop`
- `Modern`: a separate desktop product line for the next workflow/UI in `src/SignalWeave.Modern.Desktop`, centered on a single project-file workflow

Both apps share the same engine and general logic through `src/SignalWeave.Core`.

Modern workflow direction:

- one project file stores network settings, embedded patterns, current weights, completed cycles, and Modern control-panel state
- Modern no longer needs separate load/save actions for network settings, weights, or patterns
- the network settings surface lives inside the main window as a control-panel tab instead of a popup dialog
- detailed Modern usage docs live in:
  - `docs/modern-ui-guide.md`
  - `docs/modern-user-manual.md`

## Projects

- `src/SignalWeave.Core`: parsers, engine, clustering, sample assets
- `src/SignalWeave.Cli`: terminal workflow
- `src/SignalWeave.Classic.Desktop`: BasicProp-style Avalonia desktop app
- `src/SignalWeave.Modern.Desktop`: new product-line Avalonia desktop app
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
dotnet run --project src/SignalWeave.Classic.Desktop
dotnet run --project src/SignalWeave.Modern.Desktop
```

The Classic desktop app ships with built-in XOR and SRN demos and now exposes a BasicProp-like workflow surface:

- top-level `Network`, `Weights`, `Patterns`, `Utilities`, and `Help` menus
- startup with a default feed-forward network, no loaded patterns, and the original BasicProp-style prompt to load patterns before running simulations
- desktop dialogs for configuring networks and loading/saving SignalWeave files
- native desktop workflows for loading/saving `signalweave-project/v2` and `signalweave-checkpoint/v1` documents
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
- live network diagram colored by weight sign and magnitude, now verified from actual rendered desktop screenshots
- main network nodes now render as BasicProp-like unlabeled boxes with tighter BasicProp-style spacing instead of SignalWeave-style annotated editor nodes
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
- weight display now uses a matrix-sized Hinton-style frame with numeric layer selectors plus `Rec` for SRN recurrent weights, BasicProp-style target-row/source-column orientation, a compact BasicProp-like bottom control strip, a live `Refresh` action against the current engine weights, and screenshot-backed popup verification against the BasicProp reference window
- utility-window launches stay quiet in the main console, matching BasicProp more closely
- in-place analysis/refresh actions also stay quiet unless they actually produce user-facing output or a note
- reset and hidden-activation export no longer emit synthetic console lines
- a detached message-log window under `Help`, mirroring the original BasicProp message-frame workflow
- restored `Help -> Messages` and `Help -> Clear Messages` actions so the detached message window is reachable from the visible desktop shell
- hidden-activation export from the desktop app through a save dialog using the BasicProp `getHiddenActs()` helper semantics and raw concatenated `.dat` rows
- screenshot-driven main-shell cleanup now trims some wasted network-pane space while keeping the existing diagram visible

The Modern desktop app uses a single-project workflow instead:

- top toolbar for `New`, `Load`, `Save`, and `Save As`
- left workspace tabs for `Network Graph`, `Weights`, and `Analysis`
- right workflow tabs for `Network`, `Training`, `Tests`, `Patterns`, and `Summary`
- bottom `Console` and `Error graph` panels
- project-first save/load using `.swproj.json`

Quick start for Modern:

1. Start the app:

```bash
dotnet run --project src/SignalWeave.Modern.Desktop
```

2. Click `📂 Load`
3. Open `samples/seventeen-patterns-7x7-modern.swproj.json`
4. Open `Training` and click `Train #1`
5. Open `Tests` and click `Test all`
6. Inspect the result in:
- `Network Graph`
- `Weights`
- `Analysis`

Full step-by-step instructions are in:

- `docs/modern-ui-guide.md`
- `docs/modern-user-manual.md`

## Release automation

- `.github/workflows/ci.yml` runs restore, release build, and core tests on Linux, Windows, and macOS.
- `.github/workflows/release.yml` publishes self-contained bundles for either the `Classic` or `Modern` desktop line depending on the release tag prefix.
- release tags are product-specific:
  - `classic-vX.Y.Z`
  - `modern-vX.Y.Z`
- the GitHub `release` workflow is now verified from a successful remote `workflow_dispatch` publish-matrix run: `22874465646`
- `scripts/parity-signoff.sh` is the local release-gate command for the `Classic` line: it builds the solution, runs the core parity tests, compiles and executes every checked-in BasicProp probe, then publishes and archives the Classic desktop bundles for `linux-x64`, `win-x64`, `osx-x64`, and `osx-arm64`.
- validated local publish examples:

```bash
dotnet publish src/SignalWeave.Classic.Desktop/SignalWeave.Classic.Desktop.csproj -c Release -r linux-x64 --self-contained true -o artifacts/signalweave-classic-desktop-linux-x64
dotnet publish src/SignalWeave.Classic.Desktop/SignalWeave.Classic.Desktop.csproj -c Release -r win-x64 --self-contained true -o artifacts/signalweave-classic-desktop-win-x64
dotnet publish src/SignalWeave.Modern.Desktop/SignalWeave.Modern.Desktop.csproj -c Release -r linux-x64 --self-contained true -o artifacts/signalweave-modern-desktop-linux-x64
dotnet publish src/SignalWeave.Modern.Desktop/SignalWeave.Modern.Desktop.csproj -c Release -r win-x64 --self-contained true -o artifacts/signalweave-modern-desktop-win-x64
```

Full local sign-off:

```bash
./scripts/parity-signoff.sh
```

## Samples

The `samples/` directory contains starter network and pattern files:

- `xor.swcfg` and `xor.pat`
- `echo-srn.swcfg` and `echo-srn.pat`
- `seventeen-patterns-7x7-modern.swproj.json`

## Compatibility note

SignalWeave targets BasicProp 1.3 behavioral parity using the local reference JAR in `/home/simon/temp/BasicProp/basicProp-1.3.jar`. Legacy BasicProp file compatibility is intentionally out of scope.

## Delivery docs

- `docs/basicprop-compatibility.md`: scope and compatibility basis
- `docs/basicprop-engine-notes.md`: observed BasicProp 1.3 runtime semantics
- `docs/reference-assets.md`: local BasicProp reference bundle inventory
- `docs/parity-checklist.md`: parity status by engine, UI, and utilities
- `docs/e2e-backlog.md`: milestone backlog and implementation tasks
- `docs/signalweave-schema.md`: native project and checkpoint schema definitions
- `docs/product-lines.md`: Classic/Modern product-line and release strategy
- `docs/modern-ui-todo.md`: Modern-specific backlog for porting analysis and utility workflows
- `docs/modern-ui-guide.md`: step-by-step guide for loading, training, testing, analyzing, and saving Modern projects
- `docs/modern-user-manual.md`: full Modern reference manual covering every section, setting, and workflow surface
