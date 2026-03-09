# BasicProp Compatibility Target

Source basis used for this implementation:

- https://basicprop.wordpress.com/
- https://basicprop.wordpress.com/configure/
- https://basicprop.wordpress.com/details/
- https://basicprop.wordpress.com/example/
- https://basicprop.wordpress.com/interacting/
- https://basicprop.wordpress.com/patterns/
- https://basicprop.wordpress.com/weights/
- local reference bundle: `/home/simon/temp/BasicProp`

## Mapped feature surface

- Feed-forward networks
- Simple recurrent networks
- Bias controls
- Pattern-by-pattern and batch training
- Learning rate and momentum
- Random weight initialization range
- Stop conditions based on epochs and error
- Text pattern files
- Test-one and test-all workflows
- Weight save/load
- Output visualization and clustering
- 3D plotting
- Time series plotting
- Hidden activation export

## What SignalWeave already covers

- Config and pattern parsing
- Core engine for feed-forward and SRN execution
- Core engine support for 2-layer, 3-layer, and 4-layer feed-forward execution, plus SRN execution
- Training and testing workflows
- Weight persistence
- Hierarchical clustering over outputs and hidden activations
- Probe-backed golden regression fixtures for 2-layer, 3-layer, 3-layer batch, 3-layer cross-entropy, 3-layer momentum, stop-rule, and 4-layer feed-forward training
- Probe-backed SRN fixtures for forward outputs, direct `Test one` output, sequential-training weights, batch-training weights, hidden-bias lifecycle, leading-`reset`, and reported aggregate error
- Training fixtures now also assert completed-cycle parity alongside the saved weight matrices
- Reset-aware SRN trace capture in the BasicProp probe so hidden activations can be compared from one consistent execution path
- Probe-backed SRN edge-case fixture for the ignored leading-`reset` marker behavior
- Probe-backed SRN training fixture for the hidden-bias lifecycle across reset boundaries
- BasicProp-style reported SRN `testAll()` average error is now tracked separately from the coherent reset-aware trace used for outputs and hidden states
- `Test one` now follows BasicProp's direct `testOnePattern(...)` path instead of deriving the selected pattern from a full `Test all` pass
- BasicProp-style desktop shell with:
  - menu structure
  - dedicated network configuration dialog
  - distinct feed-forward and SRN tab content in the configuration dialog, instead of one merged form
  - working 2-layer feed-forward apply path in the desktop dialog
  - working 4-layer feed-forward apply path in the desktop dialog
  - desktop save/load workflows for networks and weights, plus pattern loading
  - screenshot-driven control-panel defaults and layout closer to the BasicProp 1.3 shell
  - screenshot-driven four-panel main workbench layout closer to the original `Network` / `Control panel` / `Console` / `Error progress` arrangement
  - top-level menu surface now tracks the BasicProp 1.3 shell more closely by removing extra SignalWeave-only menu actions and matching the original visible wording
  - main frame title and default weight legend now read closer to the original shell chrome and legend markings
  - main network pane now renders unlabeled node boxes closer to the original BasicProp display instead of annotated editor-style node labels
  - BasicProp-style training console phrasing, control-value validation messages, and `continue` button text only while a training action is active
  - train/test actions now drive a real busy-state control surface so learning/test buttons, control-panel combos, checkboxes, and pattern selection disable during active controller work more like `SimControl.checkControls()`
  - modal `Invalid value` dialogs for invalid learning-rate, momentum, learning-step, and empty-pattern conditions
  - BasicProp-style `Test All` / `Test One` console output text and test-one menu gating
  - test-one/test-all interaction
  - note output now renders with proper `Note: ...` spacing in the desktop console instead of collapsing directly into the message text
  - pattern loading now uses the current configured network state instead of reparsing the editor surface
  - pattern-loading parse/validation failures now collapse to the original `Failed to load patterns` note instead of leaking raw parser exceptions into the desktop console
  - network saving now uses the loaded configured network state instead of reparsing the editor surface
  - successful network/pattern/weight load-save actions no longer inject synthetic console status lines that BasicProp itself does not emit
  - network-definition changes now clear stale console output when they go through the controller-style apply/load path, matching BasicProp's `updateDisplay()` behavior more closely
  - `Batch Update` now stays enabled like the original control surface, but is cleared automatically for SRNs so the applied runtime still matches BasicProp's feed-forward-only batch-update semantics
  - main `Pattern` selector now uses BasicProp-style `PatternHolder` text for `< 24` patterns and falls back to the loaded pattern source name at `24+`, while keeping `Test one` enabled at exactly `24` patterns to match the original controller
  - weight loading no longer fakes a `Loaded weights` progress-bar state; the progress bar stays aligned with BasicProp's cycle-based `Untrained`/count display instead of the selected learning-step count
  - current weight inspection
  - screenshot-verified live network diagram with 2-layer, 3-layer, 4-layer, and SRN-aware weight routing
  - weight-map visualization with Hinton-style emphasis
  - 2-layer and 4-layer-aware weight-layer switching in the desktop and popup weight views
  - pattern/output inspection
  - projected 3D and time-series utility plots
  - weight legend and axis-based error progress plot
  - progress bar state with `Untrained` and cycle-count display
  - dedicated secondary windows for weights, plots, and patterns/outputs
  - popup plot axes/labels and BasicProp-style weight-layer controls
  - startup now mirrors BasicProp more closely: a default feed-forward network is loaded with no patterns, and the initial desktop note prompts the user to load patterns before running simulations
  - training progress now tracks cumulative completed cycles more closely instead of resetting the progress label to the last training batch size
  - dedicated time-series plot workflow aligned to the BasicProp `TimeSeriesPlotter` control surface
  - cumulative time-series `Add plot` behavior with `InputN`, `TargetN`, and `OutputN` selectors, plus the BasicProp-sized 600x300 frame
  - dedicated `Plot Setup` workflow aligned to the BasicProp `SurfacePlotter` control surface
  - dedicated `3D plot` result window using BasicProp-style unique-X/unique-Y grid semantics from `TargetN` or `OutputN`
  - dedicated per-pattern chart workflow aligned to the BasicProp `PatternPlot` control surface
  - utility-window launches no longer inject extra console status text for weight, pattern, time-series, or 3D plot windows, matching the original controller more closely
  - tab-only utility refresh and analysis actions no longer inject synthetic console status text when they simply update in-place views
  - `Reset` and hidden-activation export now stay quiet in the main console, matching the original controller's silent action flow more closely
  - output clustering, hidden-state clustering, and the compatibility summary are now reachable again from the visible desktop shell through dedicated text-report windows instead of remaining hidden behind dead viewmodel commands
  - BasicProp-style pattern selector formatting using the original `PatternHolder` text shape (`[0]: ...    >>>...`)
  - BasicProp-style `PatternPlot` bar labels (`outputN`, `targetN`, `inputN`) and 800x600 window sizing
  - hidden activation export through a save dialog using BasicProp-like raw `.dat` rows instead of the earlier SignalWeave temp-CSV flow
  - detached message-log window under `Help`, aligned to the BasicProp `MessageWindow` workflow
  - `SimControl.checkPatternsAvailable()`-style mixed feedback routing: BasicProp-style `Note:` message output that appends to the desktop console for uninitialized patterns, plus a `No can do!` modal for zero loaded patterns
  - split `Weights` menu with `Load Weights (FF)` and `Load Weights (SRN)`, including the original wrong-menu note messages
  - screenshot-driven cleanup of the main window and configuration dialog to remove SignalWeave-specific chrome and move closer to the BasicProp 1.3 layout
  - built-in XOR demo defaults aligned to the BasicProp reference control values (`0.3`, `0.8`, `5000`, `-1 - 1`) when loaded from the menu
  - `Configure Network...` now opens from the loaded network definition rather than reparsing the editor first
  - `Network Configuration -> Apply` now updates the live desktop network without closing the dialog, matching the original workflow more closely
  - network configuration sliders, bias column, and centered action buttons now follow the BasicProp dialog proportions more closely
  - popup weight display now uses a larger Hinton-style frame with a compact left-aligned bottom control strip closer to the original window

## Remaining parity work

- Match BasicProp 1.3 engine behavior exactly from the runnable JAR
- Continue resolving the remaining SRN parity edges around BasicProp's inconsistent helper buffers, using the reset-aware probe trace instead of raw `getHiddenActs()`
- Recreate the original graph and weight-grid visual panels more exactly in the desktop app
- Tighten utility workflows and visuals so they match BasicProp more closely
- Finish cross-platform release validation from real CI runs and ship the packaged desktop bundles
- Keep the local sign-off path in sync with the release gate through `scripts/parity-signoff.sh`

## Scope decision

- Exact BasicProp file compatibility is not required.
- SignalWeave now uses its own documented `signalweave-project/v1` and `signalweave-checkpoint/v1` JSON formats.
- Exact behavioral parity remains the target for:
  - training results
  - testing outputs
  - plots and visual analysis
  - user workflows
