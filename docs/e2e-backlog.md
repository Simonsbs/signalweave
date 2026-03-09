# E2E Backlog

This backlog is the implementation plan for reaching BasicProp 1.3 feature parity in SignalWeave.

Parity target:

- executable reference: `/home/simon/temp/BasicProp/basicProp-1.3.jar`
- screenshot bundle: `/home/simon/temp/BasicProp`

Definition of parity for this document:

- every BasicProp ability exists in SignalWeave
- every BasicProp workflow is reachable from the desktop UI
- equivalent settings produce equivalent outputs, errors, and analysis data
- unsupported BasicProp topologies or actions are reduced to zero

## Current blockers to 100% parity

- feed-forward execution still lacks true 4-layer support
- engine parity is not yet locked by JAR-backed golden tests
- the main network canvas is still only an approximation of BasicProp layout behavior
- weights, patterns, utilities, and plotting flows are not yet proven identical against the JAR
- Windows and macOS parity are not validated

## Milestone P0 - Freeze The Parity Contract

Goal:

- convert “close to BasicProp” into a strict, testable contract

Tasks:

- enumerate every menu item, dialog, button, tab, chart, file workflow, and output artifact in BasicProp
- assign a parity ID to each feature
- classify each parity ID as:
  - `engine`
  - `ui`
  - `utility`
  - `file workflow`
  - `platform`
- record each item in `docs/parity-checklist.md` with one of:
  - `done`
  - `in-progress`
  - `todo`
  - `blocked`
- define numeric acceptance tolerance for:
  - outputs
  - aggregate error
  - hidden activations
  - exported analysis data

Acceptance criteria:

- `docs/parity-checklist.md` becomes the release gate
- every remaining gap is visible as an explicit parity item

## Milestone P1 - Complete The Reference Corpus

Goal:

- remove the remaining observational blind spots from the BasicProp reference

Tasks:

- capture screenshots for:
  - patterns and outputs window
  - time series plot window
  - 3D plotting setup and result windows
  - export-hidden-activations flow
  - help/about dialogs
- capture at least one golden experiment for each of:
  - feed-forward 3-layer
  - feed-forward 4-layer
  - SRN
  - batch update
  - cross-entropy
- capture saved weights and exported data where possible
- document exact menu flows and visible state changes for each capture

Acceptance criteria:

- every user-visible BasicProp workflow has a corresponding reference artifact
- no major UI or utility workflow remains undocumented

## Milestone P2 - Engine Topology Completion

Goal:

- support every BasicProp network topology in the core engine

Tasks:

- add true feed-forward 4-layer support to the core model
- extend weight structures to represent:
  - input -> hidden1
  - hidden1 -> hidden2
  - hidden2 -> output
  - optional bias rows at each applicable layer
- update forward pass, backpropagation, batch updates, and persistence for 4-layer FF
- keep SRN behavior intact while extending shared code paths
- update all topology-dependent summaries and validation code

Acceptance criteria:

- SignalWeave can execute BasicProp-equivalent 3-layer FF, 4-layer FF, and SRN networks
- no dialog or UI path advertises unsupported BasicProp topologies

## Milestone P3 - Exact Engine Parity

Goal:

- match BasicProp runtime behavior exactly enough to lock the engine

Tasks:

- finalize FF parity for:
  - online random pattern selection
  - batch accumulation and apply timing
  - SSE and cross-entropy deltas
  - momentum updates
  - stop rules
  - TSQ reporting
- finalize SRN parity for:
  - `reset` handling
  - `lastTrainedIx`
  - recurrent-state carry/flush behavior
  - hidden bias lifecycle quirks
- add JAR-backed golden tests for:
  - forward outputs
  - test-all aggregate error
  - per-cycle history
  - hidden activations
  - saved weights
- lock deterministic parity tests with explicit starting weights

Acceptance criteria:

- golden parity tests pass for FF 3-layer, FF 4-layer, and SRN
- engine behavior is no longer marked `in-progress`

## Milestone P4 - Configuration Dialog Parity

Goal:

- make `Configure Network` match BasicProp behavior and structure

Tasks:

- finish feed-forward config parity:
  - layer-count slider
  - second hidden layer enable/disable behavior
  - bias toggles by layer
  - apply/ok/cancel semantics
- finish SRN config parity:
  - dedicated SRN tab behavior
  - correct layer/bias controls
- preserve BasicProp separation:
  - topology in config dialog
  - training controls in main control panel
- validate the dialog against the screenshot bundle under desktop rendering

Acceptance criteria:

- feed-forward and SRN tabs have distinct content and behavior
- topology changes round-trip correctly into the engine

## Milestone P5 - Main Window Workflow Parity

Goal:

- make the core desktop workbench behave like BasicProp

Tasks:

- rebuild the network canvas to support:
  - FF 3-layer layout
  - FF 4-layer layout
  - SRN layout
  - bias boxes
  - proper line routing and scaling
