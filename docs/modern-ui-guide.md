# Modern UI Guide

This guide covers the current `Modern` desktop workflow in SignalWeave.

## What Modern is

`Modern` is the project-file-first SignalWeave desktop line:

- one project file stores network settings, patterns, weights, training history, and workspace state
- no separate load/save flows for network settings, weights, or patterns
- the main workspace is split into:
  - left: `Network Graph`, `Weights`, `Analysis`
  - right: `Network`, `Training`, `Tests`, `Patterns`, `Summary`
  - bottom: `Console` and `Error graph`

Desktop project:

- `src/SignalWeave.Modern.Desktop`

## Main areas

### Top toolbar

- `✚ New`: create a fresh project
- `📂 Load`: open a `.swproj.json` project
- `💾 Save`: save the current project
- `🖫 Save As`: save the current project to a new file

### Left workspace tabs

- `Network Graph`: live topology, weights, and `Test one` activation overlay
- `Weights`: matrix-based weight inspector for current weights or saved training checkpoints
- `Analysis`: clustering, compatibility summary, time series, and surface plotting

### Right workflow tabs

- `Network`: project name, network type, topology, bias, and weight range
- `Training`: learning controls, training sessions, and rollback
- `Tests`: per-pattern testing, detailed pattern inspector, and hidden-activation export
- `Patterns`: text editor and graphic table editor for pattern data
- `Summary`: project/run summary text

### Bottom panels

- `Console`: Markdown-style structured log output with copy/save
- `Error graph`: training error points across the current or selected training session

## Step-by-step: load and use a sample

Use the built-in sample project:

- `samples/seventeen-patterns-7x7-modern.swproj.json`

### 1. Start Modern

From the repo root:

```bash
dotnet run --project src/SignalWeave.Modern.Desktop
```

### 2. Load the sample

1. Click `📂 Load`
2. Open:

```text
samples/seventeen-patterns-7x7-modern.swproj.json
```

3. Confirm the UI updates:
- `Network Graph` shows the loaded topology
- `Patterns` contains the 17 embedded patterns
- `Network` shows the project’s saved topology/settings

### 3. Inspect the loaded project

1. Open the `Network` tab on the right
2. Review:
- project name
- network type
- inputs / hidden layers / outputs
- bias toggles
- weight range

3. Open the `Patterns` tab
4. Switch between:
- `Text mode`
- `Graphic mode`

Changes in one editor are mirrored into the other.

### 4. Train the sample

1. Open the `Training` tab
2. Review:
- learning rate
- momentum
- learning steps
- error threshold
- batch update
- cross-entropy

3. Click `Train #1`

What to expect:

- the error graph starts plotting training points
- the console logs the training run
- a training session entry appears in the training-session list

4. Click `Train #2` if you want to continue from the current weights

That does not restart the model. It continues training from the current state.

### 5. Roll back a training session

1. Stay in `Training`
2. Select a saved `Train #N` entry
3. Click `Rollback`

This restores:

- the weights from that training session
- the associated error-history view for that session

### 6. Test patterns

1. Open the `Tests` tab
2. Click `Test all`

What to expect:

- each pattern gets a cached result
- pass/fail markers update on the pattern list
- the inspector updates for the selected pattern

3. Click a pattern in the list

What to expect:

- the network graph shows that pattern’s last cached test result
- the inspector shows:
  - inputs
  - targets
  - outputs
  - delta
  - hidden activations

4. To test only one pattern, select it and click `Test selected`

### 7. Inspect weights

1. Open the left `Weights` tab
2. Choose:
- `Source`: `Current` or a saved `Train #N`
- `Layer`
- `View`: `Heatmap`, `Magnitude`, or `Values`

Use this to understand what changed between training sessions.

### 8. Run analysis

1. Open the left `Analysis` tab
2. Choose a mode:
- `Output clustering`
- `Hidden-state clustering`
- `Compatibility summary`
- `Time series`
- `Surface plot`

3. Choose `Source`:
- `Current`
- or a saved `Train #N`

4. Click `Run`

Mode notes:

- `Time series`: choose `Input`, `Target`, `Output`, or `Hidden`, then choose the index
- `Surface plot`: choose `X input`, `Y input`, `Z series`, and the index

Exports:

- `⧉` copies the current analysis output
- `🖫` saves report text or CSV data
- `🖼` saves the analysis chart as PNG for chart modes

### 9. Export useful artifacts

Console:

- use `⧉` to copy the full console
- use `🖫` to save the console as `.md`

Network graph:

- use the `🖫` button in `Network Graph` to save PNG

Error graph:

- use the `🖫` button in the error panel to save PNG

Hidden activations:

- in `Tests`, use:
  - `Export selected`
  - `Export set`

### 10. Save the project

1. Click `💾 Save` or `🖫 Save As`
2. Save as a `.swproj.json` file

The saved project keeps:

- network settings
- patterns
- current weights
- completed cycles
- training-session snapshots
- workspace state needed by Modern

## Recommended first-use workflow

For a new user, this is the shortest useful loop:

1. Load `samples/seventeen-patterns-7x7-modern.swproj.json`
2. Open `Training`
3. Click `Train #1`
4. Open `Tests`
5. Click `Test all`
6. Click a few patterns in the list
7. Open `Weights`
8. Compare `Current` vs a saved `Train #N`
9. Open `Analysis`
10. Run `Output clustering`, then `Time series`, then `Surface plot`
11. Save the project

## Tips

- If you change topology in `Network`, the graph updates immediately.
- If you shrink inputs/outputs/hidden layers, stale test visuals are cleared automatically.
- Training sessions are intended to be user-visible checkpoints, not hidden internals.
- `Train #N` means “train one more block from the current weights.”

## Sample files

Relevant samples currently in the repo:

- `samples/seventeen-patterns-7x7-modern.swproj.json`
- `samples/seventeen-patterns-7x7.swproj.json`
- `samples/xor.swcfg`
- `samples/xor.pat`
- `samples/echo-srn.swcfg`
- `samples/echo-srn.pat`
