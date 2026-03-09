# SignalWeave

SignalWeave is a cross-platform `.NET 8` desktop and CLI alternative to BasicProp. The project is open source, runs on Windows, Linux, and macOS through Avalonia, and is built around a compatibility-first core so the original BasicProp workflows can be reproduced without locking the project to a single OS.

## Why this shape

Rebuilding every BasicProp screen and workflow in one pass would produce a brittle prototype. SignalWeave instead starts with the parts that matter for long-term parity:

- BasicProp-style text configuration parsing
- text pattern files with sequence resets for simple recurrent networks
- feed-forward and simple recurrent network execution
- training controls for learning rate, momentum, update mode, random range, and stopping rules
- saved weight snapshots
- output testing and hierarchical clustering over model activations
- a desktop shell plus a scriptable CLI

## Current parity snapshot

Implemented now:

- feed-forward and simple recurrent network definitions
- config parser with aliases for common BasicProp-style field names
- pattern parser with `reset` sequence markers
- training with online (`pattern`) and batch updates
- sum-squared-error and cross-entropy output losses
- error history suitable for plotting
- test-all and test-one execution
- weight save/load
- output and hidden-state hierarchical clustering
- desktop workbench for demo loading, parsing, training, testing, and clustering

Still to build for full feature parity:

- exact BasicProp file-format compatibility for legacy weight files
- desktop plotting panes and matrix visualizations that mirror the original UX
- interactive weight editing
- import of original BasicProp examples if the retired artifacts are recovered

## Projects

- `src/SignalWeave.Core`: parsers, engine, clustering, sample assets
- `src/SignalWeave.Cli`: terminal workflow
- `src/SignalWeave.Desktop`: Avalonia desktop app
- `tests/SignalWeave.Core.Tests`: parser and trainer tests

## CLI examples

```bash
dotnet run --project src/SignalWeave.Cli -- summary --network samples/xor.swcfg --patterns samples/xor.pat
dotnet run --project src/SignalWeave.Cli -- train --network samples/xor.swcfg --patterns samples/xor.pat --weights weights.json --seed 42
dotnet run --project src/SignalWeave.Cli -- test-all --network samples/xor.swcfg --patterns samples/xor.pat --weights weights.json
dotnet run --project src/SignalWeave.Cli -- cluster --network samples/xor.swcfg --patterns samples/xor.pat --weights weights.json --mode outputs
```

## Desktop

```bash
dotnet run --project src/SignalWeave.Desktop
```

The desktop app ships with built-in XOR and SRN demos so it can be exercised immediately after clone.

## Samples

The `samples/` directory contains starter network and pattern files:

- `xor.swcfg` and `xor.pat`
- `echo-srn.swcfg` and `echo-srn.pat`

## Compatibility note

SignalWeave targets the BasicProp feature model described on `basicprop.wordpress.com` as of March 9, 2026. The original downloadable JAR and weight-format reference were not publicly reachable during implementation, so legacy binary-level compatibility is the remaining gap.
