using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SignalWeave.Core;

namespace SignalWeave.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly string[] _learningRateOptions = ["0.1", "0.2", "0.3", "0.4", "0.5", "0.6", "0.7", "0.8", "0.9", "1.0", "5.0"];
    private readonly string[] _momentumOptions = ["0", "0.2", "0.5", "0.6", "0.7", "0.8", "0.9", "1.0"];
    private readonly string[] _learningStepOptions = ["100", "200", "500", "1000", "2000", "5000", "10000", "20000", "50000", "100000"];
    private readonly string[] _weightRangeOptions = ["-0.1 - 0.1", "-1 - 1", "-10 - 10"];

    private NetworkDefinition? _definition;
    private PatternSet? _patternSet;
    private SignalWeaveEngine? _engine;
    private string? _engineSignature;

    public MainWindowViewModel()
    {
        LearningRateOptions = new ReadOnlyCollection<string>(_learningRateOptions);
        MomentumOptions = new ReadOnlyCollection<string>(_momentumOptions);
        LearningStepOptions = new ReadOnlyCollection<string>(_learningStepOptions);
        WeightRangeOptions = new ReadOnlyCollection<string>(_weightRangeOptions);

        ParseEditorInternal(syncControlsFromEditor: true, resetWeights: true, consoleMessage: "Loaded XOR demo.");
    }

    public IReadOnlyList<string> LearningRateOptions { get; }
    public IReadOnlyList<string> MomentumOptions { get; }
    public IReadOnlyList<string> LearningStepOptions { get; }
    public IReadOnlyList<string> WeightRangeOptions { get; }
    public ObservableCollection<string> PatternOptions { get; } = [];
    public ObservableCollection<DiagramEdgeItem> DiagramEdges { get; } = [];
    public ObservableCollection<DiagramNodeItem> DiagramNodes { get; } = [];

    [ObservableProperty]
    private string _sampleTitle = "XOR demo";

    [ObservableProperty]
    private string _configText = SignalWeaveSamples.XorConfig;

    [ObservableProperty]
    private string _patternText = SignalWeaveSamples.XorPatterns;

    [ObservableProperty]
    private string _selectedLearningRate = "0.3";

    [ObservableProperty]
    private string _selectedMomentum = "0.8";

    [ObservableProperty]
    private string _selectedLearningSteps = "5000";

    [ObservableProperty]
    private string _selectedWeightRange = "-1 - 1";

    [ObservableProperty]
    private bool _batchUpdate;

    [ObservableProperty]
    private bool _crossEntropy;

    [ObservableProperty]
    private string _selectedPattern = string.Empty;

    [ObservableProperty]
    private bool _canTestOne;

    [ObservableProperty]
    private string _networkSummary = "No network parsed.";

    [ObservableProperty]
    private string _progressLabel = "Untrained";

    [ObservableProperty]
    private string _consoleText = "Use the menu or editor tabs to configure a network, then train or test it.";

    [ObservableProperty]
    private string _analysisText = CompatibilityProfile.ToDisplayText();

    [ObservableProperty]
    private string _weightsText = "Weights are not initialized yet.";

    [ObservableProperty]
    private string _historyText = "No training history yet.";

    [ObservableProperty]
    private string _errorProgressPoints = "0,132 240,132";

    [ObservableProperty]
    private int _editorTabIndex;

    [RelayCommand]
    private void LoadXorDemo()
    {
        SampleTitle = "XOR demo";
        ConfigText = SignalWeaveSamples.XorConfig;
        PatternText = SignalWeaveSamples.XorPatterns;
        ParseEditorInternal(syncControlsFromEditor: true, resetWeights: true, consoleMessage: "Loaded XOR demo.");
    }

    [RelayCommand]
    private void LoadSrnDemo()
    {
        SampleTitle = "Echo SRN demo";
        ConfigText = SignalWeaveSamples.EchoSrnConfig;
        PatternText = SignalWeaveSamples.EchoSrnPatterns;
        ParseEditorInternal(syncControlsFromEditor: true, resetWeights: true, consoleMessage: "Loaded SRN demo.");
    }

    [RelayCommand]
    private void ParseEditor()
    {
        ParseEditorInternal(syncControlsFromEditor: true, resetWeights: true, consoleMessage: "Parsed current editor contents and reset network weights.");
    }

    [RelayCommand]
    private void ResetNetwork()
    {
        RunSafe(() =>
        {
            EnsureContext(resetWeights: true);
            ProgressLabel = "Untrained";
            HistoryText = "No training history yet.";
            ErrorProgressPoints = "0,132 240,132";
            AnalysisText = "Weights were reset using the selected range.";
            ConsoleText = $"Reset {_definition!.Name}.{Environment.NewLine}{_definition.ToSummary()}";
        });
    }

    [RelayCommand]
    private void Train()
    {
        RunSafe(() =>
        {
            EnsureContext(resetWeights: false);
            var steps = ParseInt(SelectedLearningSteps);
            var result = _engine!.Train(_patternSet!, steps);

            ProgressLabel = $"{result.History.Count} cycles";
            HistoryText = BuildHistoryText(result.History);
            ErrorProgressPoints = BuildErrorPolyline(result.History);
            WeightsText = BuildWeightsText(result.Weights);
            RefreshDiagram();

            ConsoleText =
                $"Training completed for {_definition!.Name}.{Environment.NewLine}" +
                $"Cycles: {result.History.Count}{Environment.NewLine}" +
                $"Final TSQ: {result.FinalPoint.AverageError.ToString("0.000000", CultureInfo.InvariantCulture)}";
            AnalysisText = result.FinalRun.ToTable();
        });
    }

    [RelayCommand]
    private void TestAll()
    {
        RunSafe(() =>
        {
            EnsureContext(resetWeights: false);
            var run = _engine!.TestAll(_patternSet!);
            ConsoleText = $"Test all completed for {_definition!.Name}.{Environment.NewLine}Average error: {run.AverageError.ToString("0.000000", CultureInfo.InvariantCulture)}";
            AnalysisText = run.ToTable();
            WeightsText = BuildWeightsText(_engine.Weights);
            RefreshDiagram();
        });
    }

    [RelayCommand]
    private void TestOne()
    {
        RunSafe(() =>
        {
            EnsureContext(resetWeights: false);
            if (!CanTestOne)
            {
                throw new InvalidOperationException("Test one is only available when fewer than 24 patterns are loaded.");
            }

            var patternIndex = PatternOptions.IndexOf(SelectedPattern);
            if (patternIndex < 0)
            {
                patternIndex = 0;
            }

            var result = _engine!.TestOne(_patternSet!, patternIndex);
            ConsoleText = $"Test one completed for pattern '{result.Label}'.";
            AnalysisText = BuildSinglePatternResult(result);
        });
    }

    [RelayCommand]
    private void ShowWeights()
    {
        RunSafe(() =>
        {
            EnsureContext(resetWeights: false);
            WeightsText = BuildWeightsText(_engine!.Weights);
            AnalysisText = WeightsText;
            EditorTabIndex = 3;
            ConsoleText = "Showing current network weights.";
        });
    }

    [RelayCommand]
    private void ShowPatterns()
    {
        RunSafe(() =>
        {
            EnsureContext(resetWeights: false);
            AnalysisText = BuildPatternSummary();
            EditorTabIndex = 2;
            ConsoleText = "Showing parsed pattern summary.";
        });
    }

    [RelayCommand]
    private void ClusterOutputs()
    {
        RunSafe(() =>
        {
            EnsureContext(resetWeights: false);
            AnalysisText = _engine!.ClusterOutputs(_patternSet!).ToDisplayText();
            ConsoleText = "Output clustering complete.";
        });
    }

    [RelayCommand]
    private void ClusterHidden()
    {
        RunSafe(() =>
        {
            EnsureContext(resetWeights: false);
            AnalysisText = _engine!.ClusterHiddenStates(_patternSet!).ToDisplayText();
            ConsoleText = "Hidden-state clustering complete.";
        });
    }

    [RelayCommand]
    private void ShowCompatibility()
    {
        AnalysisText = CompatibilityProfile.ToDisplayText();
        ConsoleText = "Showing current compatibility profile.";
    }

    private void ParseEditorInternal(bool syncControlsFromEditor, bool resetWeights, string consoleMessage)
    {
        RunSafe(() =>
        {
            EnsureContext(resetWeights, syncControlsFromEditor);
            ProgressLabel = "Untrained";
            HistoryText = "No training history yet.";
            ErrorProgressPoints = "0,132 240,132";
            AnalysisText = $"{_definition!.ToSummary()}{Environment.NewLine}{_patternSet!.ToSummary()}";
            ConsoleText = consoleMessage;
        });
    }

    private void EnsureContext(bool resetWeights, bool syncControlsFromEditor = false)
    {
        var parsedDefinition = BasicPropNetworkConfigParser.Parse(ConfigText, SampleTitle);
        if (syncControlsFromEditor)
        {
            SyncControlsFromDefinition(parsedDefinition);
        }

        var effectiveDefinition = ApplyControlOverrides(parsedDefinition);
        var parsedPatterns = PatternSetParser.Parse(PatternText, effectiveDefinition.Name);
        var signature = BuildSignature(effectiveDefinition);

        if (resetWeights || _engine is null || _engineSignature != signature)
        {
            WeightSet? preservedWeights = null;
            if (!resetWeights && _engine is not null && CanReuseWeights(effectiveDefinition, _engine.Weights))
            {
                preservedWeights = _engine.Weights.Clone();
            }

            _engine = new SignalWeaveEngine(effectiveDefinition, preservedWeights, seed: 42);
            _engineSignature = signature;
        }

        _definition = effectiveDefinition;
        _patternSet = parsedPatterns;
        SampleTitle = effectiveDefinition.Name;

        UpdatePatternOptions(parsedPatterns);
        NetworkSummary = BuildNetworkSummary(effectiveDefinition, parsedPatterns);
        WeightsText = BuildWeightsText(_engine.Weights);
        RefreshDiagram();
    }

    private void SyncControlsFromDefinition(NetworkDefinition definition)
    {
        SelectedLearningRate = PickNearest(_learningRateOptions, definition.LearningRate);
        SelectedMomentum = PickNearest(_momentumOptions, definition.Momentum);
        SelectedLearningSteps = PickNearest(_learningStepOptions, definition.MaxEpochs);
        SelectedWeightRange = PickWeightRange(definition.RandomWeightRange);
        BatchUpdate = definition.UpdateMode == UpdateMode.Batch;
        CrossEntropy = definition.CostFunction == CostFunction.CrossEntropy;
    }

    private NetworkDefinition ApplyControlOverrides(NetworkDefinition definition)
    {
        return new NetworkDefinition
        {
            Name = definition.Name,
            NetworkKind = definition.NetworkKind,
            InputUnits = definition.InputUnits,
            HiddenUnits = definition.HiddenUnits,
            OutputUnits = definition.OutputUnits,
            UseInputBias = definition.UseInputBias,
            UseHiddenBias = definition.UseHiddenBias,
            LearningRate = ParseDouble(SelectedLearningRate),
            Momentum = ParseDouble(SelectedMomentum),
            RandomWeightRange = ParseWeightRange(SelectedWeightRange),
            SigmoidPrimeOffset = definition.SigmoidPrimeOffset,
            MaxEpochs = ParseInt(SelectedLearningSteps),
            ErrorThreshold = definition.ErrorThreshold,
            UpdateMode = BatchUpdate ? UpdateMode.Batch : UpdateMode.Pattern,
            CostFunction = CrossEntropy ? CostFunction.CrossEntropy : CostFunction.SumSquaredError
        };
    }

    private void UpdatePatternOptions(PatternSet patternSet)
    {
        PatternOptions.Clear();

        if (patternSet.Examples.Count < 24)
        {
            foreach (var example in patternSet.Examples)
            {
                PatternOptions.Add(example.Label);
            }

            CanTestOne = patternSet.Examples.Count > 0;
            SelectedPattern = PatternOptions.FirstOrDefault() ?? string.Empty;
            return;
        }

        PatternOptions.Add($"{SampleTitle} ({patternSet.Examples.Count} patterns)");
        SelectedPattern = PatternOptions[0];
        CanTestOne = false;
    }

    private void RefreshDiagram()
    {
        DiagramEdges.Clear();
        DiagramNodes.Clear();

        if (_definition is null || _engine is null)
        {
            return;
        }

        const double inputX = 130;
        const double hiddenX = 350;
        const double outputX = 570;
        const double nodeWidth = 76;
        const double nodeHeight = 42;
        const double biasWidth = 64;
        const double biasHeight = 56;

        var inputYs = BuildLane(_definition.InputUnits, 120, 350);
        var hiddenYs = BuildLane(_definition.HiddenUnits, 90, 300);
        var outputYs = BuildLane(_definition.OutputUnits, 160, 240);
        var maxWeight = CalculateMaxWeight(_engine.Weights);

        var inputBiasY = inputYs.Length == 0 ? 200 : (inputYs[0] + inputYs[^1]) / 2;
        var hiddenBiasY = hiddenYs.Length == 0 ? 200 : (hiddenYs[0] + hiddenYs[^1]) / 2;

        if (_definition.UseInputBias)
        {
            DiagramNodes.Add(new DiagramNodeItem(30, inputBiasY - (biasHeight / 2), biasWidth, biasHeight, "1.000\nBIAS", "#E7E1D5", "#746B5B"));
        }

        if (_definition.UseHiddenBias)
        {
            DiagramNodes.Add(new DiagramNodeItem(250, hiddenBiasY - (biasHeight / 2), biasWidth, biasHeight, "1.000\nBIAS", "#E7E1D5", "#746B5B"));
        }

        for (var index = 0; index < _definition.InputUnits; index++)
        {
            DiagramNodes.Add(new DiagramNodeItem(inputX, inputYs[index], nodeWidth, nodeHeight, $"I{index + 1}", "#F4F4F2", "#585858"));
        }

        for (var index = 0; index < _definition.HiddenUnits; index++)
        {
            DiagramNodes.Add(new DiagramNodeItem(hiddenX, hiddenYs[index], nodeWidth, nodeHeight, $"H{index + 1}", "#F4F4F2", "#585858"));
        }

        for (var index = 0; index < _definition.OutputUnits; index++)
        {
            DiagramNodes.Add(new DiagramNodeItem(outputX, outputYs[index], nodeWidth, nodeHeight, $"O{index + 1}", "#F4F4F2", "#585858"));
        }

        for (var source = 0; source < _engine.Weights.InputHidden.GetLength(0); source++)
        {
            var x1 = source < _definition.InputUnits ? inputX + nodeWidth : 30 + biasWidth;
            var y1 = source < _definition.InputUnits
                ? inputYs[source] + (nodeHeight / 2)
                : inputBiasY;

            for (var target = 0; target < _engine.Weights.InputHidden.GetLength(1); target++)
            {
                var weight = _engine.Weights.InputHidden[source, target];
                DiagramEdges.Add(new DiagramEdgeItem(
                    ToPoint(x1, y1),
                    ToPoint(hiddenX, hiddenYs[target] + (nodeHeight / 2)),
                    WeightColor(weight),
                    WeightThickness(weight, maxWeight),
                    false));
            }
        }

        for (var source = 0; source < _engine.Weights.HiddenOutput.GetLength(0); source++)
        {
            var x1 = source < _definition.HiddenUnits ? hiddenX + nodeWidth : 250 + biasWidth;
            var y1 = source < _definition.HiddenUnits
                ? hiddenYs[source] + (nodeHeight / 2)
                : hiddenBiasY;

            for (var target = 0; target < _engine.Weights.HiddenOutput.GetLength(1); target++)
            {
                var weight = _engine.Weights.HiddenOutput[source, target];
                DiagramEdges.Add(new DiagramEdgeItem(
                    ToPoint(x1, y1),
                    ToPoint(outputX, outputYs[target] + (nodeHeight / 2)),
                    WeightColor(weight),
                    WeightThickness(weight, maxWeight),
                    false));
            }
        }

        if (_engine.Weights.RecurrentHidden is null)
        {
            return;
        }

        for (var source = 0; source < _engine.Weights.RecurrentHidden.GetLength(0); source++)
        {
            for (var target = 0; target < _engine.Weights.RecurrentHidden.GetLength(1); target++)
            {
                var weight = _engine.Weights.RecurrentHidden[source, target];
                DiagramEdges.Add(new DiagramEdgeItem(
                    ToPoint(hiddenX + (nodeWidth / 2), hiddenYs[source] + nodeHeight + 6),
                    ToPoint(hiddenX + (nodeWidth / 2), hiddenYs[target] - 8),
                    WeightColor(weight),
                    WeightThickness(weight, maxWeight),
                    true));
            }
        }
    }

    private static string BuildNetworkSummary(NetworkDefinition definition, PatternSet patternSet)
    {
        var updateText = definition.UpdateMode == UpdateMode.Batch ? "Batch update" : "Pattern update";
        var costText = definition.CostFunction == CostFunction.CrossEntropy ? "Cross-entropy" : "Sum squared error";
        return
            $"{definition.Name}{Environment.NewLine}" +
            $"{definition.NetworkKind} | {definition.InputUnits}-{definition.HiddenUnits}-{definition.OutputUnits}{Environment.NewLine}" +
            $"{updateText} | {costText}{Environment.NewLine}" +
            $"{patternSet.ToSummary()}";
    }

    private string BuildPatternSummary()
    {
        var builder = new StringBuilder();
        builder.AppendLine(_patternSet!.ToSummary());
        builder.AppendLine();

        for (var index = 0; index < _patternSet.Examples.Count; index++)
        {
            var example = _patternSet.Examples[index];
            var resetSuffix = example.ResetsContextAfter ? "  [reset]" : string.Empty;
            builder.AppendLine(
                $"{index + 1,3} {example.Label,-16} " +
                $"in: {string.Join(" ", example.Inputs.Select(FormatNumber)),-12} " +
                $"out: {(example.Targets is null ? "-" : string.Join(" ", example.Targets.Select(FormatNumber)))}{resetSuffix}");
        }

        return builder.ToString();
    }

    private static string BuildWeightsText(WeightSet weights)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Input -> Hidden");
        builder.AppendLine(FormatMatrix(weights.InputHidden));
        builder.AppendLine();
        builder.AppendLine("Hidden -> Output");
        builder.AppendLine(FormatMatrix(weights.HiddenOutput));

        if (weights.RecurrentHidden is not null)
        {
            builder.AppendLine();
            builder.AppendLine("Hidden -> Hidden");
            builder.AppendLine(FormatMatrix(weights.RecurrentHidden));
        }

        return builder.ToString();
    }

    private static string BuildHistoryText(IReadOnlyList<TrainingPoint> history)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Cycle   TSQ");
        foreach (var point in history.TakeLast(24))
        {
            builder.AppendLine($"{point.Epoch,5}   {point.AverageError.ToString("0.000000", CultureInfo.InvariantCulture)}");
        }

        return builder.ToString();
    }

    private static string BuildErrorPolyline(IReadOnlyList<TrainingPoint> history)
    {
        if (history.Count == 0)
        {
            return "0,132 240,132";
        }

        const double width = 240;
        const double height = 132;
        var maxX = Math.Max(1, history.Count - 1);
        var maxY = Math.Max(0.01, history.Max(point => point.AverageError));

        return string.Join(
            " ",
            history.Select(point =>
            {
                var x = (point.Epoch - 1) * width / maxX;
                var y = height - ((point.AverageError / maxY) * height);
                return $"{x.ToString("0.##", CultureInfo.InvariantCulture)},{y.ToString("0.##", CultureInfo.InvariantCulture)}";
            }));
    }

    private static string BuildSinglePatternResult(TestResult result)
    {
        return
            $"Pattern: {result.Label}{Environment.NewLine}" +
            $"Inputs: {string.Join(" ", result.Inputs.Select(FormatNumber))}{Environment.NewLine}" +
            $"Outputs: {string.Join(" ", result.Outputs.Select(FormatNumber))}{Environment.NewLine}" +
            $"Hidden: {string.Join(" ", result.HiddenActivations.Select(FormatNumber))}{Environment.NewLine}" +
            $"Targets: {(result.Targets is null ? "-" : string.Join(" ", result.Targets.Select(FormatNumber)))}{Environment.NewLine}" +
            $"Error: {result.Error.ToString("0.000000", CultureInfo.InvariantCulture)}";
    }

    private static string FormatMatrix(double[,] matrix)
    {
        var builder = new StringBuilder();

        for (var row = 0; row < matrix.GetLength(0); row++)
        {
            for (var column = 0; column < matrix.GetLength(1); column++)
            {
                builder.Append(matrix[row, column].ToString("0.000000", CultureInfo.InvariantCulture).PadLeft(12));
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildSignature(NetworkDefinition definition)
    {
        return string.Join(
            "|",
            definition.Name,
            definition.NetworkKind,
            definition.InputUnits,
            definition.HiddenUnits,
            definition.OutputUnits,
            definition.UseInputBias,
            definition.UseHiddenBias,
            definition.LearningRate.ToString("R", CultureInfo.InvariantCulture),
            definition.Momentum.ToString("R", CultureInfo.InvariantCulture),
            definition.RandomWeightRange.ToString("R", CultureInfo.InvariantCulture),
            definition.MaxEpochs,
            definition.ErrorThreshold.ToString("R", CultureInfo.InvariantCulture),
            definition.UpdateMode,
            definition.CostFunction);
    }

    private static bool CanReuseWeights(NetworkDefinition definition, WeightSet weights)
    {
        var inputRows = definition.InputUnits + (definition.UseInputBias ? 1 : 0);
        var hiddenRows = definition.HiddenUnits + (definition.UseHiddenBias ? 1 : 0);

        if (weights.InputHidden.GetLength(0) != inputRows || weights.InputHidden.GetLength(1) != definition.HiddenUnits)
        {
            return false;
        }

        if (weights.HiddenOutput.GetLength(0) != hiddenRows || weights.HiddenOutput.GetLength(1) != definition.OutputUnits)
        {
            return false;
        }

        return definition.NetworkKind != NetworkKind.SimpleRecurrent ||
               (weights.RecurrentHidden is not null &&
                weights.RecurrentHidden.GetLength(0) == definition.HiddenUnits &&
                weights.RecurrentHidden.GetLength(1) == definition.HiddenUnits);
    }

    private static double[] BuildLane(int count, double start, double end)
    {
        if (count <= 1)
        {
            return [((start + end) / 2)];
        }

        var step = (end - start) / (count - 1);
        return Enumerable.Range(0, count).Select(index => start + (index * step)).ToArray();
    }

    private static double CalculateMaxWeight(WeightSet weights)
    {
        var values = Flatten(weights.InputHidden).Concat(Flatten(weights.HiddenOutput));
        if (weights.RecurrentHidden is not null)
        {
            values = values.Concat(Flatten(weights.RecurrentHidden));
        }

        return Math.Max(0.001, values.Select(Math.Abs).DefaultIfEmpty(1.0).Max());
    }

    private static IEnumerable<double> Flatten(double[,] matrix)
    {
        for (var row = 0; row < matrix.GetLength(0); row++)
        {
            for (var column = 0; column < matrix.GetLength(1); column++)
            {
                yield return matrix[row, column];
            }
        }
    }

    private static string WeightColor(double weight)
    {
        if (weight > 0)
        {
            return "#53A451";
        }

        if (weight < 0)
        {
            return "#D25555";
        }

        return "#808080";
    }

    private static double WeightThickness(double weight, double maxWeight)
    {
        return 0.8 + (Math.Abs(weight) / maxWeight * 3.8);
    }

    private static string PickNearest(IEnumerable<string> options, double value)
    {
        return options
            .OrderBy(option => Math.Abs(ParseDouble(option) - value))
            .First();
    }

    private static string PickNearest(IEnumerable<string> options, int value)
    {
        return options
            .OrderBy(option => Math.Abs(ParseInt(option) - value))
            .First();
    }

    private static string PickWeightRange(double value)
    {
        if (value <= 0.1)
        {
            return "-0.1 - 0.1";
        }

        if (value <= 1.0)
        {
            return "-1 - 1";
        }

        return "-10 - 10";
    }

    private static double ParseWeightRange(string value)
    {
        return value switch
        {
            "-0.1 - 0.1" => 0.1,
            "-1 - 1" => 1.0,
            "-10 - 10" => 10.0,
            _ => 1.0
        };
    }

    private static int ParseInt(string value)
    {
        return int.Parse(value, CultureInfo.InvariantCulture);
    }

    private static double ParseDouble(string value)
    {
        return double.Parse(value, CultureInfo.InvariantCulture);
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.000", CultureInfo.InvariantCulture);
    }

    private static string ToPoint(double x, double y)
    {
        return $"{x.ToString("0.##", CultureInfo.InvariantCulture)},{y.ToString("0.##", CultureInfo.InvariantCulture)}";
    }

    private void RunSafe(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            ConsoleText = exception.Message;
        }
    }
}

public sealed record DiagramNodeItem(double X, double Y, double Width, double Height, string Label, string Fill, string Stroke);
public sealed record DiagramEdgeItem(string StartPoint, string EndPoint, string Stroke, double Thickness, bool IsRecurrent);
