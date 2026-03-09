# BasicProp 1.3 Engine Notes

This note captures direct observations from decompiling `basicProp-1.3.jar` and probing it headlessly. It is intended to reduce ambiguity before engine-parity work begins in SignalWeave.

Reference artifact:

- `/home/simon/temp/BasicProp/basicProp-1.3.jar`

## Core classes

- `basicProp.FeedForwardNetwork`
- `basicProp.SrnNetwork`
- `basicProp.NetSpec`
- `basicProp.FeedForwardSpec`
- `basicProp.SrnSpec`
- `basicProp.PatternConfig`
- `basicProp.SimControl`
- `basicProp.ConfigPanel`

## Confirmed engine behavior

### Activation

- Both feed-forward and SRN layers use logistic sigmoid activation:
  - `1 / (1 + exp(-sum))`

### Error metric

- `testAll()` accumulates raw squared error across outputs:
  - `sum((target - output)^2)`
- Final reported test error is:
  - `sqrt(totalSquaredError / numberOfPatterns)`
- Training stores per-cycle TSQ values as:
  - `sqrt(sum((target - output)^2))`

### Output delta

- With `crossEntropy = true`:
  - output delta is `target - output`
- With `crossEntropy = false`:
  - output delta is `(target - output) * output * (1 - output)`

### Hidden delta

- Hidden units use:
  - `hidden * (1 - hidden) * downstreamWeightedDelta`
- No derivative offset was found in the BasicProp 1.3 bytecode.

### Feed-forward training order

- Online feed-forward training chooses the current pattern randomly each cycle:
  - `abs(new Random().nextInt()) % numPatterns`
- Batch feed-forward training walks patterns sequentially and applies accumulated updates at the end of each full pattern pass.

### SRN training order

- SRN training advances sequentially from `PatternConfig.lastTrainedIx`.
- `lastTrainedIx` is updated after training completes.
- SRN respects `reset` markers in the pattern config.

### SRN recurrent-state handling

- SRN `testAll()` resets hidden state after patterns marked with `reset`.
- SRN training also resets hidden state after cycles whose current pattern is marked with `reset`.
- SRN `feedForward()` copies the previous hidden activations before computing the new hidden state and uses those saved values for recurrent contributions.
- In pattern files, `reset` marks the previous pattern index, not the next one.
  - A leading `reset` is effectively ignored.
- SRN hidden-layer bias does not behave like feed-forward hidden bias.
  - Training starts with the hidden bias available.
  - Once `resetHidden()` is called, the hidden-layer bias is also zeroed.
  - `getOutputsAsList()` and `testAll()` can therefore diverge if one call follows the other.

### Stop rule

- Feed-forward early stop threshold:
  - `sqrt(0.01) * outputUnits`
- SRN early stop threshold:
  - `0.01 * outputUnits`
- In both cases, BasicProp checks:
  - `spec.getMaxTSQInLastN(10 * numberOfPatterns) < threshold`
- On success it aborts training and writes:
  - `Training goal reached prematurely`

### Weight initialization

- Weight initialization uses `new Random()` with no explicit seed.
- Selected weight range depends on UI combo-box index:
  - `0` -> `[-0.1, 0.1]`
  - `1` -> `[-1, 1]`
  - `2` -> `[-10, 10]`
- Generated value formula:
  - `(random.nextDouble() - 0.5) * 2 * range`

### Bias handling

- Weight matrices include a `fromIndex = 0` slot for bias.
- When a layer bias is disabled, BasicProp skips bias input by starting from `fromIndex = 1`.

## Confirmed UI defaults

From `basicProp.SimControl`:

- learning rate options:
  - `0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 5.0`
- default learning rate:
  - `0.3`
- momentum options:
  - `0, 0.2, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0`
- default momentum:
  - `0.8`
- learning steps options:
  - `100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000, 100000`
- default learning steps:
  - `5000`
- weight range options:
  - `-0.1 - 0.1`
  - `-1 - 1`
  - `-10 - 10`
- default weight range:
  - `-1 - 1`
- progress bar string at cycle zero:
  - `Untrained`

## Pattern UI behavior

- `SimControl.fillPatterns()` populates individual patterns only when `numberOfPatterns < 24`.
- At `24` or more patterns:
  - the combo box shows only the pattern filename
- A live BasicProp 1.3 controller check shows:
  - at exactly `24` patterns, `Test one` remains enabled even though the combo box has collapsed to the filename
  - above `24` patterns, `Test one` is disabled

## Configuration constraints

From `SpecStub` and config code:

- maximum layers:
  - feed-forward: `4`
  - SRN: exactly `3`
- maximum units per layer:
  - `10`
- pattern limit:
  - `5000`

## Implications for SignalWeave

- Remove the current sigmoid-derivative offset from the parity engine path.
- Feed-forward online mode must support random pattern sampling.
- SRN parity must preserve sequential progression and `lastTrainedIx`.
- SRN parity must model BasicProp's non-standard hidden-bias lifecycle separately from feed-forward behavior.
- Leading-`reset` behavior is now covered by a probe-backed regression fixture in SignalWeave rather than only by a decompile note.
- SRN hidden-bias lifecycle is now covered by a probe-backed training fixture that crosses a reset boundary with repeated inputs.
- Golden parity tests should not rely on BasicProp random initialization.
  - Instead, set explicit starting weights or capture saved initial weights.
