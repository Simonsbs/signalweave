using System.Text.Json;

namespace SignalWeave.Core;

public sealed record ProjectWorkspaceState(
    int LearningSteps = 5000,
    int SelectedPatternIndex = 0,
    string ErrorPlotDisplayMode = "Line",
    IReadOnlyList<TrainingSessionSnapshot>? TrainingSessions = null);

public sealed record TrainingSessionSnapshot(
    int SessionNumber,
    int StepsExecuted,
    int CompletedCycles,
    double DisplayAverageError,
    DateTimeOffset CompletedAtUtc,
    WeightSet Weights,
    IReadOnlyList<TrainingPoint>? History = null);

public sealed record SignalWeaveProject(
    NetworkDefinition Definition,
    PatternSet Patterns,
    WeightSet? Weights,
    int CompletedCycles = 0,
    ProjectWorkspaceState? Workspace = null);

public sealed record SignalWeaveCheckpoint(
    NetworkDefinition Definition,
    PatternSet Patterns,
    WeightSet Weights,
    int CompletedCycles,
    DateTimeOffset SavedAtUtc);

public static class SignalWeaveProjectSerializer
{
    public const string SchemaId = "signalweave-project/v4";
    public const string CompatibilitySchemaId = "signalweave-project/v3";
    public const string CompatibilitySchemaId2 = "signalweave-project/v2";
    public const string LegacySchemaId = "signalweave-project/v1";

    public static void SaveFile(string path, NetworkDefinition definition, PatternSet patterns, WeightSet? weights = null)
    {
        SaveFile(path, definition, patterns, weights, 0, null);
    }

    public static void SaveFile(
        string path,
        NetworkDefinition definition,
        PatternSet patterns,
        WeightSet? weights,
        int completedCycles,
        ProjectWorkspaceState? workspace)
    {
        var document = new ProjectDocument
        {
            Schema = SchemaId,
            Definition = definition,
            Patterns = ToDocumentPatterns(patterns),
            Weights = weights is null ? null : WeightSetDocumentMapper.ToDocument(weights),
            CompletedCycles = completedCycles,
            Workspace = workspace is null
                ? null
                : new WorkspaceDocument
                {
                    LearningSteps = workspace.LearningSteps,
                    SelectedPatternIndex = workspace.SelectedPatternIndex,
                    ErrorPlotDisplayMode = workspace.ErrorPlotDisplayMode,
                    TrainingSessions = workspace.TrainingSessions?
                        .Select(session => new TrainingSessionDocument
                        {
                            SessionNumber = session.SessionNumber,
                            StepsExecuted = session.StepsExecuted,
                            CompletedCycles = session.CompletedCycles,
                            DisplayAverageError = session.DisplayAverageError,
                            CompletedAtUtc = session.CompletedAtUtc,
                            Weights = WeightSetDocumentMapper.ToDocument(session.Weights),
                            History = session.History?
                                .Select(point => new TrainingPointDocument
                                {
                                    Epoch = point.Epoch,
                                    AverageError = point.AverageError
                                })
                                .ToList()
                        })
                        .ToList()
                }
        };

        WriteDocument(path, document);
    }

    public static SignalWeaveProject LoadFile(string path)
    {
        var document = ReadDocument<ProjectDocument>(path);
        EnsureSchema(document.Schema, [SchemaId, CompatibilitySchemaId, CompatibilitySchemaId2, LegacySchemaId], "project");

        return new SignalWeaveProject(
            document.Definition ?? throw new InvalidOperationException("Project file is missing the network definition."),
            new PatternSet(ToPatternExamples(document.Patterns)),
            document.Weights is null ? null : WeightSetDocumentMapper.FromDocument(document.Weights),
            document.CompletedCycles,
            document.Workspace is null
                ? null
                : new ProjectWorkspaceState(
                    document.Workspace.LearningSteps,
                    document.Workspace.SelectedPatternIndex,
                    document.Workspace.ErrorPlotDisplayMode ?? "Line",
                    document.Workspace.TrainingSessions?
                        .Where(static session => session.Weights is not null)
                        .Select(session => new TrainingSessionSnapshot(
                            session.SessionNumber,
                            session.StepsExecuted,
                            session.CompletedCycles,
                            session.DisplayAverageError,
                            session.CompletedAtUtc,
                            WeightSetDocumentMapper.FromDocument(session.Weights!),
                            session.History?
                                .Select(point => new TrainingPoint(point.Epoch, point.AverageError))
                                .ToArray()))
                        .ToArray()));
    }

