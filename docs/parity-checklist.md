# BasicProp 1.3 Parity Checklist

Status legend:

- `[done]` implemented and verified
- `[in-progress]` implemented partially or not yet JAR-verified
- `[todo]` missing
- `[blocked]` cannot be closed until a dependency is completed

## Engine

- `[done]` feed-forward 2-layer execution
- `[done]` feed-forward 3-layer execution exists
- `[done]` feed-forward 4-layer execution exists in the core
- `[done]` feed-forward online training parity
- `[done]` feed-forward batch update parity
- `[done]` feed-forward cross-entropy parity
- `[done]` feed-forward momentum parity
- `[done]` feed-forward stop-rule parity
- `[done]` SRN sequential training parity
- `[done]` SRN hidden-state reset parity
- `[done]` SRN hidden-bias lifecycle parity
- `[done]` per-pattern output parity
- `[done]` test-all aggregate error parity
- `[done]` saved-weight parity fixtures
- `[in-progress]` JAR-backed golden parity suite

## Topologies

- `[done]` feed-forward 2-layer topology
- `[done]` feed-forward 3-layer topology
- `[done]` feed-forward 4-layer topology
- `[done]` SRN topology
- `[done]` 4-layer topology support in persistence
- `[done]` 4-layer topology support in diagrams and utilities

## Network configuration dialog

- `[done]` feed-forward tab structure
- `[done]` SRN tab structure
- `[done]` feed-forward 2-layer apply path in the desktop dialog
- `[done]` feed-forward 4-layer apply path in the desktop dialog
- `[done]` layer-count slider behavior
- `[done]` layer bias toggles
- `[done]` separate topology dialog from main control-panel training settings
- `[done]` screenshot-verified visual parity

## Main window

- `[done]` menu bar
- `[done]` main workbench shell
- `[done]` control panel defaults
- `[done]` `Train` / `continue` state
- `[done]` progress bar state
- `[done]` pattern combo `< 24` handling
- `[done]` full `SimControl.checkControls()` parity
- `[todo]` BasicProp-identical network drawing
- `[done]` detached message window
- `[done]` modal invalid-value dialogs
- `[done]` `SimControl.checkPatternsAvailable()` note/modal routing
- `[done]` error-progress chart

## Weights workflows

- `[done]` Hinton-style weight window
- `[done]` SignalWeave-native weight persistence
- `[done]` separate FF and SRN weight-load workflows
- `[done]` layer switching
- `[done]` refresh behavior
- `[done]` screenshot-verified visual parity

## Patterns workflows

- `[done]` pattern parsing with `reset`
- `[done]` desktop pattern loading
- `[done]` pattern plot window
- `[done]` patterns/outputs viewing
- `[todo]` exact workflow and formatting parity

## Utilities

- `[done]` 3D plotting workflow
- `[done]` time-series plotting workflow
- `[done]` hidden activation export
- `[done]` output clustering
- `[done]` hidden-state clustering
- `[todo]` JAR-backed utility parity verification

## File workflows

- `[done]` custom SignalWeave project/file formats are allowed
- `[done]` network save/load exists
- `[done]` pattern load exists
- `[done]` weight save/load exists
- `[done]` stable v1 project schema
- `[done]` checkpoint schema
- `[done]` SignalWeave-native schema documentation

## Release engineering

- `[done]` Linux build validation
- `[done]` Windows build validation
- `[done]` macOS build validation
- `[done]` packaged desktop binaries
- `[in-progress]` GitHub release workflow
- `[done]` final parity sign-off script

## Release gate

SignalWeave reaches BasicProp-equivalent status only when:

- every item above is `[done]`
- no topology advertised by the UI is unsupported by the engine
- golden parity tests pass against `basicProp-1.3.jar`
