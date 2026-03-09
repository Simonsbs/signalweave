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
- `[in-progress]` feed-forward online training parity
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
- `[in-progress]` 4-layer topology support in diagrams and utilities

## Network configuration dialog

- `[in-progress]` feed-forward tab structure
- `[in-progress]` SRN tab structure
- `[done]` feed-forward 2-layer apply path in the desktop dialog
- `[done]` feed-forward 4-layer apply path in the desktop dialog
- `[in-progress]` layer-count slider behavior
- `[in-progress]` layer bias toggles
- `[done]` separate topology dialog from main control-panel training settings
- `[todo]` screenshot-verified visual parity

## Main window

- `[done]` menu bar
- `[in-progress]` main workbench shell
- `[in-progress]` control panel defaults
- `[done]` `Train` / `continue` state
- `[done]` progress bar state
- `[in-progress]` pattern combo `< 24` handling
- `[todo]` full `SimControl.checkControls()` parity
- `[todo]` BasicProp-identical network drawing
- `[in-progress]` detached message window
- `[done]` modal invalid-value dialogs
- `[done]` `SimControl.checkPatternsAvailable()` note/modal routing
- `[in-progress]` error-progress chart

## Weights workflows

- `[in-progress]` Hinton-style weight window
- `[done]` SignalWeave-native weight persistence
- `[done]` separate FF and SRN weight-load workflows
- `[in-progress]` layer switching
- `[in-progress]` refresh behavior
- `[todo]` screenshot-verified visual parity

## Patterns workflows

- `[done]` pattern parsing with `reset`
- `[in-progress]` desktop pattern loading
- `[in-progress]` pattern plot window
- `[in-progress]` patterns/outputs viewing
- `[todo]` exact workflow and formatting parity

## Utilities

- `[in-progress]` 3D plotting workflow
- `[in-progress]` time-series plotting workflow
- `[done]` hidden activation export
- `[in-progress]` output clustering
- `[in-progress]` hidden-state clustering
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
