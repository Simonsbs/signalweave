# BasicProp Probe

Development-only tool for extracting reference behavior from `basicProp-1.3.jar` without using the Swing UI manually.

## Requirements

- Java 17+
- local reference JAR at `/home/simon/temp/BasicProp/basicProp-1.3.jar`

## Compile

```bash
javac -cp /home/simon/temp/BasicProp/basicProp-1.3.jar tools/basicprop-probe/BasicPropProbe.java
```

## Commands

Print observed defaults from the live BasicProp classes:

```bash
java -cp /home/simon/temp/BasicProp/basicProp-1.3.jar:tools/basicprop-probe BasicPropProbe defaults
```

Run a deterministic experiment from a probe file:

```bash
java -cp /home/simon/temp/BasicProp/basicProp-1.3.jar:tools/basicprop-probe BasicPropProbe run tools/basicprop-probe/examples/xor-forward.bppr
```

`run` now emits two kinds of outputs:

- `outputs` / `hiddenActivations`: BasicProp's built-in helper methods
- `traceOutputs` / `traceHiddenActivations` / `traceError`: a reset-aware per-pattern trace captured from one consistent execution path

For SRNs, prefer the `trace*` fields when comparing hidden-state behavior, because BasicProp's `getHiddenActs()` does not apply reset handling the same way `getOutputsAsList()` does.

Current checked-in feed-forward training probes:

- `examples/ff2-train-single.bppr`
- `examples/ff3-train-single.bppr`
- `examples/ff3-train-batch.bppr`
- `examples/ff3-train-cross-entropy.bppr`
- `examples/ff3-train-momentum.bppr`
- `examples/ff3-train-online-multistep.bppr`
- `examples/ff3-stop-rule.bppr`
- `examples/ff4-train-single.bppr`

Current checked-in SRN probes:

- `examples/srn-forward.bppr`
- `examples/srn-hidden-bias-lifecycle.bppr`
- `examples/srn-leading-reset.bppr`
- `examples/srn-test-one.bppr`
- `examples/srn-train-sequential.bppr`
- `examples/srn-train-batch.bppr`

## Probe file format

Header fields:

- `type=FeedForward` or `type=SRN`
- `layers=2,2,1`
- `biases=true,true,false`
- `learningRate=0.3`
- `momentum=0.8`
- `steps=0`
- `batchUpdate=false`
- `crossEntropy=false`
- `sequentialUpdate=false` for SRN only
- `testOneIndex=<n>` to emit the direct `testOnePattern(...)` output for a selected pattern without changing the main `testAll()` capture

Pattern block:

```text
patterns:
0,0 => 0
0,1 => 1
reset
1,0 => 1
```

`reset` follows BasicProp's actual loader behavior: it resets the recurrent context after the previous pattern, even though it appears on the line before the next pattern.

Weight blocks:

- `weights <layerIndex>:` where `layerIndex` is zero-based
- rows map to destination units in that weight layer
- columns map to source units, including bias at column `0`

Example:

```text
weights 0:
0.5,0.2,-0.1
-0.4,0.3,0.7
```

SRN recurrent weights:

```text
recweights:
0.1,-0.2
0.3,0.4
```
