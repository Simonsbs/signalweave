# SignalWeave Native Schemas

SignalWeave uses stable JSON documents for its native project and checkpoint formats.

## Project schema

- schema id: `signalweave-project/v1`
- suggested extension: `.swproj.json`
- purpose: store a runnable SignalWeave workspace with embedded network definition, patterns, and optional weights

Top-level fields:

- `schema`
- `definition`
- `patterns`
- `weights` optional

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

The desktop app exposes the same native schema types under `Network`:

- `Load Project`
- `Save Project`
- `Load Checkpoint`
- `Save Checkpoint`

Projects embed the current network, pattern set, and current weight snapshot. Checkpoints additionally persist the completed-cycle count.
