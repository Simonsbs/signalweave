using System.Globalization;
using System.Text;

namespace SignalWeave.Core;

public enum NetworkKind
{
    FeedForward,
    SimpleRecurrent
}

public enum UpdateMode
{
    Pattern,
    Batch
}

public enum CostFunction
{
    SumSquaredError,
    CrossEntropy
}

public sealed class NetworkDefinition
{
    public string Name { get; init; } = "Untitled";
    public NetworkKind NetworkKind { get; init; } = NetworkKind.FeedForward;
    public int InputUnits { get; init; }
    public int HiddenUnits { get; init; }
    public int SecondHiddenUnits { get; init; }
    public int OutputUnits { get; init; }
    public bool UseInputBias { get; init; } = true;
    public bool UseHiddenBias { get; init; } = true;
    public bool UseSecondHiddenBias { get; init; }
    public double LearningRate { get; init; } = 0.3;
    public double Momentum { get; init; } = 0.0;
    public double RandomWeightRange { get; init; } = 0.5;
    public double SigmoidPrimeOffset { get; init; } = 0.1;
    public int MaxEpochs { get; init; } = 1000;
    public double ErrorThreshold { get; init; } = 0.01;
    public UpdateMode UpdateMode { get; init; } = UpdateMode.Pattern;
    public CostFunction CostFunction { get; init; } = CostFunction.SumSquaredError;

    public bool HasSecondHiddenLayer => NetworkKind == NetworkKind.FeedForward && SecondHiddenUnits > 0;
    public int TotalLayerCount => NetworkKind == NetworkKind.SimpleRecurrent ? 3 : HasSecondHiddenLayer ? 4 : 3;

    public void Validate()
    {
        if (InputUnits < 1 || HiddenUnits < 1 || OutputUnits < 1)
        {
            throw new InvalidOperationException("Input, hidden, and output unit counts must all be positive.");
        }

        if (SecondHiddenUnits < 0)
        {
            throw new InvalidOperationException("Second hidden unit count cannot be negative.");
        }

        if (NetworkKind == NetworkKind.SimpleRecurrent && SecondHiddenUnits > 0)
        {
            throw new InvalidOperationException("Simple recurrent networks do not support a second hidden layer.");
        }

        if (LearningRate <= 0)
        {
            throw new InvalidOperationException("Learning rate must be greater than zero.");
        }

        if (Momentum < 0)
        {
            throw new InvalidOperationException("Momentum cannot be negative.");
        }

        if (RandomWeightRange <= 0)
        {
            throw new InvalidOperationException("Random weight range must be greater than zero.");
        }

        if (MaxEpochs < 1)
        {
            throw new InvalidOperationException("Max epochs must be at least 1.");
        }

        if (ErrorThreshold < 0)
        {
            throw new InvalidOperationException("Error threshold cannot be negative.");
        }
    }

    public string ToSummary()
    {
        return string.Join(
            Environment.NewLine,
            $"Name: {Name}",
            $"Network: {NetworkKind}",
            HasSecondHiddenLayer
                ? $"Units: {InputUnits} input / {HiddenUnits} hidden1 / {SecondHiddenUnits} hidden2 / {OutputUnits} output"
                : $"Units: {InputUnits} input / {HiddenUnits} hidden / {OutputUnits} output",
            HasSecondHiddenLayer
                ? $"Bias: input={UseInputBias}, hidden1={UseHiddenBias}, hidden2={UseSecondHiddenBias}"
                : $"Bias: input={UseInputBias}, hidden={UseHiddenBias}",
            $"Learning: rate={LearningRate.ToString("0.###", CultureInfo.InvariantCulture)}, momentum={Momentum.ToString("0.###", CultureInfo.InvariantCulture)}",
            $"Training: update={UpdateMode}, cost={CostFunction}, maxEpochs={MaxEpochs}, threshold={ErrorThreshold.ToString("0.####", CultureInfo.InvariantCulture)}");
    }
}

public sealed record PatternExample(string Label, double[] Inputs, double[]? Targets, bool ResetsContextAfter);

public sealed class PatternSet
{
    public PatternSet(IReadOnlyList<PatternExample> examples)
    {
        Examples = examples;
    }

    public IReadOnlyList<PatternExample> Examples { get; }

    public bool HasResetMarkers => Examples.Any(example => example.ResetsContextAfter);

