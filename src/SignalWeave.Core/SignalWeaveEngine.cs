namespace SignalWeave.Core;

public sealed class SignalWeaveEngine
{
    private readonly NetworkDefinition _definition;
    private readonly Random _patternRandom;
    private readonly double[,] _previousInputHiddenDelta;
    private readonly double[,] _previousHiddenOutputDelta;
    private readonly double[,]? _previousHiddenHiddenDelta;
    private readonly double[,]? _previousRecurrentDelta;
    private double[]? _trainingHiddenContext;
    private double _trainingHiddenBiasValue;
    private double[]? _visibleHiddenContext;
    private double _visibleHiddenBiasValue;
    private int _lastTrainedPatternIndex;

    public SignalWeaveEngine(NetworkDefinition definition, WeightSet? weights = null, int? seed = null)
    {
        _definition = definition;
        _definition.Validate();
        _patternRandom = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        Weights = weights?.Clone() ?? WeightSet.CreateRandom(_definition, seed);
        _previousInputHiddenDelta = new double[Weights.InputHidden.GetLength(0), Weights.InputHidden.GetLength(1)];
        _previousHiddenOutputDelta = new double[Weights.HiddenOutput.GetLength(0), Weights.HiddenOutput.GetLength(1)];
        _previousHiddenHiddenDelta = Weights.HiddenHidden is null
            ? null
            : new double[Weights.HiddenHidden.GetLength(0), Weights.HiddenHidden.GetLength(1)];
        _previousRecurrentDelta = Weights.RecurrentHidden is null
            ? null
            : new double[Weights.RecurrentHidden.GetLength(0), Weights.RecurrentHidden.GetLength(1)];
        _trainingHiddenContext = _definition.NetworkKind == NetworkKind.SimpleRecurrent
            ? new double[_definition.HiddenUnits]
            : null;
        _trainingHiddenBiasValue = _definition.UseHiddenBias ? 1.0 : 0.0;
        _visibleHiddenContext = _definition.NetworkKind == NetworkKind.SimpleRecurrent
            ? new double[_definition.HiddenUnits]
            : null;
        _visibleHiddenBiasValue = _definition.NetworkKind == NetworkKind.SimpleRecurrent && _definition.UseHiddenBias ? 1.0 : 0.0;
    }

    public WeightSet Weights { get; private set; }
    public int CompletedCycles { get; private set; }

    public void RestoreCompletedCycles(int cycles)
    {
        CompletedCycles = Math.Max(cycles, 0);
    }

    public TrainResult Train(PatternSet patternSet, int? maxEpochs = null)
    {
        patternSet.ValidateAgainst(_definition, requireTargets: true);

        var steps = maxEpochs ?? _definition.MaxEpochs;
        var history = new List<TrainingPoint>(steps);

        if (_definition.NetworkKind == NetworkKind.FeedForward)
        {
            TrainFeedForward(patternSet, steps, history);
        }
        else
        {
            TrainSimpleRecurrent(patternSet, steps, history);
        }

        CompletedCycles += history.Count;
        SyncVisibleStateFromTraining();
        var finalRun = TestAll(patternSet);
        return new TrainResult(history, Weights.Clone(), finalRun);
    }