    private static List<PatternExampleDocument> ToDocumentPatterns(PatternSet patterns)
    {
        return patterns.Examples
            .Select(example => new PatternExampleDocument
            {
                Label = example.Label,
                Inputs = example.Inputs,
                Targets = example.Targets,
                ResetsContextAfter = example.ResetsContextAfter
            })
            .ToList();
    }

    private static IReadOnlyList<PatternExample> ToPatternExamples(IEnumerable<PatternExampleDocument>? documents)
    {
        return (documents ?? [])
            .Select(document => new PatternExample(
                document.Label,
                document.Inputs ?? [],
                document.Targets,
                document.ResetsContextAfter))
            .ToArray();
    }

    private static void WriteDocument<TDocument>(string path, TDocument document)
    {
        var json = JsonSerializer.Serialize(document, SerializerOptions);
        File.WriteAllText(path, json);
    }

    private static TDocument ReadDocument<TDocument>(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<TDocument>(json, SerializerOptions)
            ?? throw new InvalidOperationException($"File '{path}' is empty or invalid.");
    }

    private static void EnsureSchema(string? actual, IReadOnlyList<string> expected, string type)
    {
        if (actual is null || !expected.Contains(actual, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported {type} schema '{actual ?? "<missing>"}'. Expected one of: {string.Join(", ", expected)}.");
        }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private sealed class ProjectDocument
    {
        public string? Schema { get; set; }
        public NetworkDefinition? Definition { get; set; }
        public List<PatternExampleDocument> Patterns { get; set; } = [];
        public WeightDocument? Weights { get; set; }
        public int CompletedCycles { get; set; }
        public WorkspaceDocument? Workspace { get; set; }
    }
}

internal sealed class WorkspaceDocument
{
    public int LearningSteps { get; set; } = 5000;
    public int SelectedPatternIndex { get; set; }
    public string? ErrorPlotDisplayMode { get; set; } = "Line";
    public List<TrainingSessionDocument>? TrainingSessions { get; set; }
}

internal sealed class TrainingSessionDocument
{
    public int SessionNumber { get; set; }
    public int StepsExecuted { get; set; }
    public int CompletedCycles { get; set; }
    public double DisplayAverageError { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public WeightDocument? Weights { get; set; }
    public List<TrainingPointDocument>? History { get; set; }
}

internal sealed class TrainingPointDocument
{
    public int Epoch { get; set; }
    public double AverageError { get; set; }
}

public static class SignalWeaveCheckpointSerializer
{
    public const string SchemaId = "signalweave-checkpoint/v1";

    public static void SaveFile(
        string path,
        NetworkDefinition definition,
        PatternSet patterns,
        WeightSet weights,
        int completedCycles,
        DateTimeOffset? savedAtUtc = null)
    {
        var document = new CheckpointDocument
        {
            Schema = SchemaId,
            Definition = definition,
            Patterns = patterns.Examples
                .Select(example => new PatternExampleDocument
                {
                    Label = example.Label,
                    Inputs = example.Inputs,
                    Targets = example.Targets,
                    ResetsContextAfter = example.ResetsContextAfter
                })
                .ToList(),
            Weights = WeightSetDocumentMapper.ToDocument(weights),
            CompletedCycles = completedCycles,
            SavedAtUtc = savedAtUtc ?? DateTimeOffset.UtcNow
        };

        var json = JsonSerializer.Serialize(document, SerializerOptions);
        File.WriteAllText(path, json);
    }

    public static SignalWeaveCheckpoint LoadFile(string path)
    {
        var json = File.ReadAllText(path);
        var document = JsonSerializer.Deserialize<CheckpointDocument>(json, SerializerOptions)
            ?? throw new InvalidOperationException($"File '{path}' is empty or invalid.");

        if (!string.Equals(document.Schema, SchemaId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported checkpoint schema '{document.Schema ?? "<missing>"}'. Expected '{SchemaId}'.");
        }

        return new SignalWeaveCheckpoint(
            document.Definition ?? throw new InvalidOperationException("Checkpoint file is missing the network definition."),
            new PatternSet((document.Patterns ?? [])
                .Select(documentPattern => new PatternExample(
                    documentPattern.Label,
                    documentPattern.Inputs ?? [],
                    documentPattern.Targets,
                    documentPattern.ResetsContextAfter))
                .ToArray()),
            document.Weights is null
                ? throw new InvalidOperationException("Checkpoint file is missing the weight snapshot.")
                : WeightSetDocumentMapper.FromDocument(document.Weights),
            document.CompletedCycles,
            document.SavedAtUtc);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private sealed class CheckpointDocument
    {
        public string? Schema { get; set; }
        public NetworkDefinition? Definition { get; set; }
        public List<PatternExampleDocument>? Patterns { get; set; }
        public WeightDocument? Weights { get; set; }
        public int CompletedCycles { get; set; }
        public DateTimeOffset SavedAtUtc { get; set; }
    }
}

internal sealed class PatternExampleDocument
{
    public string Label { get; set; } = string.Empty;
    public double[]? Inputs { get; set; }
    public double[]? Targets { get; set; }
    public bool ResetsContextAfter { get; set; }
}

internal sealed class WeightDocument
{
    public string Name { get; set; } = string.Empty;
    public string NetworkKind { get; set; } = string.Empty;
    public double[][] InputHidden { get; set; } = [];
    public double[][] HiddenOutput { get; set; } = [];
    public double[][]? RecurrentHidden { get; set; }
    public double[][]? HiddenHidden { get; set; }
}

internal static class WeightSetDocumentMapper
{
    public static WeightDocument ToDocument(WeightSet weights)
    {
        return new WeightDocument
        {
            InputHidden = ToJagged(weights.InputHidden),
            HiddenOutput = ToJagged(weights.HiddenOutput),
            RecurrentHidden = weights.RecurrentHidden is null ? null : ToJagged(weights.RecurrentHidden),
            HiddenHidden = weights.HiddenHidden is null ? null : ToJagged(weights.HiddenHidden)
        };
    }

    public static WeightSet FromDocument(WeightDocument document)
    {
        return new WeightSet(
            ToRectangular(document.InputHidden),
            ToRectangular(document.HiddenOutput),
            document.RecurrentHidden is null ? null : ToRectangular(document.RecurrentHidden),
            document.HiddenHidden is null ? null : ToRectangular(document.HiddenHidden));
    }

    private static double[][] ToJagged(double[,] matrix)
    {
        var rows = matrix.GetLength(0);
        var columns = matrix.GetLength(1);
        var jagged = new double[rows][];

        for (var row = 0; row < rows; row++)
        {
            jagged[row] = new double[columns];
            for (var column = 0; column < columns; column++)
            {
                jagged[row][column] = matrix[row, column];
            }
        }

        return jagged;
    }

    private static double[,] ToRectangular(double[][] jagged)
    {
        if (jagged.Length == 0)
        {
            return new double[0, 0];
        }

        var rows = jagged.Length;
        var columns = jagged[0].Length;
        var matrix = new double[rows, columns];

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                matrix[row, column] = jagged[row][column];
            }
        }

        return matrix;
    }
}
