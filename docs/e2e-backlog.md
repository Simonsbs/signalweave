# E2E Backlog

This is the execution backlog for turning SignalWeave into a complete BasicProp-equivalent application, using `basicProp-1.3.jar` and the screenshot bundle in `/home/simon/temp/BasicProp` as the current reference.

## Milestone 0 - Reference Baseline

Goal:

- Replace assumptions with observed BasicProp behavior.

Tasks:

- Run `basicProp-1.3.jar` and inventory every reachable screen and dialog.
- Decompile `FeedForwardNetwork`, `SrnNetwork`, `NetSpec`, `PatternConfig`, and `FileIO`.
- Capture one feed-forward golden experiment.
- Capture one SRN golden experiment.
- Save reference screenshots for:
  - patterns/output view
  - 3D plotting
  - time series plot
  - help/about
- Document all combo-box value sets in the control panel.

Acceptance criteria:

- A complete reference inventory exists in `docs/reference-assets.md`.
- Engine semantics unknowns are reduced to a small explicit list.
- At least two golden experiments are checked into the repo or documented with reproducible assets.

## Milestone 1 - Engine Parity

Goal:

- Make `SignalWeave.Core` produce the same results as BasicProp.

Tasks:

- Compare BasicProp feed-forward update math to `SignalWeave.Core`.
- Compare SRN recurrent update math and hidden-reset logic.
- Match error computation and stop conditions exactly.
- Match training button semantics:
  - fresh train
  - continue train
  - reset
- Add fixture-driven parity tests:
  - feed-forward outputs
  - SRN outputs
  - test-all error
  - hidden activations
- Tighten tolerances after every correction.

Acceptance criteria:

- Golden tests pass for both feed-forward and SRN cases.
- SignalWeave reproduces BasicProp outputs and aggregate error within agreed tolerance.
- Engine code paths are deterministic under fixed seeds.

## Milestone 2 - Native Project Format

Goal:

- Define the long-term SignalWeave file model without carrying BasicProp file-format baggage.

Tasks:

- Design JSON schema for:
  - project file
  - network definition
  - training settings
  - dataset reference or embedded dataset
  - checkpoints
  - UI state
- Implement versioned serialization.
- Add migration/version checks.
- Document the schema.

Acceptance criteria:

- A project can be saved and loaded without information loss.
- Schema versioning exists from the start.
- The engine is not coupled to the desktop view layer.

## Milestone 3 - Main Application Parity

Goal:

- Match the core BasicProp desktop workflows.

Tasks:

- Rebuild the top-level window layout:
  - menu bar
  - network canvas
  - control panel
  - console panel
  - error progress panel
- Implement a real control panel matching BasicProp controls:
  - learning rate
  - momentum
  - learning steps
  - weight range
  - batch update
  - cross-entropy
  - progress indicator
  - pattern selector
  - test buttons
- Add command/state management so the UI behaves like BasicProp in trained/untrained/loading states.

Acceptance criteria:

- Every core workflow visible in the current screenshots works in SignalWeave.
- UI state transitions match BasicProp behavior closely enough that a user can switch between the two without relearning the flow.

## Milestone 4 - Analysis Tools Parity

Goal:

- Recreate the visual and analysis tools that make BasicProp useful beyond raw training.

Tasks:

- Implement Hinton-style weight visualization with layer selector.
- Implement patterns and outputs viewer.
- Implement error-progress plotting.
- Implement 3D plotting.
- Implement time series plotting.
- Implement hidden activation export.

Acceptance criteria:

- Each utility menu item opens a working SignalWeave equivalent.
- Data shown by each tool matches the same underlying run state used in the main window.

## Milestone 5 - Release Hardening

Goal:

- Make the application ready for real use on all target operating systems.

Tasks:

- Validate Linux, Windows, and macOS behavior.
- Package desktop binaries for all three platforms.
- Add release automation for GitHub.
- Write user documentation and migration notes.
- Create a manual parity validation script for final release testing.

Acceptance criteria:

- Tagged releases exist for Linux, Windows, and macOS.
- Final parity checklist is green or has explicitly accepted exceptions.

## Implementation Task Queue

Priority 1:

- Capture golden experiments from the BasicProp JAR.
- Decompile and document engine math.
- Add parity tests in `tests/SignalWeave.Core.Tests`.

Priority 2:

- Replace the current placeholder workbench with the real BasicProp main-window layout.
- Implement the actual network configuration dialogs.
- Add a versioned native project schema.

Priority 3:

- Build the weight viewer, patterns/output viewer, and error graph.
- Build the 3D and time-series utilities.
- Add packaging and release automation.

## Definition of Done

SignalWeave is complete only when:

- it reproduces BasicProp 1.3 training and test results for the golden experiments
- it provides all core BasicProp workflows in the desktop app
- it ships as a stable cross-platform desktop application
- the parity checklist in `docs/parity-checklist.md` is green
