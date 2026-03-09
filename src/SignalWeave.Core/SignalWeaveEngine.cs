namespace SignalWeave.Core;

public sealed class SignalWeaveEngine
{
    private readonly NetworkDefinition _definition;
    private readonly Random _patternRandom;

    public SignalWeaveEngine(NetworkDefinition definition, WeightSet? weights = null, int? seed = null)
    {
        _definition = definition;
        _definition.Validate();
        _patternRandom = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        Weights = weights?.Clone() ?? WeightSet.CreateRandom(_definition, seed);
    }

    public WeightSet Weights { get; private set; }

    public TrainResult Train(PatternSet patternSet, int? maxEpochs = null)
    {
        patternSet.ValidateAgainst(_definition, requireTargets: true);

        var steps = maxEpochs ?? _definition.MaxEpochs;
        var history = new List<TrainingPoint>(steps);
        var previousInputHiddenDelta = new double[Weights.InputHidden.GetLength(0), Weights.InputHidden.GetLength(1)];
        var previousHiddenOutputDelta = new double[Weights.HiddenOutput.GetLength(0), Weights.HiddenOutput.GetLength(1)];
        var previousRecurrentDelta = Weights.RecurrentHidden is null
            ? null
            : new double[Weights.RecurrentHidden.GetLength(0), Weights.RecurrentHidden.GetLength(1)];

        if (_definition.NetworkKind == NetworkKind.FeedForward)
        {
            TrainFeedForward(
                patternSet,
                steps,
                history,
                previousInputHiddenDelta,
                previousHiddenOutputDelta,
                previousRecurrentDelta);
        }
        else
        {
            var sequences = patternSet.ToSequences();

            for (var step = 1; step <= steps; step++)
            {
                var totalSquaredError = 0.0;
                var labeledExampleCount = 0;

                var batchInputHidden = new double[Weights.InputHidden.GetLength(0), Weights.InputHidden.GetLength(1)];
                var batchHiddenOutput = new double[Weights.HiddenOutput.GetLength(0), Weights.HiddenOutput.GetLength(1)];
                var batchRecurrent = Weights.RecurrentHidden is null
                    ? null
                    : new double[Weights.RecurrentHidden.GetLength(0), Weights.RecurrentHidden.GetLength(1)];

                foreach (var sequence in sequences)
                {
                    var gradient = ComputeSequenceGradient(sequence);
                    totalSquaredError += gradient.TotalSquaredError;
                    labeledExampleCount += gradient.LabeledExampleCount;

                    if (_definition.UpdateMode == UpdateMode.Batch)
                    {
                        AddInPlace(batchInputHidden, gradient.InputHiddenGradient);
                        AddInPlace(batchHiddenOutput, gradient.HiddenOutputGradient);
                        if (batchRecurrent is not null && gradient.RecurrentGradient is not null)
                        {
                            AddInPlace(batchRecurrent, gradient.RecurrentGradient);
                        }
                    }
                    else
                    {
                        ApplyGradient(gradient, previousInputHiddenDelta, previousHiddenOutputDelta, previousRecurrentDelta, 1.0);
                    }
                }

                if (_definition.UpdateMode == UpdateMode.Batch)
                {
                    var normalizer = Math.Max(1, labeledExampleCount);
                    ApplyGradient(
                        new SequenceGradient(batchInputHidden, batchHiddenOutput, batchRecurrent, totalSquaredError, labeledExampleCount),
                        previousInputHiddenDelta,
                        previousHiddenOutputDelta,
                        previousRecurrentDelta,
                        1.0 / normalizer);
                }

                var currentTsq = ComputeTsq(totalSquaredError);
                history.Add(new TrainingPoint(step, currentTsq));

                if (currentTsq <= _definition.ErrorThreshold || ShouldStopEarly(history, patternSet.Examples.Count))
                {
                    break;
                }
            }
        }

        var finalRun = TestAll(patternSet);
        return new TrainResult(history, Weights.Clone(), finalRun);
    }

    public RunResult TestAll(PatternSet patternSet)
    {
        patternSet.ValidateAgainst(_definition, requireTargets: false);

        var results = new List<TestResult>(patternSet.Examples.Count);
        var totalSquaredError = 0.0;
        var labeled = 0;
        var index = 0;

        foreach (var sequence in patternSet.ToSequences())
        {
            var context = new double[_definition.HiddenUnits];

            foreach (var example in sequence)
            {
                var step = Forward(example.Inputs, context);
                context = step.Hidden;

                var error = 0.0;
                if (example.Targets is not null)
                {
                    var squaredError = CalculateSquaredError(step.Outputs, example.Targets);
                    error = ComputeTsq(squaredError);
                    totalSquaredError += squaredError;
                    labeled++;
                }

                results.Add(new TestResult(index, example.Label, example.Inputs, step.Outputs, step.Hidden, example.Targets, error));
                index++;
            }
        }

        return new RunResult(results, labeled == 0 ? 0.0 : Math.Sqrt(totalSquaredError / labeled));
    }

