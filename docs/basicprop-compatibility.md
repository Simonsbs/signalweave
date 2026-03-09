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
- Probe-backed SRN fixtures for forward outputs, sequential-training weights, and batch-training weights
- Reset-aware SRN trace capture in the BasicProp probe so hidden activations can be compared from one consistent execution path
- BasicProp-style desktop shell with:
  - menu structure
  - dedicated network configuration dialog
  - distinct feed-forward and SRN tab content in the configuration dialog, instead of one merged form
  - working 2-layer feed-forward apply path in the desktop dialog
  - working 4-layer feed-forward apply path in the desktop dialog
  - desktop save/load workflows for networks and weights, plus pattern loading
  - screenshot-driven control-panel defaults and layout closer to the BasicProp 1.3 shell
  - `Train` to `continue` state transitions and BasicProp-style control-value validation messages
  - modal `Invalid value` dialogs for invalid learning-rate, momentum, learning-step, and empty-pattern conditions
  - test-one/test-all interaction
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
  - dedicated time-series plot workflow aligned to the BasicProp `TimeSeriesPlotter` control surface
  - cumulative time-series `Add plot` behavior with `InputN`, `TargetN`, and `OutputN` selectors, plus the BasicProp-sized 600x300 frame
  - dedicated `Plot Setup` workflow aligned to the BasicProp `SurfacePlotter` control surface
  - dedicated `3D plot` result window using BasicProp-style unique-X/unique-Y grid semantics from `TargetN` or `OutputN`
  - dedicated per-pattern chart workflow aligned to the BasicProp `PatternPlot` control surface
  - BasicProp-style pattern selector formatting using the original `PatternHolder` text shape (`[0]: ...    >>>...`)
  - BasicProp-style `PatternPlot` bar labels (`outputN`, `targetN`, `inputN`) and 800x600 window sizing
  - hidden activation export
  - detached message-log window under `Help`, aligned to the BasicProp `MessageWindow` workflow
  - `SimControl.checkPatternsAvailable()`-style mixed feedback routing: note-style message output for uninitialized patterns and modal feedback for zero loaded patterns
  - split `Weights` menu with `Load Weights (FF)` and `Load Weights (SRN)`, including the original wrong-menu note messages
  - screenshot-driven cleanup of the main window and configuration dialog to remove SignalWeave-specific chrome and move closer to the BasicProp 1.3 layout
  - built-in XOR demo defaults aligned to the BasicProp reference control values (`0.3`, `0.8`, `5000`, `-1 - 1`)

## Remaining parity work

- Match BasicProp 1.3 engine behavior exactly from the runnable JAR
- Extend the golden parity suite beyond the current feed-forward training fixtures and initial SRN training fixtures into broader FF and SRN coverage
- Continue resolving the remaining SRN parity edges around BasicProp's inconsistent helper buffers, using the reset-aware probe trace instead of raw `getHiddenActs()`
- Recreate the original graph and weight-grid visual panels more exactly in the desktop app
- Tighten utility workflows and visuals so they match BasicProp more closely

## Scope decision

- Exact BasicProp file compatibility is not required.
- SignalWeave may use its own project, dataset, and checkpoint formats.
- Exact behavioral parity remains the target for:
  - training results
  - testing outputs
  - plots and visual analysis
  - user workflows
