# Reference Assets

This document inventories the current local BasicProp reference bundle that SignalWeave will use for parity work.

Local path:

- `/home/simon/temp/BasicProp`

## Executable reference

- `basicProp-1.3.jar`
  - main class: `basicProp.BasicProp`
  - notable classes discovered from the JAR:
    - `basicProp.FeedForwardNetwork`
    - `basicProp.SrnNetwork`
    - `basicProp.ConfigPanel`
    - `basicProp.SimControl`
    - `basicProp.SimDisplay`
    - `basicProp.SimErrorDisplay`
    - `basicProp.JHintonFrame`
    - `basicProp.PatternPlot`
    - `basicProp.TimeSeriesPlotter`
    - `basicProp.SurfacePlotter`
    - `basicProp.FileIO`

## Screenshot inventory

- `Screenshot 2026-03-09 125927.png`
  - main window
  - menu bar
  - network canvas
  - control panel
  - console
  - error progress panel
- `Screenshot 2026-03-09 125951.png`
  - feed-forward network configuration dialog
- `Screenshot 2026-03-09 130008.png`
  - SRN configuration dialog
- `Screenshot 2026-03-09 130030.png`
  - weight visualization window
- `Screenshot 2026-03-09 130059.png`
  - `Network` menu
- `Screenshot 2026-03-09 130111.png`
  - `Weights` menu
- `Screenshot 2026-03-09 130125.png`
  - `Patterns` menu
- `Screenshot 2026-03-09 130137.png`
  - `Utilities` menu

## Observed menu structure

- `Network`
  - `Configure Network`
  - `Load Network`
  - `Save Network`
- `Weights`
  - `Show Weights`
  - `Load Weights (FF)`
  - `Load Weights (SRN)`
  - `Save Weights`
- `Patterns`
  - `Show Patterns and Outputs`
  - `Load Patterns`
- `Utilities`
  - `3D Plotting`
  - `Time Series Plot`
  - `Export hidden units activations`
- `Help`

## Known gaps in the bundle

- No example datasets or saved weight files have been added yet.
- No screenshots currently show:
  - patterns/output viewer
  - 3D plotting window
  - time series plot window
  - help/about dialog
- No golden run outputs have been captured yet.

## Immediate next actions

- Add at least one feed-forward and one SRN golden experiment captured from the JAR.
- Capture screenshots for all utility windows and pattern/output inspection screens.
- Decompile engine classes and document update semantics in a dedicated design note.

## Probe tooling

- SignalWeave now includes a headless BasicProp probe:
  - `/home/simon/signalweave/tools/basicprop-probe/BasicPropProbe.java`
- Purpose:
  - instantiate BasicProp without the full UI workflow
  - print live defaults from `SimControl`
  - run deterministic experiments from probe files with explicit weights
