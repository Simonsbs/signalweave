# SignalWeave Native Schemas

SignalWeave uses stable JSON documents for its native project and checkpoint formats.

## Project schema

- schema id: `signalweave-project/v2`
- suggested extension: `.swproj.json`
- purpose: store a runnable SignalWeave workspace with embedded network definition, patterns, optional weights, completed-cycle count, and optional UI workspace state

Top-level fields:

- `schema`
- `definition`
- `patterns`
- `weights` optional
- `completedCycles`
- `workspace` optional

Workspace fields:

- `learningSteps`
- `selectedPatternIndex`
- `errorPlotDisplayMode`

Each pattern entry stores:

- `label`
- `inputs`
- `targets` optional
- `resetsContextAfter`

## Checkpoint schema

- schema id: `signalweave-checkpoint/v1`
- suggested extension: `.swcheckpoint.json`
- purpose: store a resumable training or evaluation snapshot with embedded weights and cycle count

Top-level fields:

- `schema`
- `definition`
- `patterns`
- `weights`
- `completedCycles`
- `savedAtUtc`

## CLI workflows

Create a project:

```bash
dotnet run --project src/SignalWeave.Cli -- pack-project \
  --network samples/xor.swcfg \
  --patterns samples/xor.pat \
  --output xor.swproj.json
```

Create a checkpoint:

```bash
dotnet run --project src/SignalWeave.Cli -- pack-checkpoint \
  --network samples/xor.swcfg \
  --patterns samples/xor.pat \
  --weights xor.weights.json \
  --output xor.swcheckpoint.json \
  --cycles 5000
```

Use a project or checkpoint directly:

```bash
dotnet run --project src/SignalWeave.Cli -- summary --project xor.swproj.json
dotnet run --project src/SignalWeave.Cli -- train --project xor.swproj.json --checkpoint-out xor-trained.swcheckpoint.json
dotnet run --project src/SignalWeave.Cli -- test-all --checkpoint xor-trained.swcheckpoint.json
dotnet run --project src/SignalWeave.Cli -- cluster --project xor.swproj.json --mode hidden
```

## Desktop workflows

The desktop apps expose the same native schema types, but the workflow differs by product line:

- `Classic`
  - still exposes both project and checkpoint workflows
  - `Load Project`
  - `Save Project`
  - `Load Checkpoint`
  - `Save Checkpoint`

- `Modern`
  - uses a single project-driven workflow from `File`
  - `New Project`
  - `Load Project`
  - `Save Project`
  - `Save Project As`

Projects now embed the current network, pattern set, current weight snapshot, completed-cycle count, and optional workspace state. Checkpoints remain available for the Classic line and CLI.

Compatibility:

- `signalweave-project/v2` is the current save format
- legacy `signalweave-project/v1` documents still load