    public IReadOnlyList<IReadOnlyList<PatternExample>> ToSequences()
    {
        var sequences = new List<IReadOnlyList<PatternExample>>();
        var current = new List<PatternExample>();

        foreach (var example in Examples)
        {
            current.Add(example);

            if (example.ResetsContextAfter && current.Count > 0)
            {
                sequences.Add(current.ToArray());
                current = new List<PatternExample>();
            }
        }

        if (current.Count > 0)
        {
            sequences.Add(current.ToArray());
        }

        return sequences;
    }

    public void ValidateAgainst(NetworkDefinition definition, bool requireTargets)
    {
        if (Examples.Count == 0)
        {
            throw new InvalidOperationException("Pattern file did not contain any examples.");
        }

        foreach (var example in Examples)
        {
            if (example.Inputs.Length != definition.InputUnits)
            {
                throw new InvalidOperationException($"Pattern '{example.Label}' has {example.Inputs.Length} inputs but the network expects {definition.InputUnits}.");
            }

            if (requireTargets)
            {
                if (example.Targets is null)
                {
                    throw new InvalidOperationException($"Pattern '{example.Label}' is missing targets required for training.");
                }

                if (example.Targets.Length != definition.OutputUnits)
                {
                    throw new InvalidOperationException($"Pattern '{example.Label}' has {example.Targets.Length} targets but the network expects {definition.OutputUnits} outputs.");
                }
            }
        }
    }

    public string ToSummary()
    {
        var withTargets = Examples.Count(example => example.Targets is not null);
        var sequences = ToSequences().Count;
        return $"Patterns: {Examples.Count}, sequences: {sequences}, labeled targets: {withTargets}";
    }
}

public sealed record TrainingPoint(int Epoch, double AverageError);

public sealed record TestResult(
    int Index,
    string Label,
    double[] Inputs,
    double[] Outputs,
    double[] HiddenActivations,
    double[]? Targets,
    double Error);

public sealed record RunResult(IReadOnlyList<TestResult> Results, double AverageError)
{
    public string ToTable()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Idx Label              Outputs          Targets          Error");

        foreach (var result in Results)
        {
            var outputs = string.Join(" ", result.Outputs.Select(FormatNumber));
            var targets = result.Targets is null ? "-" : string.Join(" ", result.Targets.Select(FormatNumber));
            builder.AppendLine($"{result.Index + 1,3} {result.Label,-17} {outputs,-16} {targets,-16} {FormatNumber(result.Error)}");
        }

        builder.AppendLine($"Average error: {FormatNumber(AverageError)}");
        return builder.ToString();
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.000", CultureInfo.InvariantCulture);
    }
}

public sealed record TrainResult(
    IReadOnlyList<TrainingPoint> History,
    WeightSet Weights,
    RunResult FinalRun)
{
    public TrainingPoint FinalPoint => History[^1];
}

public sealed class ClusterNode
{
    public ClusterNode(string label, ClusterNode? left, ClusterNode? right, double distance, int size)
    {
        Label = label;
        Left = left;
        Right = right;
        Distance = distance;
        Size = size;
    }

    public string Label { get; }
    public ClusterNode? Left { get; }
    public ClusterNode? Right { get; }
    public double Distance { get; }
    public int Size { get; }
    public bool IsLeaf => Left is null && Right is null;

    public string ToDisplayText()
    {
        var builder = new StringBuilder();
        Write(builder, 0);
        return builder.ToString();
    }

    private void Write(StringBuilder builder, int depth)
    {
        builder.Append(' ', depth * 2);
        builder.Append(IsLeaf ? "- " : "+ ");
        builder.Append(Label);
        builder.Append(" (d=");
        builder.Append(Distance.ToString("0.000", CultureInfo.InvariantCulture));
        builder.AppendLine(")");

        Left?.Write(builder, depth + 1);
        Right?.Write(builder, depth + 1);
    }
}

public static class CompatibilityProfile
{
    public static string ToDisplayText()
    {
        return string.Join(
            Environment.NewLine,
            "SignalWeave parity target",
            "implemented: feed-forward nets",
            "implemented: simple recurrent nets",
            "implemented: batch and pattern updates",
            "implemented: learning rate, momentum, stop thresholds, random init range",
            "implemented: save/load weights",
            "implemented: test all, test one, hidden/output clustering",
            "pending: original BasicProp weight-file compatibility",
            "pending: visual plots and editable matrix views");
    }
}