    public RunResult TestAll(PatternSet patternSet)
    {
        patternSet.ValidateAgainst(_definition, requireTargets: false);

        var results = new List<TestResult>(patternSet.Examples.Count);
        var totalSquaredError = 0.0;
        var labeled = 0;
        var context = new double[_definition.HiddenUnits];
        var hiddenBiasValue = _definition.NetworkKind == NetworkKind.SimpleRecurrent
            ? 0.0
            : _definition.UseHiddenBias ? 1.0 : 0.0;

        for (var index = 0; index < patternSet.Examples.Count; index++)
        {
            var example = patternSet.Examples[index];
            var step = Forward(
                example.Inputs,
                _definition.NetworkKind == NetworkKind.SimpleRecurrent ? context : Array.Empty<double>(),
                _definition.NetworkKind == NetworkKind.SimpleRecurrent ? hiddenBiasValue : 1.0);

            var error = 0.0;
            if (example.Targets is not null)
            {
                var squaredError = CalculateSquaredError(step.Outputs, example.Targets);
                error = ComputeTsq(squaredError);
                totalSquaredError += squaredError;
                labeled++;
            }

            results.Add(new TestResult(index, example.Label, example.Inputs, step.Outputs, step.HiddenActivations, example.Targets, error));

            if (_definition.NetworkKind == NetworkKind.SimpleRecurrent)
            {
                if (example.ResetsContextAfter)
                {
                    Array.Clear(context, 0, context.Length);
                    hiddenBiasValue = 0.0;
                }
                else
                {
                    context = (double[])step.FinalHidden.Clone();
                }
            }
        }

        var averageError = labeled == 0 ? 0.0 : Math.Sqrt(totalSquaredError / labeled);
        var reportedAverageError = _definition.NetworkKind == NetworkKind.SimpleRecurrent
            ? MeasureReportedSrnAggregateError(patternSet)
            : averageError;
        return new RunResult(results, averageError, reportedAverageError);
    }