- finish control panel parity:
  - enable/disable rules from `SimControl.checkControls()`
  - progress bar updates
  - `Train` / `continue`
  - `Reset`
  - `Test one`
  - `Test all`
  - pattern combo behavior for `< 24` and `>= 24`
- finish messaging parity:
  - modal invalid-value dialogs
  - missing-pattern dialogs
  - detached messages window behavior
- tighten menu structure and workflow names to BasicProp wording

Acceptance criteria:

- the main window can be used exactly like BasicProp for network setup, training, testing, and inspection
- screenshot-based comparisons show no structural workflow gaps

## Milestone P6 - Weights And Patterns Workflow Parity

Goal:

- finish the inspection workflows users rely on during training

Tasks:

- match Hinton-style weight window behavior:
  - FF vs SRN load semantics
  - layer selector behavior
  - refresh behavior
  - window titles and labels
- finish patterns and outputs parity:
  - table/chart behavior
  - pattern selectors
  - value ranges and labels
- verify saved weights and loaded weights against BasicProp semantics where relevant

Acceptance criteria:

- the weight and pattern inspection flows are functionally equivalent to BasicProp

## Milestone P7 - Utility And Analysis Parity

Goal:

- complete the non-core analysis tools that BasicProp exposes

Tasks:

- finish 3D plotting parity:
  - setup controls
  - result rendering semantics
  - target/output selector behavior
- finish time-series plot parity:
  - output selection
  - add-plot behavior
  - axis labeling
- finish hidden activation export parity
- verify clustering workflows against the JAR
- align utility dialog titles, controls, and dismissal behavior

Acceptance criteria:

- every BasicProp utility menu entry is implemented and parity-verified

## Milestone P8 - Platform And Release Parity

Goal:

- make SignalWeave usable as a real cross-platform replacement

Tasks:

- validate Linux, Windows, and macOS desktop behavior
- package self-contained binaries for all 3 platforms
- add GitHub release automation
- create a final manual parity script for release sign-off
- document known zero-gap parity status

Acceptance criteria:

- release binaries exist for Linux, Windows, and macOS
- final parity checklist contains no remaining `todo` or `blocked` items

## Issue-Sized Task Queue

### Engine

- `ENG-001` add 4-layer FF topology to `NetworkDefinition`
- `ENG-002` extend weight persistence for 4-layer FF
- `ENG-003` implement 4-layer FF forward pass
- `ENG-004` implement 4-layer FF backpropagation
- `ENG-005` verify FF batch semantics against JAR
- `ENG-006` verify FF cross-entropy semantics against JAR
- `ENG-007` verify FF momentum semantics against JAR
- `ENG-008` verify FF stop-rule semantics against JAR
- `ENG-009` verify SRN hidden-bias lifecycle against JAR
- `ENG-010` add golden parity fixtures for FF 3-layer
- `ENG-011` add golden parity fixtures for FF 4-layer
- `ENG-012` add golden parity fixtures for SRN

### Configuration

- `CFG-001` make feed-forward dialog fully 4-layer-aware
- `CFG-002` make SRN tab match the reference layout exactly
- `CFG-003` implement BasicProp-like layer enable/disable behavior
- `CFG-004` verify Apply/OK/Cancel state transitions

### Main Window

- `UI-001` support FF 4-layer canvas drawing
- `UI-002` support SRN-specific canvas drawing parity
- `UI-003` mirror `SimControl.checkControls()` enable/disable behavior
- `UI-004` replace console-only invalid-value reporting with modal dialogs
- `UI-005` replace console-only missing-pattern reporting with modal dialogs
- `UI-006` match BasicProp progress chart behavior more closely
- `UI-007` tighten control-panel spacing and styling toward the reference

### Weights And Patterns

- `WGT-001` separate FF and SRN weight-load workflows
- `WGT-002` tighten Hinton glyph sizing and color parity
- `WGT-003` verify layer switching and refresh parity
- `PAT-001` finish patterns/output window parity
- `PAT-002` verify `Test one` and pattern selector edge cases

### Utilities

- `UTL-001` verify time-series plotting against reference screenshots
- `UTL-002` verify surface plot semantics against the JAR
- `UTL-003` verify hidden activation export shape and values
- `UTL-004` verify clustering workflows and outputs

### Release

- `REL-001` validate Windows desktop build
- `REL-002` validate macOS desktop build
- `REL-003` add packaged release artifacts
- `REL-004` add GitHub release workflow
- `REL-005` run final parity sign-off checklist

## Execution Order

1. `P0` freeze parity IDs and acceptance criteria
2. `P1` complete the reference corpus
3. `P2` complete missing topologies
4. `P3` lock the engine with golden tests
5. `P4` and `P5` finish core desktop parity
6. `P6` and `P7` finish inspection and utilities
7. `P8` validate platforms and ship

## Definition Of Done

SignalWeave is complete only when:

- FF 3-layer, FF 4-layer, and SRN are all supported
- all BasicProp menu workflows exist
- all dialogs and utilities have equivalent behavior
- JAR-backed parity tests pass
- the final parity checklist is fully green
- Linux, Windows, and macOS builds are validated