    public TestResult TestOne(PatternSet patternSet, int index)
    {
        var run = TestAll(patternSet);
        return run.Results[index];
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

    private SequenceGradient ComputeSequenceGradient(IReadOnlyList<PatternExample> sequence)
    {
        var steps = new ForwardStep[sequence.Count];
        var previousHiddenStates = new double[sequence.Count][];
        var context = new double[_definition.HiddenUnits];
        var totalSquaredError = 0.0;
        var labeledCount = 0;

        for (var index = 0; index < sequence.Count; index++)
        {
            previousHiddenStates[index] = (double[])context.Clone();
            steps[index] = Forward(sequence[index].Inputs, context);
            context = steps[index].Hidden;

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
            var outputDelta = new double[_definition.OutputUnits];

            if (example.Targets is not null)
            {
                for (var output = 0; output < _definition.OutputUnits; output++)
                {
                    var difference = example.Targets[output] - step.Outputs[output];
                    outputDelta[output] = _definition.CostFunction == CostFunction.CrossEntropy
                        ? difference
                        : difference * SigmoidDerivative(step.Outputs[output]);
                }
            }

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

                hiddenDelta[hidden] = signal * SigmoidDerivative(step.Hidden[hidden]);
            }

            AccumulateOuterProduct(inputHiddenGradient, step.AugmentedInputs, hiddenDelta);
            AccumulateOuterProduct(hiddenOutputGradient, step.AugmentedHidden, outputDelta);

            if (recurrentGradient is not null)
            {
                AccumulateOuterProduct(recurrentGradient, previousHiddenStates[index], hiddenDelta);
            }

            nextHiddenDelta = hiddenDelta;
        }

        return new SequenceGradient(inputHiddenGradient, hiddenOutputGradient, recurrentGradient, totalSquaredError, labeledCount);
    }

    private ForwardStep Forward(double[] inputs, double[] context)
    {
        var augmentedInputs = AppendBias(inputs, _definition.UseInputBias);
        var hiddenNet = Multiply(augmentedInputs, Weights.InputHidden);

        if (Weights.RecurrentHidden is not null)
        {
            AddInPlace(hiddenNet, Multiply(context, Weights.RecurrentHidden));
        }

        var hidden = hiddenNet.Select(Sigmoid).ToArray();
        var augmentedHidden = AppendBias(hidden, _definition.UseHiddenBias);
        var outputs = Multiply(augmentedHidden, Weights.HiddenOutput).Select(Sigmoid).ToArray();
        return new ForwardStep(augmentedInputs, hidden, augmentedHidden, outputs);
    }

    private void ApplyGradient(
        SequenceGradient gradient,
        double[,] previousInputHiddenDelta,
        double[,] previousHiddenOutputDelta,
        double[,]? previousRecurrentDelta,
        double scale)
    {
        UpdateWeights(Weights.InputHidden, gradient.InputHiddenGradient, previousInputHiddenDelta, scale);
        UpdateWeights(Weights.HiddenOutput, gradient.HiddenOutputGradient, previousHiddenOutputDelta, scale);

        if (Weights.RecurrentHidden is not null &&
            gradient.RecurrentGradient is not null &&
            previousRecurrentDelta is not null)
        {
            UpdateWeights(Weights.RecurrentHidden, gradient.RecurrentGradient, previousRecurrentDelta, scale);
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

    private void TrainFeedForward(
        PatternSet patternSet,
        int steps,
        List<TrainingPoint> history,
        double[,] previousInputHiddenDelta,
        double[,] previousHiddenOutputDelta,
        double[,]? previousRecurrentDelta)
    {
        var examples = patternSet.Examples;
        var batchInputHidden = new double[Weights.InputHidden.GetLength(0), Weights.InputHidden.GetLength(1)];
        var batchHiddenOutput = new double[Weights.HiddenOutput.GetLength(0), Weights.HiddenOutput.GetLength(1)];
        var stepIndex = 0;

        for (var step = 1; step <= steps; step++)
        {
            var example = _definition.UpdateMode == UpdateMode.Batch
                ? examples[stepIndex % examples.Count]
                : examples[_patternRandom.Next(examples.Count)];

            var gradient = ComputeSequenceGradient([example]);

            if (_definition.UpdateMode == UpdateMode.Batch)
            {
                AddInPlace(batchInputHidden, gradient.InputHiddenGradient);
                AddInPlace(batchHiddenOutput, gradient.HiddenOutputGradient);

                if ((stepIndex + 1) % examples.Count == 0)
                {
                    ApplyGradient(
                        new SequenceGradient(batchInputHidden, batchHiddenOutput, null, gradient.TotalSquaredError, gradient.LabeledExampleCount),
                        previousInputHiddenDelta,
                        previousHiddenOutputDelta,
                        previousRecurrentDelta,
                        1.0);
                    Array.Clear(batchInputHidden, 0, batchInputHidden.Length);
                    Array.Clear(batchHiddenOutput, 0, batchHiddenOutput.Length);
                }
            }
            else
            {
                ApplyGradient(gradient, previousInputHiddenDelta, previousHiddenOutputDelta, previousRecurrentDelta, 1.0);
            }

            var currentTsq = ComputeTsq(gradient.TotalSquaredError);
            history.Add(new TrainingPoint(step, currentTsq));

            if (currentTsq <= _definition.ErrorThreshold || ShouldStopEarly(history, examples.Count))
            {
                break;
            }

            stepIndex++;
        }
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

    private double[] AppendBias(double[] values, bool includeBias)
    {
        if (!includeBias)
        {
            return (double[])values.Clone();
        }

        var augmented = new double[values.Length + 1];
        Array.Copy(values, augmented, values.Length);
        augmented[^1] = 1.0;
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
        double[] Hidden,
        double[] AugmentedHidden,
        double[] Outputs);

    private sealed record SequenceGradient(
        double[,] InputHiddenGradient,
        double[,] HiddenOutputGradient,
        double[,]? RecurrentGradient,
        double TotalSquaredError,
        int LabeledExampleCount);
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