    public TestResult TestOne(PatternSet patternSet, int index)
    {
        patternSet.ValidateAgainst(_definition, requireTargets: false);

        if (index < 0 || index >= patternSet.Examples.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var example = patternSet.Examples[index];
        var context = _definition.NetworkKind == NetworkKind.SimpleRecurrent
            ? (_visibleHiddenContext is null ? new double[_definition.HiddenUnits] : (double[])_visibleHiddenContext.Clone())
            : Array.Empty<double>();
        var hiddenBiasValue = _definition.NetworkKind == NetworkKind.SimpleRecurrent
            ? _visibleHiddenBiasValue
            : 1.0;
        var step = Forward(example.Inputs, context, hiddenBiasValue);

        if (_definition.NetworkKind == NetworkKind.SimpleRecurrent)
        {
            if (example.ResetsContextAfter)
            {
                _visibleHiddenContext = new double[_definition.HiddenUnits];
                _visibleHiddenBiasValue = 0.0;
            }
            else
            {
                _visibleHiddenContext = (double[])step.FinalHidden.Clone();
            }
        }

        var error = 0.0;
        if (example.Targets is not null)
        {
            error = ComputeTsq(CalculateSquaredError(step.Outputs, example.Targets));
        }

        return new TestResult(index, example.Label, example.Inputs, step.Outputs, step.HiddenActivations, example.Targets, error);
    }

    public ClusterNode ClusterOutputs(PatternSet patternSet)
    {
        var run = TestAll(patternSet);
        return HierarchicalClusterer.Cluster(run.Results.Select(result => (result.Label, result.Outputs)).ToList());
    }

    public ClusterNode ClusterHiddenStates(PatternSet patternSet)
    {
        var run = TestAll(patternSet);
        return HierarchicalClusterer.Cluster(run.Results.Select(result => (result.Label, result.HiddenActivations)).ToList());
    }

    private void TrainSimpleRecurrent(PatternSet patternSet, int steps, List<TrainingPoint> history)
    {
        var examples = patternSet.Examples;
        var useSequentialUpdate = patternSet.HasResetMarkers;
        var useAccumulatedUpdates = _definition.UpdateMode == UpdateMode.Batch || useSequentialUpdate;
        var pendingInputHidden = new double[Weights.InputHidden.GetLength(0), Weights.InputHidden.GetLength(1)];
        var pendingHiddenOutput = new double[Weights.HiddenOutput.GetLength(0), Weights.HiddenOutput.GetLength(1)];
        var pendingRecurrent = Weights.RecurrentHidden is null
            ? null
            : new double[Weights.RecurrentHidden.GetLength(0), Weights.RecurrentHidden.GetLength(1)];
        var lastPatternIndex = _lastTrainedPatternIndex;
        _trainingHiddenContext ??= new double[_definition.HiddenUnits];

        for (var step = 1; step <= steps; step++)
        {
            lastPatternIndex = (_lastTrainedPatternIndex + step - 1) % examples.Count;
            var example = examples[lastPatternIndex];
            var previousHidden = (double[])_trainingHiddenContext.Clone();
            var forward = Forward(example.Inputs, previousHidden, _trainingHiddenBiasValue);
            var outputDelta = ComputeOutputDelta(forward.Outputs, example.Targets);
            var hiddenDelta = ComputeSingleHiddenDelta(forward.FinalHidden, outputDelta, Weights.HiddenOutput);

            if (useAccumulatedUpdates)
            {
                AccumulateScaledOuterProduct(pendingInputHidden, forward.AugmentedInputs, hiddenDelta, _definition.LearningRate);
                AccumulateScaledOuterProduct(pendingHiddenOutput, forward.AugmentedFinalHidden, outputDelta, _definition.LearningRate);

                if (pendingRecurrent is not null)
                {
                    AccumulateScaledOuterProduct(pendingRecurrent, previousHidden, hiddenDelta, _definition.LearningRate);
                }
            }
            else
            {
                ApplyImmediateWeights(Weights.InputHidden, forward.AugmentedInputs, hiddenDelta, _previousInputHiddenDelta);
                ApplyImmediateWeights(Weights.HiddenOutput, forward.AugmentedFinalHidden, outputDelta, _previousHiddenOutputDelta);

                if (Weights.RecurrentHidden is not null && _previousRecurrentDelta is not null)
                {
                    ApplyImmediateWeights(Weights.RecurrentHidden, previousHidden, hiddenDelta, _previousRecurrentDelta);
                }
            }

            var shouldFlushPending = useAccumulatedUpdates &&
                (example.ResetsContextAfter || (!useSequentialUpdate && step % examples.Count == 0));

            if (shouldFlushPending)
            {
                FlushPendingWeights(Weights.InputHidden, pendingInputHidden, _previousInputHiddenDelta);
                FlushPendingWeights(Weights.HiddenOutput, pendingHiddenOutput, _previousHiddenOutputDelta);

                if (Weights.RecurrentHidden is not null && pendingRecurrent is not null && _previousRecurrentDelta is not null)
                {
                    FlushPendingWeights(Weights.RecurrentHidden, pendingRecurrent, _previousRecurrentDelta);
                }
            }

            if (example.ResetsContextAfter)
            {
                Array.Clear(_trainingHiddenContext, 0, _trainingHiddenContext.Length);
                _trainingHiddenBiasValue = 0.0;
            }
            else
            {
                _trainingHiddenContext = (double[])forward.FinalHidden.Clone();
            }

            var currentTsq = ComputeTsq(CalculateSquaredError(forward.Outputs, example.Targets!));

            if (currentTsq <= _definition.ErrorThreshold)
            {
                history.Add(new TrainingPoint(step, currentTsq));
                break;
            }

            if (ShouldStopEarly(history, examples.Count))
            {
                break;
            }

            history.Add(new TrainingPoint(step, currentTsq));
        }

        _lastTrainedPatternIndex = lastPatternIndex;
    }

    private SequenceGradient ComputeSrnSequenceGradient(IReadOnlyList<PatternExample> sequence)
    {
        var steps = new ForwardStep[sequence.Count];
        var previousHiddenStates = new double[sequence.Count][];
        var context = new double[_definition.HiddenUnits];
        var totalSquaredError = 0.0;
        var labeledCount = 0;

        for (var index = 0; index < sequence.Count; index++)
        {
            previousHiddenStates[index] = (double[])context.Clone();
            steps[index] = Forward(sequence[index].Inputs, context, 1.0);
            context = steps[index].FinalHidden;

            if (sequence[index].Targets is not null)
            {
                totalSquaredError += CalculateSquaredError(steps[index].Outputs, sequence[index].Targets!);
                labeledCount++;
            }
        }

        var inputHiddenGradient = new double[Weights.InputHidden.GetLength(0), Weights.InputHidden.GetLength(1)];
        var hiddenOutputGradient = new double[Weights.HiddenOutput.GetLength(0), Weights.HiddenOutput.GetLength(1)];
        var recurrentGradient = Weights.RecurrentHidden is null
            ? null
            : new double[Weights.RecurrentHidden.GetLength(0), Weights.RecurrentHidden.GetLength(1)];
        var nextHiddenDelta = new double[_definition.HiddenUnits];

        for (var index = sequence.Count - 1; index >= 0; index--)
        {
            var example = sequence[index];
            var step = steps[index];
            var outputDelta = ComputeOutputDelta(step.Outputs, example.Targets);
            var hiddenDelta = new double[_definition.HiddenUnits];

            for (var hidden = 0; hidden < _definition.HiddenUnits; hidden++)
            {
                var signal = 0.0;

                for (var output = 0; output < _definition.OutputUnits; output++)
                {
                    signal += outputDelta[output] * Weights.HiddenOutput[hidden, output];
                }

                if (Weights.RecurrentHidden is not null)
                {
                    for (var next = 0; next < _definition.HiddenUnits; next++)
                    {
                        signal += nextHiddenDelta[next] * Weights.RecurrentHidden[hidden, next];
                    }
                }

                hiddenDelta[hidden] = signal * SigmoidDerivative(step.FinalHidden[hidden]);
            }

            AccumulateOuterProduct(inputHiddenGradient, step.AugmentedInputs, hiddenDelta);
            AccumulateOuterProduct(hiddenOutputGradient, step.AugmentedFinalHidden, outputDelta);

            if (recurrentGradient is not null)
            {
                AccumulateOuterProduct(recurrentGradient, previousHiddenStates[index], hiddenDelta);
            }

            nextHiddenDelta = hiddenDelta;
        }

        return new SequenceGradient(inputHiddenGradient, hiddenOutputGradient, recurrentGradient, null, totalSquaredError, labeledCount);
    }

    private FeedForwardGradient ComputeFeedForwardGradient(PatternExample example)
    {
        var step = Forward(example.Inputs, Array.Empty<double>(), 1.0);
        var outputDelta = ComputeOutputDelta(step.Outputs, example.Targets);

        if (_definition.IsDirectFeedForward)
        {
            return new FeedForwardGradient(
                step,
                new SequenceGradient(
                    CreateOuterProduct(step.AugmentedInputs, outputDelta),
                    new double[0, 0],
                    null,
                    null,
                    CalculateSquaredError(step.Outputs, example.Targets!),
                    example.Targets is null ? 0 : 1),
                Array.Empty<double>(),
                null,
                outputDelta);
        }

        if (_definition.HasSecondHiddenLayer)
        {
            var secondHiddenDelta = ComputeSingleHiddenDelta(step.FinalHidden, outputDelta, Weights.HiddenOutput);
            var firstHiddenDelta = ComputeSingleHiddenDelta(step.FirstHidden, secondHiddenDelta, Weights.HiddenHidden!);
            var hiddenHiddenGradient = new double[Weights.HiddenHidden!.GetLength(0), Weights.HiddenHidden.GetLength(1)];
            AccumulateOuterProduct(hiddenHiddenGradient, step.AugmentedFirstHidden!, secondHiddenDelta);

            return new FeedForwardGradient(
                step,
                new SequenceGradient(
                    CreateOuterProduct(step.AugmentedInputs, firstHiddenDelta),
                    CreateOuterProduct(step.AugmentedFinalHidden, outputDelta),
                    null,
                    hiddenHiddenGradient,
                    CalculateSquaredError(step.Outputs, example.Targets!),
                    example.Targets is null ? 0 : 1),
                firstHiddenDelta,
                secondHiddenDelta,
                outputDelta);
        }

        var hiddenDelta = ComputeSingleHiddenDelta(step.FinalHidden, outputDelta, Weights.HiddenOutput);
        return new FeedForwardGradient(
            step,
            new SequenceGradient(
                CreateOuterProduct(step.AugmentedInputs, hiddenDelta),
                CreateOuterProduct(step.AugmentedFinalHidden, outputDelta),
                null,
                null,
                CalculateSquaredError(step.Outputs, example.Targets!),
                example.Targets is null ? 0 : 1),
            hiddenDelta,
            null,
            outputDelta);
    }

    private ForwardStep Forward(double[] inputs, double[] context, double hiddenBiasValue)
    {
        var augmentedInputs = AppendBias(inputs, _definition.UseInputBias);

        if (_definition.IsDirectFeedForward)
        {
            var outputs = Multiply(augmentedInputs, Weights.InputHidden).Select(Sigmoid).ToArray();
            return new ForwardStep(
                augmentedInputs,
                Array.Empty<double>(),
                null,
                Array.Empty<double>(),
                Array.Empty<double>(),
                outputs,
                Array.Empty<double>());
        }

        var firstHiddenNet = Multiply(augmentedInputs, Weights.InputHidden);

        if (Weights.RecurrentHidden is not null)
        {
            AddInPlace(firstHiddenNet, Multiply(context, Weights.RecurrentHidden));
        }

        var firstHidden = firstHiddenNet.Select(Sigmoid).ToArray();

        if (_definition.HasSecondHiddenLayer)
        {
            var augmentedFirstHidden = AppendBias(firstHidden, _definition.UseHiddenBias);
            var secondHidden = Multiply(augmentedFirstHidden, Weights.HiddenHidden!).Select(Sigmoid).ToArray();
            var augmentedSecondHidden = AppendBias(secondHidden, _definition.UseSecondHiddenBias);
            var outputs = Multiply(augmentedSecondHidden, Weights.HiddenOutput).Select(Sigmoid).ToArray();
            return new ForwardStep(
                augmentedInputs,
                firstHidden,
                augmentedFirstHidden,
                secondHidden,
                augmentedSecondHidden,
                outputs,
                [.. firstHidden, .. secondHidden]);
        }

        var augmentedHidden = AppendBias(firstHidden, _definition.UseHiddenBias, hiddenBiasValue);
        var singleOutputs = Multiply(augmentedHidden, Weights.HiddenOutput).Select(Sigmoid).ToArray();
        return new ForwardStep(
            augmentedInputs,
            firstHidden,
            null,
            firstHidden,
            augmentedHidden,
            singleOutputs,
            (double[])firstHidden.Clone());
    }

    private double[] ComputeOutputDelta(double[] outputs, double[]? targets)
    {
        var outputDelta = new double[_definition.OutputUnits];
        if (targets is null)
        {
            return outputDelta;
        }

        for (var output = 0; output < _definition.OutputUnits; output++)
        {
            var difference = targets[output] - outputs[output];
            outputDelta[output] = _definition.CostFunction == CostFunction.CrossEntropy
                ? difference
                : difference * SigmoidDerivative(outputs[output]);
        }

        return outputDelta;
    }

    private static double[] ComputeSingleHiddenDelta(double[] hidden, double[] downstreamDelta, double[,] downstreamWeights)
    {
        var hiddenDelta = new double[hidden.Length];

        for (var hiddenIndex = 0; hiddenIndex < hidden.Length; hiddenIndex++)
        {
            var signal = 0.0;
            for (var next = 0; next < downstreamDelta.Length; next++)
            {
                signal += downstreamDelta[next] * downstreamWeights[hiddenIndex, next];
            }

            hiddenDelta[hiddenIndex] = signal * hidden[hiddenIndex] * (1.0 - hidden[hiddenIndex]);
        }

        return hiddenDelta;
    }

    private void TrainFeedForward(PatternSet patternSet, int steps, List<TrainingPoint> history)
    {
        var examples = patternSet.Examples;
        var batchInputHidden = new double[Weights.InputHidden.GetLength(0), Weights.InputHidden.GetLength(1)];
        var batchHiddenOutput = new double[Weights.HiddenOutput.GetLength(0), Weights.HiddenOutput.GetLength(1)];
        var batchHiddenHidden = Weights.HiddenHidden is null
            ? null
            : new double[Weights.HiddenHidden.GetLength(0), Weights.HiddenHidden.GetLength(1)];
        var stepIndex = 0;
        for (var step = 1; step <= steps; step++)
        {
            var example = _definition.UpdateMode == UpdateMode.Batch
                ? examples[stepIndex % examples.Count]
                : examples[_patternRandom.Next(examples.Count)];

            var gradient = ComputeFeedForwardGradient(example);

            if (_definition.UpdateMode == UpdateMode.Batch)
            {
                AddInPlace(batchInputHidden, gradient.SequenceGradient.InputHiddenGradient);
                AddInPlace(batchHiddenOutput, gradient.SequenceGradient.HiddenOutputGradient);

                if (batchHiddenHidden is not null && gradient.SequenceGradient.HiddenHiddenGradient is not null)
                {
                    AddInPlace(batchHiddenHidden, gradient.SequenceGradient.HiddenHiddenGradient);
                }

                if ((stepIndex + 1) % examples.Count == 0)
                {
                    ApplyGradient(
                        new SequenceGradient(
                            batchInputHidden,
                            batchHiddenOutput,
                            null,
                            batchHiddenHidden,
                            gradient.SequenceGradient.TotalSquaredError,
                            gradient.SequenceGradient.LabeledExampleCount));
                    Array.Clear(batchInputHidden, 0, batchInputHidden.Length);
                    Array.Clear(batchHiddenOutput, 0, batchHiddenOutput.Length);

                    if (batchHiddenHidden is not null)
                    {
                        Array.Clear(batchHiddenHidden, 0, batchHiddenHidden.Length);
                    }
                }
            }
            else
            {
                ApplyGradient(gradient.SequenceGradient);
            }

            var currentTsq = ComputeTsq(gradient.SequenceGradient.TotalSquaredError);

            if (currentTsq <= _definition.ErrorThreshold)
            {
                history.Add(new TrainingPoint(step, currentTsq));
                break;
            }

            if (ShouldStopEarly(history, examples.Count))
            {
                break;
            }

            history.Add(new TrainingPoint(step, currentTsq));

            stepIndex++;
        }
    }

    private void ApplyGradient(SequenceGradient gradient)
    {
        UpdateWeights(Weights.InputHidden, gradient.InputHiddenGradient, _previousInputHiddenDelta, 1.0);
        UpdateWeights(Weights.HiddenOutput, gradient.HiddenOutputGradient, _previousHiddenOutputDelta, 1.0);

        if (Weights.HiddenHidden is not null &&
            gradient.HiddenHiddenGradient is not null &&
            _previousHiddenHiddenDelta is not null)
        {
            UpdateWeights(Weights.HiddenHidden, gradient.HiddenHiddenGradient, _previousHiddenHiddenDelta, 1.0);
        }

        if (Weights.RecurrentHidden is not null &&
            gradient.RecurrentGradient is not null &&
            _previousRecurrentDelta is not null)
        {
            UpdateWeights(Weights.RecurrentHidden, gradient.RecurrentGradient, _previousRecurrentDelta, 1.0);
        }
    }

    private void UpdateWeights(double[,] weights, double[,] gradient, double[,] previousDelta, double scale)
    {
        for (var row = 0; row < weights.GetLength(0); row++)
        {
            for (var column = 0; column < weights.GetLength(1); column++)
            {
                var delta = (_definition.LearningRate * gradient[row, column] * scale) + (_definition.Momentum * previousDelta[row, column]);
                weights[row, column] += delta;
                previousDelta[row, column] = delta;
            }
        }
    }

    private void ApplyImmediateWeights(double[,] weights, double[] left, double[] right, double[,] previousDelta)
    {
        for (var row = 0; row < left.Length; row++)
        {
            for (var column = 0; column < right.Length; column++)
            {
                var delta = (_definition.LearningRate * left[row] * right[column]) + (_definition.Momentum * previousDelta[row, column]);
                weights[row, column] += delta;
                previousDelta[row, column] = delta;
            }
        }
    }

    private void FlushPendingWeights(double[,] weights, double[,] pendingDelta, double[,] previousDelta)
    {
        for (var row = 0; row < weights.GetLength(0); row++)
        {
            for (var column = 0; column < weights.GetLength(1); column++)
            {
                weights[row, column] += pendingDelta[row, column] + (_definition.Momentum * previousDelta[row, column]);
                previousDelta[row, column] = pendingDelta[row, column];
                pendingDelta[row, column] = 0.0;
            }
        }
    }

    private static double[,] CreateOuterProduct(double[] left, double[] right)
    {
        var matrix = new double[left.Length, right.Length];
        AccumulateOuterProduct(matrix, left, right);
        return matrix;
    }

    private double CalculateSquaredError(double[] outputs, double[] targets)
    {
        var total = 0.0;

        for (var index = 0; index < outputs.Length; index++)
        {
            var target = targets[index];
            total += Math.Pow(target - outputs[index], 2);
        }

        return total;
    }

    private bool ShouldStopEarly(IReadOnlyList<TrainingPoint> history, int patternCount)
    {
        var window = patternCount * 10;
        if (history.Count < window)
        {
            return false;
        }

        var rollingMax = 0.0;
        for (var index = history.Count - window; index < history.Count; index++)
        {
            rollingMax = Math.Max(rollingMax, history[index].AverageError);
        }

        return rollingMax < GetPrematureStopThreshold();
    }

    private double GetPrematureStopThreshold()
    {
        return _definition.NetworkKind == NetworkKind.SimpleRecurrent
            ? 0.01 * _definition.OutputUnits
            : Math.Sqrt(0.01) * _definition.OutputUnits;
    }

    private static double ComputeTsq(double squaredError)
    {
        return Math.Sqrt(squaredError);
    }

    private void SyncVisibleStateFromTraining()
    {
        if (_definition.NetworkKind != NetworkKind.SimpleRecurrent)
        {
            return;
        }

        _visibleHiddenContext = _trainingHiddenContext is null
            ? new double[_definition.HiddenUnits]
            : (double[])_trainingHiddenContext.Clone();
        _visibleHiddenBiasValue = _trainingHiddenBiasValue;
    }

    private double MeasureReportedSrnAggregateError(PatternSet patternSet)
    {
        var context = _visibleHiddenContext is null
            ? new double[_definition.HiddenUnits]
            : (double[])_visibleHiddenContext.Clone();
        var hiddenBiasValue = _visibleHiddenBiasValue;
        var totalSquaredError = 0.0;
        var labeled = 0;

        foreach (var example in patternSet.Examples)
        {
            var step = Forward(example.Inputs, context, hiddenBiasValue);

            if (example.Targets is not null)
            {
                totalSquaredError += CalculateSquaredError(step.Outputs, example.Targets);
                labeled++;
            }

            if (example.ResetsContextAfter)
            {
                Array.Clear(context, 0, context.Length);
                hiddenBiasValue = 0.0;
            }
            else
            {
                context = (double[])step.FinalHidden.Clone();
            }
        }

        _visibleHiddenContext = (double[])context.Clone();
        _visibleHiddenBiasValue = hiddenBiasValue;
        return labeled == 0 ? 0.0 : Math.Sqrt(totalSquaredError / labeled);
    }

    private double[] AppendBias(double[] values, bool includeBias)
    {
        return AppendBias(values, includeBias, 1.0);
    }

    private double[] AppendBias(double[] values, bool includeBias, double biasValue)
    {
        if (!includeBias)
        {
            return (double[])values.Clone();
        }

        var augmented = new double[values.Length + 1];
        Array.Copy(values, augmented, values.Length);
        augmented[^1] = biasValue;
        return augmented;
    }

    private static double[] Multiply(double[] vector, double[,] matrix)
    {
        var result = new double[matrix.GetLength(1)];

        for (var column = 0; column < matrix.GetLength(1); column++)
        {
            var total = 0.0;
            for (var row = 0; row < matrix.GetLength(0); row++)
            {
                total += vector[row] * matrix[row, column];
            }

            result[column] = total;
        }

        return result;
    }

    private static void AddInPlace(double[] destination, double[] source)
    {
        for (var index = 0; index < destination.Length; index++)
        {
            destination[index] += source[index];
        }
    }

    private static void AddInPlace(double[,] destination, double[,] source)
    {
        for (var row = 0; row < destination.GetLength(0); row++)
        {
            for (var column = 0; column < destination.GetLength(1); column++)
            {
                destination[row, column] += source[row, column];
            }
        }
    }

    private static void AccumulateOuterProduct(double[,] matrix, double[] left, double[] right)
    {
        for (var row = 0; row < left.Length; row++)
        {
            for (var column = 0; column < right.Length; column++)
            {
                matrix[row, column] += left[row] * right[column];
            }
        }
    }

    private static void AccumulateScaledOuterProduct(double[,] matrix, double[] left, double[] right, double scale)
    {
        for (var row = 0; row < left.Length; row++)
        {
            for (var column = 0; column < right.Length; column++)
            {
                matrix[row, column] += left[row] * right[column] * scale;
            }
        }
    }

    private double Sigmoid(double value)
    {
        return 1.0 / (1.0 + Math.Exp(-value));
    }

    private double SigmoidDerivative(double activatedValue)
    {
        return activatedValue * (1.0 - activatedValue);
    }

    private sealed record ForwardStep(
        double[] AugmentedInputs,
        double[] FirstHidden,
        double[]? AugmentedFirstHidden,
        double[] FinalHidden,
        double[] AugmentedFinalHidden,
        double[] Outputs,
        double[] HiddenActivations);

    private sealed record SequenceGradient(
        double[,] InputHiddenGradient,
        double[,] HiddenOutputGradient,
        double[,]? RecurrentGradient,
        double[,]? HiddenHiddenGradient,
        double TotalSquaredError,
        int LabeledExampleCount);

    private sealed record FeedForwardGradient(
        ForwardStep Step,
        SequenceGradient SequenceGradient,
        double[] FirstHiddenDelta,
        double[]? FinalHiddenDelta,
        double[] OutputDelta);
}

internal static class HierarchicalClusterer
{
    public static ClusterNode Cluster(IReadOnlyList<(string Label, double[] Vector)> points)
    {
        if (points.Count == 0)
        {
            throw new InvalidOperationException("At least one point is required for clustering.");
        }

        var clusters = points
            .Select(point => new ClusterState(
                new ClusterNode(point.Label, null, null, 0.0, 1),
                [point]))
            .ToList();

        while (clusters.Count > 1)
        {
            var bestLeft = 0;
            var bestRight = 1;
            var bestDistance = AverageDistance(clusters[0].Members, clusters[1].Members);

            for (var left = 0; left < clusters.Count - 1; left++)
            {
                for (var right = left + 1; right < clusters.Count; right++)
                {
                    var distance = AverageDistance(clusters[left].Members, clusters[right].Members);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestLeft = left;
                        bestRight = right;
                    }
                }
            }

            var leftState = clusters[bestLeft];
            var rightState = clusters[bestRight];
            var mergedMembers = leftState.Members.Concat(rightState.Members).ToArray();
            var merged = new ClusterState(
                new ClusterNode(
                    $"{leftState.Node.Label}+{rightState.Node.Label}",
                    leftState.Node,
                    rightState.Node,
                    bestDistance,
                    mergedMembers.Length),
                mergedMembers);

            clusters.RemoveAt(bestRight);
            clusters.RemoveAt(bestLeft);
            clusters.Add(merged);
        }

        return clusters[0].Node;
    }

    private static double AverageDistance(IReadOnlyList<(string Label, double[] Vector)> left, IReadOnlyList<(string Label, double[] Vector)> right)
    {
        var total = 0.0;
        var count = 0;

        foreach (var leftPoint in left)
        {
            foreach (var rightPoint in right)
            {
                total += Distance(leftPoint.Vector, rightPoint.Vector);
                count++;
            }
        }

        return total / count;
    }

    private static double Distance(double[] left, double[] right)
    {
        var sum = 0.0;
        for (var index = 0; index < left.Length; index++)
        {
            var delta = left[index] - right[index];
            sum += delta * delta;
        }

        return Math.Sqrt(sum);
    }

    private sealed record ClusterState(ClusterNode Node, IReadOnlyList<(string Label, double[] Vector)> Members);
}
