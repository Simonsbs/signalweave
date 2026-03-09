# BasicProp 1.3 Parity Checklist

Status legend:

- `[done]` implemented and verified
- `[in-progress]` partially implemented or not yet verified against the JAR
- `[todo]` not implemented

## Engine

- `[in-progress]` feed-forward training parity
- `[in-progress]` SRN training parity
- `[in-progress]` batch update semantics
- `[in-progress]` cross-entropy semantics
- `[in-progress]` learning-rate behavior
- `[in-progress]` momentum behavior
- `[in-progress]` learning-step stopping behavior
- `[in-progress]` weight-range initialization behavior
- `[in-progress]` per-pattern test output parity
- `[in-progress]` test-all aggregate error parity
- `[todo]` golden parity tests against the JAR

## Project and data model

- `[done]` custom SignalWeave project format is allowed
- `[done]` custom sample config and pattern formats exist
- `[todo]` stable v1 project schema
- `[todo]` checkpoint schema
- `[todo]` export/import docs for SignalWeave-native files

## Main window

- `[in-progress]` menu bar exists
- `[in-progress]` main network workbench exists
- `[todo]` network drawing matches BasicProp layout behavior
- `[todo]` control panel fully mirrors BasicProp
- `[todo]` console panel mirrors BasicProp messaging
- `[todo]` live error-progress chart in the main window

## Network configuration

- `[todo]` feed-forward configuration dialog parity
- `[todo]` SRN configuration dialog parity
- `[todo]` layer-count sliders and enable/disable behavior
- `[todo]` bias toggles and persistence

## Control panel workflows

- `[in-progress]` parse/train/test flow exists
- `[todo]` `Reset` parity
- `[todo]` `Train` and `Continue` button-state parity
- `[todo]` progress bar parity
- `[todo]` pattern combo-box behavior
- `[todo]` `Test one` parity
- `[todo]` `Test all` parity

## Weights workflows

- `[todo]` Hinton-style weight display parity
- `[done]` weight persistence exists in SignalWeave-native form
- `[todo]` weight refresh / layer switching parity
- `[todo]` FF and SRN weight workflow parity

## Patterns workflows

- `[done]` pattern parsing with `reset`
- `[todo]` load patterns desktop flow parity
- `[todo]` patterns and outputs viewer parity

## Utility workflows

- `[todo]` 3D plotting parity
- `[todo]` time series plot parity
- `[todo]` hidden activation export parity
- `[in-progress]` hidden activations are available in the core

## Release engineering

- `[done]` .NET 8 solution builds on Linux
- `[todo]` Windows validation
- `[todo]` macOS validation
- `[todo]` packaged desktop releases
- `[todo]` GitHub release workflow
