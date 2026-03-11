# Modern UI To-Do

This backlog tracks the remaining functionality worth porting from the `Classic` desktop line into `Modern`.

Principles for `Modern`:

- keep the single-project workflow
- do not reintroduce separate network/weights/pattern file workflows
- port capabilities, not BasicProp screen-for-screen duplication
- prefer in-window tools unless a dedicated utility window clearly improves the workflow

## Highest priority

- Build a dedicated weight inspector for `Modern`
  - show full matrices, not just summary text and live edge colors
  - support layer switching for direct, 3-layer, 4-layer, and SRN/recurrent weights
  - include a clear visual mode for value, sign, and magnitude

- Build a dedicated pattern/output inspector
  - show per-pattern inputs, targets, outputs, and hidden activations
  - allow quick navigation from the test-pattern list into detailed inspection
  - preserve the current network-graph activation view as the fast overview

- Add hidden-activation export to `Modern`
  - export the current hidden activation set directly from the project workflow
  - keep output compatible with the shared engine/export path already used in `Classic`

## Analysis utilities

- Port hidden-state clustering into `Modern`
  - output clustering report
  - hidden-state clustering report
  - keep report output readable and exportable

- Port compatibility/report views into `Modern`
  - surface shared engine analysis reports in the Modern workflow
  - use one reusable report view instead of multiple narrowly scoped windows if possible

- Port time-series plotting into `Modern`
  - keep the data path from the shared engine
  - adapt the setup flow to Modern instead of copying the Classic shell

- Port 3D/surface plotting into `Modern`
  - expose X/Y/Z selection through a Modern-oriented flow
  - reuse the shared plotting/session logic where possible

## Result workflows

- Expand the test-pattern list into a fuller result browser
  - filter by pass/fail/not-tested
  - sort by label, error, or status
  - add quick access to outputs/targets deltas

- Add export actions for test results
  - Markdown or CSV summary for `Test all`
  - per-pattern result export

- Add a clearer test session history
  - keep training sessions and test sessions distinct
  - make it easy to compare the latest run against prior runs

## Diagnostics

- Decide whether `Modern` still needs a detached messages window
  - default recommendation: keep the integrated console as primary
  - only add a detached message view if the integrated console proves insufficient for longer analysis sessions

- Improve structured console output further
  - richer tables for `Test all`
  - clearer summaries for rollback, train, and export actions

## Lower priority polish

- Add a dedicated graph-inspection mode
  - zoom
  - pan
  - richer node/edge hover details

- Add screenshot/export presets
  - graph-only
  - graph plus legend
  - error graph only

- Review whether some analysis tools belong as tabs instead of windows
  - default recommendation: keep frequent workflows in tabs
  - use separate windows only for dense exploratory tools

## Explicit non-goals

- Do not port separate `Load Weights`, `Save Weights`, or `Load Patterns` workflows into `Modern`
- Do not recreate the BasicProp menu structure inside `Modern`
- Do not make `Modern` depend on the `Classic` desktop project

## Recommended implementation order

1. Weight inspector
2. Pattern/output inspector
3. Hidden-activation export
4. Clustering/report views
5. Time-series plot
6. 3D/surface plot
7. Diagnostics/polish items
