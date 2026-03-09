# BasicProp Compatibility Target

Source basis used for this implementation:

- https://basicprop.wordpress.com/
- https://basicprop.wordpress.com/configure/
- https://basicprop.wordpress.com/details/
- https://basicprop.wordpress.com/example/
- https://basicprop.wordpress.com/interacting/
- https://basicprop.wordpress.com/patterns/
- https://basicprop.wordpress.com/weights/

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

## What SignalWeave already covers

- Config and pattern parsing
- Core engine for feed-forward and SRN execution
- Training and testing workflows
- Weight persistence
- Hierarchical clustering over outputs and hidden activations

## Remaining parity work

- Recover and support original BasicProp weight-file format
- Recreate the original graph and weight-grid visual panels in the desktop app
- Add manual weight editing and richer experiment management
