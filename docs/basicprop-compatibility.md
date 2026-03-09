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
- Training and testing workflows
- Weight persistence
- Hierarchical clustering over outputs and hidden activations
- BasicProp-style desktop shell with:
  - menu structure
  - control panel defaults
  - test-one/test-all interaction
  - current weight inspection
  - live network diagram

## Remaining parity work

- Match BasicProp 1.3 engine behavior exactly from the runnable JAR
- Recreate the original graph and weight-grid visual panels in the desktop app
- Recreate utility workflows for 3D plotting, time series plots, and hidden activation export
- Add parity regression fixtures using the local BasicProp reference bundle

## Scope decision

- Exact BasicProp file compatibility is not required.
- SignalWeave may use its own project, dataset, and checkpoint formats.
- Exact behavioral parity remains the target for:
  - training results
  - testing outputs
  - plots and visual analysis
  - user workflows
