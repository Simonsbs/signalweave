using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
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
    private RunResult? _lastRun;
    private string? _engineSignature;

    public MainWindowViewModel()
    {
        LearningRateOptions = new ReadOnlyCollection<string>(_learningRateOptions);
        MomentumOptions = new ReadOnlyCollection<string>(_momentumOptions);
        LearningStepOptions = new ReadOnlyCollection<string>(_learningStepOptions);
        WeightRangeOptions = new ReadOnlyCollection<string>(_weightRangeOptions);
        MessageWindow = new MessageWindowViewModel();

        ParseEditorInternal(syncControlsFromEditor: true, resetWeights: true, consoleMessage: "Loaded XOR demo.");
    }

    public IReadOnlyList<string> LearningRateOptions { get; }
    public IReadOnlyList<string> MomentumOptions { get; }
    public IReadOnlyList<string> LearningStepOptions { get; }
    public IReadOnlyList<string> WeightRangeOptions { get; }
    public ObservableCollection<string> PatternOptions { get; } = [];
    public ObservableCollection<string> WeightLayerOptions { get; } = [];
    public ObservableCollection<DiagramEdgeItem> DiagramEdges { get; } = [];
    public ObservableCollection<DiagramNodeItem> DiagramNodes { get; } = [];
    public ObservableCollection<WeightGlyphItem> WeightGlyphs { get; } = [];
    public ObservableCollection<PatternOutputRow> PatternOutputRows { get; } = [];
    public ObservableCollection<PlotMarkerItem> UtilityPlotMarkers { get; } = [];
    public MessageWindowViewModel MessageWindow { get; }

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
    private string _trainButtonLabel = "Train";

    [ObservableProperty]
    private int _progressMaximum = 5000;

    [ObservableProperty]
    private int _progressValue;

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

    [ObservableProperty]
    private int _resultsTabIndex;

    [ObservableProperty]
    private string _selectedWeightLayer = string.Empty;

    [ObservableProperty]
    private string _weightMapSummary = "No weight map available.";

    [ObservableProperty]
    private string _patternOutputSummary = "No pattern outputs calculated yet.";

    [ObservableProperty]
    private string _utilityPlotPoints = "0,110 240,110";

    [ObservableProperty]
    private string _utilityPlotSummary = "No utility plot prepared.";

    [ObservableProperty]
    private string _weightLegendLeftLabel = "<< -1";

    [ObservableProperty]
    private string _weightLegendMidLeftLabel = "-0.5";

    [ObservableProperty]
    private string _weightLegendZeroLabel = "0";

    [ObservableProperty]
    private string _weightLegendMidRightLabel = "0.5";

    [ObservableProperty]
    private string _weightLegendRightLabel = "1 >>";

    [ObservableProperty]
    private string _errorPlotTopLabel = "1.000";

    [ObservableProperty]
    private string _errorPlotBottomRightLabel = "5000";

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
            _lastRun = null;
            SetUntrainedState();
            HistoryText = "No training history yet.";
            ErrorProgressPoints = "0,132 240,132";
            UpdateErrorPlotScale([]);
            AnalysisText = "Weights were reset using the selected range.";
            PatternOutputRows.Clear();
            PatternOutputSummary = "No pattern outputs calculated yet.";
            UtilityPlotMarkers.Clear();
            UtilityPlotPoints = "0,110 240,110";
            UtilityPlotSummary = "No utility plot prepared.";
            ConsoleText = $"Reset {_definition!.Name}.{Environment.NewLine}{_definition.ToSummary()}";
        });
    }

    [RelayCommand]
    private void Train()
    {
        RunSafe(() =>
        {
            EnsureContext(resetWeights: false);
            var steps = GetLearningStepsValue();
            var result = _engine!.Train(_patternSet!, steps);
            _lastRun = result.FinalRun;

            SetTrainedState(result.History.Count);
            HistoryText = BuildHistoryText(result.History);
            ErrorProgressPoints = BuildErrorPolyline(result.History);
            UpdateErrorPlotScale(result.History);
            WeightsText = BuildWeightsText(result.Weights);
            RefreshDiagram();
            RebuildWeightMap();
            RebuildPatternOutputs(result.FinalRun);
            BuildTimeSeriesPlot(result.FinalRun);

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
            _lastRun = run;
            ConsoleText = $"Test all completed for {_definition!.Name}.{Environment.NewLine}Average error: {run.AverageError.ToString("0.000000", CultureInfo.InvariantCulture)}";
            AnalysisText = run.ToTable();
            WeightsText = BuildWeightsText(_engine.Weights);
            RefreshDiagram();
            RebuildWeightMap();
            RebuildPatternOutputs(run);
            BuildTimeSeriesPlot(run);
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
            ResultsTabIndex = 0;
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
            RebuildWeightMap();
            ResultsTabIndex = 1;
            ConsoleText = "Showing current network weights.";
        });
    }

    [RelayCommand]
    private void ShowPatterns()
    {
        RunSafe(() =>
        {
            EnsureContext(resetWeights: false);
            var run = EnsureRun();
            RebuildPatternOutputs(run);
            AnalysisText = BuildPatternSummary();
            PatternOutputSummary = $"Showing {run.Results.Count} patterns with outputs.";
            ResultsTabIndex = 2;
            ConsoleText = "Showing patterns and outputs.";
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

    [RelayCommand]
    private void ShowTimeSeriesPlot()
    {
        RunSafe(() =>
        {
            EnsureContext(resetWeights: false);
            var run = EnsureRun();
            BuildTimeSeriesPlot(run);
            ResultsTabIndex = 3;
            ConsoleText = "Showing time series plot for output unit 1.";
        });
    }

    [RelayCommand]
    private void Show3DPlot()
    {
        RunSafe(() =>
        {
            EnsureContext(resetWeights: false);
            var run = EnsureRun();
            BuildScatterPlot(run);
            ResultsTabIndex = 3;
            ConsoleText = "Showing projected 3D plot from hidden/output activations.";
        });
    }

    [RelayCommand]
    private void ExportHiddenActivations()
    {
        RunSafe(() =>
        {
            EnsureContext(resetWeights: false);
            var run = EnsureRun();
            var path = Path.Combine(Path.GetTempPath(), $"signalweave-hidden-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            File.WriteAllText(path, BuildHiddenActivationCsv(run));
            ConsoleText = $"Exported hidden activations to {path}";
            AnalysisText = $"Hidden activations exported:{Environment.NewLine}{path}";
        });
    }

    private void ParseEditorInternal(bool syncControlsFromEditor, bool resetWeights, string consoleMessage)
    {
        RunSafe(() =>
        {
            EnsureContext(resetWeights, syncControlsFromEditor);
            SetUntrainedState();
            HistoryText = "No training history yet.";
            ErrorProgressPoints = "0,132 240,132";
            UpdateErrorPlotScale([]);
            AnalysisText = $"{_definition!.ToSummary()}{Environment.NewLine}{_patternSet!.ToSummary()}";
            RebuildWeightMap();
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
        UpdateWeightLayerOptions(_engine.Weights);
        NetworkSummary = BuildNetworkSummary(effectiveDefinition, parsedPatterns);
        WeightsText = BuildWeightsText(_engine.Weights);
        RefreshDiagram();
    }

    partial void OnSelectedWeightLayerChanged(string value)
    {
        if (_engine is null || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        RebuildWeightMap();
    }

    partial void OnConsoleTextChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            MessageWindow.WriteLine(value);
        }
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
            SecondHiddenUnits = definition.SecondHiddenUnits,
            OutputUnits = definition.OutputUnits,
            UseInputBias = definition.UseInputBias,
            UseHiddenBias = definition.UseHiddenBias,
            UseSecondHiddenBias = definition.UseSecondHiddenBias,
            LearningRate = GetLearningRateValue(),
            Momentum = GetMomentumValue(),
            RandomWeightRange = ParseWeightRange(SelectedWeightRange),
            SigmoidPrimeOffset = definition.SigmoidPrimeOffset,
            MaxEpochs = GetLearningStepsValue(),
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

    private void UpdateWeightLayerOptions(WeightSet weights)
    {
        var current = SelectedWeightLayer;
        WeightLayerOptions.Clear();

        if (weights.HiddenHidden is not null)
        {
            WeightLayerOptions.Add("Input -> Hidden1");
            WeightLayerOptions.Add("Hidden1 -> Hidden2");
            WeightLayerOptions.Add("Hidden2 -> Output");
        }
        else
        {
            WeightLayerOptions.Add("Input -> Hidden");
            WeightLayerOptions.Add("Hidden -> Output");
        }

        if (weights.RecurrentHidden is not null)
        {
            WeightLayerOptions.Add("Hidden -> Hidden");
        }

        SelectedWeightLayer = WeightLayerOptions.Contains(current) ? current : WeightLayerOptions[0];
    }

    private void RefreshDiagram()
    {
        DiagramEdges.Clear();
        DiagramNodes.Clear();

        if (_definition is null || _engine is null)
        {
            UpdateWeightLegend(1.0);
            return;
        }

        const double inputX = 130;
        const double hiddenX = 280;
        const double hidden2X = 430;
        const double outputX = 580;
        const double nodeWidth = 76;
        const double nodeHeight = 42;
        const double biasWidth = 64;
        const double biasHeight = 56;
        const double inputBiasX = 30;
        const double hiddenBiasX = 180;
        const double secondHiddenBiasX = 330;

        var inputYs = BuildLane(_definition.InputUnits, 120, 350);
        var hiddenYs = BuildLane(_definition.HiddenUnits, 90, 300);
        var secondHiddenYs = _definition.HasSecondHiddenLayer
            ? BuildLane(_definition.SecondHiddenUnits, 90, 300)
            : [];
        var outputYs = BuildLane(_definition.OutputUnits, 160, 240);
        var maxWeight = CalculateMaxWeight(_engine.Weights);
        UpdateWeightLegend(maxWeight);

        var inputBiasY = inputYs.Length == 0 ? 200 : (inputYs[0] + inputYs[^1]) / 2;
        var hiddenBiasY = hiddenYs.Length == 0 ? 200 : (hiddenYs[0] + hiddenYs[^1]) / 2;
        var secondHiddenBiasY = secondHiddenYs.Length == 0 ? 200 : (secondHiddenYs[0] + secondHiddenYs[^1]) / 2;

        if (_definition.UseInputBias)
        {
            DiagramNodes.Add(new DiagramNodeItem(inputBiasX, inputBiasY - (biasHeight / 2), biasWidth, biasHeight, "1.000\nBIAS", "#E7E1D5", "#746B5B"));
        }

        if (_definition.UseHiddenBias)
        {
            var biasNodeY = _definition.HasSecondHiddenLayer ? secondHiddenBiasY : hiddenBiasY;
            DiagramNodes.Add(new DiagramNodeItem(hiddenBiasX, biasNodeY - (biasHeight / 2), biasWidth, biasHeight, "1.000\nBIAS", "#E7E1D5", "#746B5B"));
        }

        if (_definition.HasSecondHiddenLayer && _definition.UseSecondHiddenBias)
        {
            DiagramNodes.Add(new DiagramNodeItem(secondHiddenBiasX, secondHiddenBiasY - (biasHeight / 2), biasWidth, biasHeight, "1.000\nBIAS", "#E7E1D5", "#746B5B"));
        }

        for (var index = 0; index < _definition.InputUnits; index++)
        {
            DiagramNodes.Add(new DiagramNodeItem(inputX, inputYs[index], nodeWidth, nodeHeight, $"I{index + 1}", "#F4F4F2", "#585858"));
        }

        for (var index = 0; index < _definition.HiddenUnits; index++)
        {
            var label = _definition.HasSecondHiddenLayer ? $"H1-{index + 1}" : $"H{index + 1}";
            DiagramNodes.Add(new DiagramNodeItem(hiddenX, hiddenYs[index], nodeWidth, nodeHeight, label, "#F4F4F2", "#585858"));
        }

        if (_definition.HasSecondHiddenLayer)
        {
            for (var index = 0; index < _definition.SecondHiddenUnits; index++)
            {
                DiagramNodes.Add(new DiagramNodeItem(hidden2X, secondHiddenYs[index], nodeWidth, nodeHeight, $"H2-{index + 1}", "#F4F4F2", "#585858"));
            }
        }

        for (var index = 0; index < _definition.OutputUnits; index++)
        {
            DiagramNodes.Add(new DiagramNodeItem(outputX, outputYs[index], nodeWidth, nodeHeight, $"O{index + 1}", "#F4F4F2", "#585858"));
        }

        for (var source = 0; source < _engine.Weights.InputHidden.GetLength(0); source++)
        {
            var x1 = source < _definition.InputUnits ? inputX + nodeWidth : inputBiasX + biasWidth;
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

        if (_engine.Weights.HiddenHidden is not null)
        {
            for (var source = 0; source < _engine.Weights.HiddenHidden.GetLength(0); source++)
            {
                var x1 = source < _definition.HiddenUnits ? hiddenX + nodeWidth : hiddenBiasX + biasWidth;
                var y1 = source < _definition.HiddenUnits
                    ? hiddenYs[source] + (nodeHeight / 2)
                    : secondHiddenBiasY;

                for (var target = 0; target < _engine.Weights.HiddenHidden.GetLength(1); target++)
                {
                    var weight = _engine.Weights.HiddenHidden[source, target];
                    DiagramEdges.Add(new DiagramEdgeItem(
                        ToPoint(x1, y1),
                        ToPoint(hidden2X, secondHiddenYs[target] + (nodeHeight / 2)),
                        WeightColor(weight),
                        WeightThickness(weight, maxWeight),
                        false));
                }
            }
        }

        var outputSourceCount = _definition.HasSecondHiddenLayer ? _definition.SecondHiddenUnits : _definition.HiddenUnits;
        var outputSourceX = _definition.HasSecondHiddenLayer ? hidden2X : hiddenX;
        var outputSourceYs = _definition.HasSecondHiddenLayer ? secondHiddenYs : hiddenYs;
        var outputBiasX = _definition.HasSecondHiddenLayer ? secondHiddenBiasX : hiddenBiasX;
        var outputBiasY = _definition.HasSecondHiddenLayer ? secondHiddenBiasY : hiddenBiasY;

        for (var source = 0; source < _engine.Weights.HiddenOutput.GetLength(0); source++)
        {
            var x1 = source < outputSourceCount ? outputSourceX + nodeWidth : outputBiasX + biasWidth;
            var y1 = source < outputSourceCount
                ? outputSourceYs[source] + (nodeHeight / 2)
                : outputBiasY;

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
        var topology = definition.HasSecondHiddenLayer
            ? $"{definition.InputUnits}-{definition.HiddenUnits}-{definition.SecondHiddenUnits}-{definition.OutputUnits}"
            : $"{definition.InputUnits}-{definition.HiddenUnits}-{definition.OutputUnits}";
        return
            $"{definition.Name}{Environment.NewLine}" +
            $"{definition.NetworkKind} | {topology}{Environment.NewLine}" +
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

    private void RebuildPatternOutputs(RunResult run)
    {
        PatternOutputRows.Clear();

        foreach (var result in run.Results)
        {
            PatternOutputRows.Add(new PatternOutputRow(
                result.Index + 1,
                result.Label,
                string.Join(" ", result.Inputs.Select(FormatNumber)),
                result.Targets is null ? "-" : string.Join(" ", result.Targets.Select(FormatNumber)),
                string.Join(" ", result.Outputs.Select(FormatNumber)),
                result.Error.ToString("0.000000", CultureInfo.InvariantCulture)));
        }

        PatternOutputSummary = $"Average error: {run.AverageError.ToString("0.000000", CultureInfo.InvariantCulture)}";
    }

    private void RebuildWeightMap()
    {
        WeightGlyphs.Clear();

        if (_engine is null)
        {
            WeightMapSummary = "No weight map available.";
            return;
        }

        var (matrix, layerTitle) = GetSelectedWeightMatrix(_engine.Weights);
        var rows = matrix.GetLength(0);
        var columns = matrix.GetLength(1);
        var maxValue = Flatten(matrix).Select(Math.Abs).DefaultIfEmpty(1.0).Max();
        const double cellSize = 156;

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var weight = matrix[row, column];
                var normalized = Math.Abs(weight) / Math.Max(0.000001, maxValue);
                var size = 16 + (normalized * 118);
                var cellFill = ((row + column) % 2 == 0) ? "#C8C8C8" : "#D0D0D0";
                var cellX = column * cellSize;
                var cellY = row * cellSize;
                WeightGlyphs.Add(new WeightGlyphItem(
                    cellX,
                    cellY,
                    cellSize - 6,
                    cellSize - 6,
                    cellFill,
                    cellX + ((cellSize - 6 - size) / 2),
                    cellY + ((cellSize - 6 - size) / 2),
                    size,
                    size,
                    weight >= 0 ? "#020202" : "#FF1616",
                    $"{weight.ToString("0.000000", CultureInfo.InvariantCulture)}"));
            }
        }

        WeightMapSummary = $"{layerTitle} | rows={rows}, cols={columns}, max |w|={maxValue.ToString("0.000000", CultureInfo.InvariantCulture)}";
    }

    [RelayCommand]
    private void RefreshWeightMap()
    {
        RunSafe(() =>
        {
            EnsureContext(resetWeights: false);
            RebuildWeightMap();
            ResultsTabIndex = 1;
            ConsoleText = "Refreshed weight map.";
        });
    }

    private void BuildTimeSeriesPlot(RunResult run)
    {
        BuildTimeSeriesPlot(run, 0, scaleForPopup: false);
    }

    private TimeSeriesPlotOption BuildTimeSeriesSeries(RunResult run, int outputIndex)
    {
        return BuildTimeSeriesPlot(run, outputIndex, scaleForPopup: true);
    }

    private TimeSeriesPlotOption BuildTimeSeriesPlot(RunResult run, int outputIndex, bool scaleForPopup)
    {
        if (run.Results.Count == 0)
        {
            var emptyOption = new TimeSeriesPlotOption(
                $"Output {outputIndex + 1}",
                $"Output {outputIndex + 1}",
                "No time series data available.",
                scaleForPopup ? "20,210 320,210" : "0,110 240,110",
                scaleForPopup
                    ? Array.Empty<PlotMarkerItem>()
                    : Array.Empty<PlotMarkerItem>());

            if (!scaleForPopup)
            {
                UtilityPlotMarkers.Clear();
                UtilityPlotPoints = emptyOption.Points;
                UtilityPlotSummary = emptyOption.Summary;
            }

            return emptyOption;
        }

        var markers = new List<PlotMarkerItem>();
        var width = scaleForPopup ? 300.0 : 240.0;
        var height = scaleForPopup ? 190.0 : 110.0;
        var xOffset = scaleForPopup ? 20.0 : 0.0;
        var yOffset = scaleForPopup ? 20.0 : 0.0;
        var maxX = Math.Max(1, run.Results.Count - 1);
        var points = string.Join(
            " ",
            run.Results.Select(result =>
            {
                var x = xOffset + (result.Index * width / maxX);
                var y = yOffset + (height - (result.Outputs[outputIndex] * height));
                return $"{x.ToString("0.##", CultureInfo.InvariantCulture)},{y.ToString("0.##", CultureInfo.InvariantCulture)}";
            }));

        foreach (var result in run.Results)
        {
            var x = xOffset + (result.Index * width / maxX);
            var y = yOffset + (height - (result.Outputs[outputIndex] * height));
            markers.Add(new PlotMarkerItem(x, y, 6, 6, "#4E7396", result.Label));
        }

        var option = new TimeSeriesPlotOption(
            $"Output {outputIndex + 1}",
            $"Output {outputIndex + 1}",
            $"Time series plot of output unit {outputIndex + 1} across pattern order.",
            points,
            markers);

        if (!scaleForPopup)
        {
            UtilityPlotMarkers.Clear();
            foreach (var marker in markers)
            {
                UtilityPlotMarkers.Add(marker);
            }

            UtilityPlotPoints = option.Points;
            UtilityPlotSummary = option.Summary;
        }

        return option;
    }

    private void BuildScatterPlot(RunResult run)
    {
        UtilityPlotMarkers.Clear();
        UtilityPlotPoints = "0,110 240,110";

        if (run.Results.Count == 0)
        {
            UtilityPlotSummary = "No plot data available.";
            return;
        }

        var vectors = run.Results
            .Select(result => result.HiddenActivations.Length >= 2
                ? result.HiddenActivations
                : result.Outputs)
            .ToArray();

        var xs = vectors.Select(vector => vector[0]).ToArray();
        var ys = vectors.Select(vector => vector.Length > 1 ? vector[1] : 0.0).ToArray();
        var zs = vectors.Select(vector => vector.Length > 2 ? vector[2] : 0.0).ToArray();

        var minX = xs.Min();
        var maxX = xs.Max();
        var minY = ys.Min();
        var maxY = ys.Max();

        for (var index = 0; index < run.Results.Count; index++)
        {
            var projectedX = Normalize(xs[index], minX, maxX) * 220 + (zs[index] * 12);
            var projectedY = 110 - (Normalize(ys[index], minY, maxY) * 90) - (zs[index] * 8);
            UtilityPlotMarkers.Add(new PlotMarkerItem(projectedX, projectedY, 8, 8, "#B24C3D", run.Results[index].Label));
        }

        UtilityPlotSummary = "Projected 3D view using the first hidden/output dimensions.";
    }

    private static string BuildHiddenActivationCsv(RunResult run)
    {
        var builder = new StringBuilder();
        builder.Append("index,label");

        var hiddenCount = run.Results.FirstOrDefault()?.HiddenActivations.Length ?? 0;
        for (var index = 0; index < hiddenCount; index++)
        {
            builder.Append($",hidden_{index + 1}");
        }

        builder.AppendLine();

        foreach (var result in run.Results)
        {
            builder.Append(result.Index + 1);
            builder.Append(',');
            builder.Append(result.Label);

            foreach (var hidden in result.HiddenActivations)
            {
                builder.Append(',');
                builder.Append(hidden.ToString("0.000000000000", CultureInfo.InvariantCulture));
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildWeightsText(WeightSet weights)
    {
        var builder = new StringBuilder();
        builder.AppendLine(weights.HiddenHidden is null ? "Input -> Hidden" : "Input -> Hidden1");
        builder.AppendLine(FormatMatrix(weights.InputHidden));
        builder.AppendLine();

        if (weights.HiddenHidden is not null)
        {
            builder.AppendLine("Hidden1 -> Hidden2");
            builder.AppendLine(FormatMatrix(weights.HiddenHidden));
            builder.AppendLine();
            builder.AppendLine("Hidden2 -> Output");
            builder.AppendLine(FormatMatrix(weights.HiddenOutput));
        }
        else
        {
            builder.AppendLine("Hidden -> Output");
            builder.AppendLine(FormatMatrix(weights.HiddenOutput));
        }

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
            return "24,8 250,8";
        }

        const double left = 24;
        const double top = 8;
        const double width = 226;
        const double height = 108;
        var maxX = Math.Max(1, history.Count - 1);
        var maxY = Math.Max(0.01, history.Max(point => point.AverageError));

        return string.Join(
            " ",
            history.Select(point =>
            {
                var x = left + ((point.Epoch - 1) * width / maxX);
                var y = top + (height - ((point.AverageError / maxY) * height));
                return $"{x.ToString("0.##", CultureInfo.InvariantCulture)},{y.ToString("0.##", CultureInfo.InvariantCulture)}";
            }));
    }

    private void UpdateErrorPlotScale(IReadOnlyList<TrainingPoint> history)
    {
        ErrorPlotBottomRightLabel = SelectedLearningSteps;
        ErrorPlotTopLabel = history.Count == 0
            ? "1.000"
            : history.Max(point => point.AverageError).ToString("0.000", CultureInfo.InvariantCulture);
    }

    private void UpdateWeightLegend(double maxWeight)
    {
        var rounded = RoundLegendValue(maxWeight);
        WeightLegendLeftLabel = $"<< -{rounded.ToString("0.##", CultureInfo.InvariantCulture)}";
        WeightLegendMidLeftLabel = (-rounded / 2).ToString("0.##", CultureInfo.InvariantCulture);
        WeightLegendZeroLabel = "0";
        WeightLegendMidRightLabel = (rounded / 2).ToString("0.##", CultureInfo.InvariantCulture);
        WeightLegendRightLabel = $"{rounded.ToString("0.##", CultureInfo.InvariantCulture)} >>";
    }

    private static double RoundLegendValue(double value)
    {
        if (value <= 0.25)
        {
            return 0.25;
        }

        if (value <= 0.5)
        {
            return 0.5;
        }

        if (value <= 1.0)
        {
            return 1.0;
        }

        if (value <= 2.0)
        {
            return 2.0;
        }

        if (value <= 5.0)
        {
            return 5.0;
        }

        return Math.Ceiling(value);
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
            definition.SecondHiddenUnits,
            definition.OutputUnits,
            definition.UseInputBias,
            definition.UseHiddenBias,
            definition.UseSecondHiddenBias,
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

        if (weights.InputHidden.GetLength(0) != inputRows || weights.InputHidden.GetLength(1) != definition.HiddenUnits)
        {
            return false;
        }

        if (definition.HasSecondHiddenLayer)
        {
            var hiddenRows = definition.HiddenUnits + (definition.UseHiddenBias ? 1 : 0);
            var secondHiddenRows = definition.SecondHiddenUnits + (definition.UseSecondHiddenBias ? 1 : 0);

            return weights.HiddenHidden is not null &&
                   weights.HiddenHidden.GetLength(0) == hiddenRows &&
                   weights.HiddenHidden.GetLength(1) == definition.SecondHiddenUnits &&
                   weights.HiddenOutput.GetLength(0) == secondHiddenRows &&
                   weights.HiddenOutput.GetLength(1) == definition.OutputUnits &&
                   weights.RecurrentHidden is null;
        }

        var outputRows = definition.HiddenUnits + (definition.UseHiddenBias ? 1 : 0);
        if (weights.HiddenHidden is not null ||
            weights.HiddenOutput.GetLength(0) != outputRows ||
            weights.HiddenOutput.GetLength(1) != definition.OutputUnits)
        {
            return false;
        }

        return definition.NetworkKind != NetworkKind.SimpleRecurrent
            ? weights.RecurrentHidden is null
            : weights.RecurrentHidden is not null &&
              weights.RecurrentHidden.GetLength(0) == definition.HiddenUnits &&
              weights.RecurrentHidden.GetLength(1) == definition.HiddenUnits;
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
        if (weights.HiddenHidden is not null)
        {
            values = values.Concat(Flatten(weights.HiddenHidden));
        }

        if (weights.RecurrentHidden is not null)
        {
            values = values.Concat(Flatten(weights.RecurrentHidden));
        }

        return Math.Max(0.001, values.Select(Math.Abs).DefaultIfEmpty(1.0).Max());
    }

    private (double[,] Matrix, string Title) GetSelectedWeightMatrix(WeightSet weights)
    {
        return SelectedWeightLayer switch
        {
            "Hidden1 -> Hidden2" when weights.HiddenHidden is not null => (weights.HiddenHidden, "Hidden1 -> Hidden2"),
            "Hidden2 -> Output" when weights.HiddenHidden is not null => (weights.HiddenOutput, "Hidden2 -> Output"),
            "Hidden -> Output" => (weights.HiddenOutput, "Hidden -> Output"),
            "Hidden -> Hidden" when weights.RecurrentHidden is not null => (weights.RecurrentHidden, "Hidden -> Hidden"),
            "Input -> Hidden1" when weights.HiddenHidden is not null => (weights.InputHidden, "Input -> Hidden1"),
            _ => (weights.InputHidden, "Input -> Hidden")
        };
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

    private RunResult EnsureRun()
    {
        _lastRun ??= _engine!.TestAll(_patternSet!);
        return _lastRun;
    }

    private static double Normalize(double value, double min, double max)
    {
        if (Math.Abs(max - min) < 0.000001)
        {
            return 0.5;
        }

        return (value - min) / (max - min);
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

    public NetworkDefinition GetCurrentDefinition()
    {
        var parsedDefinition = BasicPropNetworkConfigParser.Parse(ConfigText, SampleTitle);
        var effectiveDefinition = ApplyControlOverrides(parsedDefinition);
        effectiveDefinition.Validate();
        return effectiveDefinition;
    }

    public NetworkDefinition GetLoadedDefinition()
    {
        return _definition ?? GetCurrentDefinition();
    }

    public WeightSet GetCurrentWeights()
    {
        if (_engine is null)
        {
            EnsureContext(resetWeights: true);
        }

        return _engine!.Weights.Clone();
    }

    public string GetSuggestedNetworkFileName()
    {
        return $"{Slugify(SampleTitle)}.swcfg";
    }

    public string GetSuggestedPatternFileName()
    {
        return $"{Slugify(SampleTitle)}.pat";
    }

    public string GetSuggestedWeightFileName()
    {
        return $"{Slugify(SampleTitle)}.weights.json";
    }

    public void LoadNetworkText(string text, string? sourceName = null)
    {
        if (!string.IsNullOrWhiteSpace(sourceName))
        {
            SampleTitle = sourceName;
        }

        ConfigText = text;
        ParseEditorInternal(syncControlsFromEditor: true, resetWeights: true, consoleMessage: $"Loaded network from {sourceName ?? "file"}.");
    }

    public void LoadPatternText(string text, string? sourceName = null)
    {
        PatternText = text;
        ParseEditorInternal(syncControlsFromEditor: true, resetWeights: false, consoleMessage: $"Loaded patterns from {sourceName ?? "file"}.");
    }

    public void ApplyConfiguredNetwork(NetworkDefinition definition)
    {
        SampleTitle = definition.Name;
        ConfigText = BasicPropNetworkConfigWriter.Write(definition);
        ParseEditorInternal(syncControlsFromEditor: true, resetWeights: true, consoleMessage: $"Configured network '{definition.Name}'.");
    }

    public void LoadWeights(WeightSet weights)
    {
        EnsureContext(resetWeights: false);

        if (!CanReuseWeights(_definition!, weights))
        {
            throw new InvalidOperationException("Weight file does not match the current network shape.");
        }

        _engine = new SignalWeaveEngine(_definition!, weights);
        _engineSignature = BuildSignature(_definition!);
        _lastRun = null;
        ProgressMaximum = Math.Max(1, GetLearningStepsValueOrDefault());
        ProgressValue = ProgressMaximum;
        ProgressLabel = "Loaded weights";
        TrainButtonLabel = "continue";
        WeightsText = BuildWeightsText(_engine.Weights);
        HistoryText = "No training history yet.";
        ErrorProgressPoints = "0,132 240,132";
        RefreshDiagram();
        RebuildWeightMap();
        AnalysisText = "Weights loaded into the current network.";
        ConsoleText = "Loaded weights from file.";
    }

    public WeightDisplaySession CreateWeightDisplaySession()
    {
        EnsureContext(resetWeights: false);
        RebuildWeightMap();
        return new WeightDisplaySession(_definition!.Name, _engine!.Weights.Clone());
    }

    public PatternOutputsSnapshot CreatePatternOutputsSnapshot()
    {
        EnsureContext(resetWeights: false);
        var run = EnsureRun();
        RebuildPatternOutputs(run);
        return new PatternOutputsSnapshot(
            $"{_definition!.Name} - Patterns and Outputs",
            PatternOutputSummary,
            PatternOutputRows.ToArray());
    }

    public PatternPlotSession CreatePatternPlotSession()
    {
        EnsureContext(resetWeights: false);
        var run = EnsureRun();

        var patterns = run.Results.Select(result =>
        {
            var selectorLabel = $"[{result.Index}]: {FormatVector(result.Inputs)} => {(result.Targets is null ? "-" : FormatVector(result.Targets))}";
            return new PatternPlotEntry(
                result.Index,
                selectorLabel,
                result.Label,
                BuildPatternChartBars("Outputs", result.Outputs, "#D6453D", -0.1, 1.1),
                BuildPatternChartBars("Targets", result.Targets ?? Array.Empty<double>(), "#2C67C7", -0.1, 1.1),
                BuildPatternChartBars("Inputs", result.Inputs, "#2F9C42", -1.1, 1.1));
        }).ToArray();

        return new PatternPlotSession($"{_definition!.Name} - Pattern Plot", patterns);
    }

    public PlotWindowSnapshot CreateTimeSeriesPlotSnapshot()
    {
        EnsureContext(resetWeights: false);
        var run = EnsureRun();
        BuildTimeSeriesPlot(run);
        var patternCount = Math.Max(1, run.Results.Count);
        return CreateScaledPlotSnapshot(
            $"{_definition!.Name} - Time Series Plot",
            "1.000",
            "0.500",
            "0.000",
            "1",
            ((patternCount + 1) / 2).ToString(CultureInfo.InvariantCulture),
            patternCount.ToString(CultureInfo.InvariantCulture),
            "Pattern order",
            "Output 1");
    }

    public TimeSeriesPlotSession CreateTimeSeriesPlotSession()
    {
        EnsureContext(resetWeights: false);
        var run = EnsureRun();
        var outputCount = run.Results.FirstOrDefault()?.Outputs.Length ?? 1;
        var options = Enumerable.Range(0, outputCount)
            .Select(index => BuildTimeSeriesSeries(run, index))
            .ToArray();

        return new TimeSeriesPlotSession(
            $"{_definition!.Name} - Time Series Plot",
            options,
            "1.000",
            "0.500",
            "0.000",
            "1",
            ((Math.Max(1, run.Results.Count) + 1) / 2).ToString(CultureInfo.InvariantCulture),
            Math.Max(1, run.Results.Count).ToString(CultureInfo.InvariantCulture),
            "Pattern order");
    }

    public PlotWindowSnapshot Create3DPlotSnapshot()
    {
        EnsureContext(resetWeights: false);
        var run = EnsureRun();
        BuildScatterPlot(run);
        var vectors = run.Results
            .Select(result => result.HiddenActivations.Length >= 2
                ? result.HiddenActivations
                : result.Outputs)
            .ToArray();
        var xs = vectors.Select(vector => vector[0]).ToArray();
        var ys = vectors.Select(vector => vector.Length > 1 ? vector[1] : 0.0).ToArray();
        var minX = xs.Min();
        var maxX = xs.Max();
        var minY = ys.Min();
        var maxY = ys.Max();

        return CreateScaledPlotSnapshot(
            $"{_definition!.Name} - 3D Plot",
            maxY.ToString("0.000", CultureInfo.InvariantCulture),
            ((minY + maxY) / 2).ToString("0.000", CultureInfo.InvariantCulture),
            minY.ToString("0.000", CultureInfo.InvariantCulture),
            minX.ToString("0.000", CultureInfo.InvariantCulture),
            ((minX + maxX) / 2).ToString("0.000", CultureInfo.InvariantCulture),
            maxX.ToString("0.000", CultureInfo.InvariantCulture),
            "Dimension 1",
            "Dimension 2");
    }

    public SurfacePlotSetupSession CreateSurfacePlotSetupSession()
    {
        EnsureContext(resetWeights: false);
        var run = EnsureRun();

        var axisOptions = Enumerable.Range(0, _definition!.InputUnits)
            .Select(index => new SurfacePlotAxisOption(
                $"Input{index + 1}",
                $"Input{index + 1}",
                index))
            .ToArray();

        var hasTargets = run.Results.Any(result => result.Targets is not null);
        var zOptions = new List<SurfacePlotZOption>();
        for (var index = 0; index < _definition.OutputUnits; index++)
        {
            if (hasTargets)
            {
                zOptions.Add(new SurfacePlotZOption($"Target{index + 1}", $"Target{index + 1}", true, index));
            }

            zOptions.Add(new SurfacePlotZOption($"Output{index + 1}", $"Output{index + 1}", false, index));
        }

        var samples = run.Results
            .Select(result => new SurfacePlotSample(
                result.Label,
                result.Inputs,
                result.Targets,
                result.Outputs))
            .ToArray();

        return new SurfacePlotSetupSession(
            $"{_definition.Name} - Plot Setup",
            axisOptions,
            zOptions,
            samples);
    }

    partial void OnSelectedLearningStepsChanged(string value)
    {
        if (ProgressValue == 0 && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cycles) && cycles > 0)
        {
            ProgressMaximum = cycles;
        }
    }

    private void SetUntrainedState()
    {
        ProgressMaximum = Math.Max(1, GetLearningStepsValueOrDefault());
        ProgressValue = 0;
        ProgressLabel = "Untrained";
        TrainButtonLabel = "Train";
    }

    private void SetTrainedState(int cycles)
    {
        var effectiveCycles = Math.Max(1, cycles);
        ProgressMaximum = effectiveCycles;
        ProgressValue = effectiveCycles;
        ProgressLabel = effectiveCycles.ToString(CultureInfo.InvariantCulture);
        TrainButtonLabel = "continue";
    }

    private double GetLearningRateValue()
    {
        return ValidateDoubleValue(SelectedLearningRate, "An invalid value for the learning rate was given!");
    }

    private double GetMomentumValue()
    {
        return ValidateDoubleValue(SelectedMomentum, "An invalid value for momentum was given!");
    }

    private int GetLearningStepsValue()
    {
        if (int.TryParse(SelectedLearningSteps, NumberStyles.Integer, CultureInfo.InvariantCulture, out var steps) && steps > 0)
        {
            return steps;
        }

        throw new InvalidOperationException("An invalid value for the learning steps was given!");
    }

    private int GetLearningStepsValueOrDefault()
    {
        return int.TryParse(SelectedLearningSteps, NumberStyles.Integer, CultureInfo.InvariantCulture, out var steps) && steps > 0
            ? steps
            : 1;
    }

    private static double ValidateDoubleValue(string value, string message)
    {
        if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException(message);
    }

    private static string Slugify(string value)
    {
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();

        var text = new string(chars);
        while (text.Contains("--", StringComparison.Ordinal))
        {
            text = text.Replace("--", "-", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(text.Trim('-')) ? "signalweave" : text.Trim('-');
    }

    private static IReadOnlyList<PatternChartBar> BuildPatternChartBars(string chartTitle, IReadOnlyList<double> values, string fill, double minValue, double maxValue)
    {
        const double plotHeight = 100;
        const double left = 34;
        const double top = 10;
        const double slotWidth = 60;
        const double barWidth = 22;

        var bars = new List<PatternChartBar>();
        var range = Math.Max(0.000001, maxValue - minValue);
        var zeroY = top + (maxValue / range * plotHeight);

        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index];
            var valueY = top + ((maxValue - value) / range * plotHeight);
            var barX = left + 18 + (index * slotWidth);
            var barTop = Math.Min(zeroY, valueY);
            var barHeight = Math.Max(2, Math.Abs(zeroY - valueY));

            bars.Add(new PatternChartBar(
                chartTitle,
                $"unit {index + 1}",
                barX,
                barTop,
                barWidth,
                barHeight,
                fill,
                index + 1,
                value.ToString("0.000", CultureInfo.InvariantCulture)));
        }

        return bars;
    }

    private static string FormatVector(IReadOnlyList<double> values)
    {
        return string.Join(" ", values.Select(FormatNumber));
    }

    private PlotWindowSnapshot CreateScaledPlotSnapshot(
        string title,
        string yAxisTopLabel,
        string yAxisMidLabel,
        string yAxisBottomLabel,
        string xAxisLeftLabel,
        string xAxisMidLabel,
        string xAxisRightLabel,
        string xAxisTitle,
        string yAxisTitle)
    {
        return new PlotWindowSnapshot(
            title,
            UtilityPlotSummary,
            ScalePlotPoints(UtilityPlotPoints),
            yAxisTopLabel,
            yAxisMidLabel,
            yAxisBottomLabel,
            xAxisLeftLabel,
            xAxisMidLabel,
            xAxisRightLabel,
            xAxisTitle,
            yAxisTitle,
            UtilityPlotMarkers
                .Select(marker => new PlotMarkerItem(
                    ScalePlotX(marker.X),
                    ScalePlotY(marker.Y),
                    marker.Width,
                    marker.Height,
                    marker.Fill,
                    marker.Label))
                .ToArray());
    }

    private static string ScalePlotPoints(string points)
    {
        var scaled = points
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(point =>
            {
                var parts = point.Split(',');
                if (parts.Length != 2)
                {
                    return point;
                }

                var x = double.Parse(parts[0], CultureInfo.InvariantCulture);
                var y = double.Parse(parts[1], CultureInfo.InvariantCulture);
                return $"{ScalePlotX(x).ToString("0.##", CultureInfo.InvariantCulture)},{ScalePlotY(y).ToString("0.##", CultureInfo.InvariantCulture)}";
            });

        return string.Join(" ", scaled);
    }

    private static double ScalePlotX(double x)
    {
        return 20 + (x * 300 / 240);
    }

    private static double ScalePlotY(double y)
    {
        var scaled = 20 + (y * 190 / 110);
        return Math.Clamp(scaled, 20, 210);
    }
}

public sealed record DiagramNodeItem(double X, double Y, double Width, double Height, string Label, string Fill, string Stroke);
public sealed record DiagramEdgeItem(string StartPoint, string EndPoint, string Stroke, double Thickness, bool IsRecurrent);
public sealed record WeightGlyphItem(double X, double Y, double CellWidth, double CellHeight, string CellFill, double EllipseX, double EllipseY, double EllipseWidth, double EllipseHeight, string Fill, string Tooltip);
public sealed record PatternOutputRow(int Index, string Label, string Inputs, string Targets, string Outputs, string Error);
public sealed record PlotMarkerItem(double X, double Y, double Width, double Height, string Fill, string Label);
public sealed record TimeSeriesPlotOption(string Id, string Label, string Summary, string Points, IReadOnlyList<PlotMarkerItem> Markers);
public sealed record WeightDisplaySession(string Title, WeightSet Weights);
public sealed record PatternOutputsSnapshot(string Title, string Summary, IReadOnlyList<PatternOutputRow> Rows);
public sealed record TimeSeriesPlotSession(
    string Title,
    IReadOnlyList<TimeSeriesPlotOption> Options,
    string YAxisTopLabel,
    string YAxisMidLabel,
    string YAxisBottomLabel,
    string XAxisLeftLabel,
    string XAxisMidLabel,
    string XAxisRightLabel,
    string XAxisTitle);
public sealed record SurfacePlotAxisOption(string Id, string Label, int InputIndex);
public sealed record SurfacePlotZOption(string Id, string Label, bool UsesTargets, int OutputIndex);
public sealed record SurfacePlotSample(string Label, double[] Inputs, double[]? Targets, double[] Outputs);
public sealed record SurfacePlotSetupSession(
    string Title,
    IReadOnlyList<SurfacePlotAxisOption> AxisOptions,
    IReadOnlyList<SurfacePlotZOption> ZOptions,
    IReadOnlyList<SurfacePlotSample> Samples);
public sealed record PatternChartBar(
    string ChartTitle,
    string CategoryLabel,
    double X,
    double Y,
    double Width,
    double Height,
    string Fill,
    int UnitNumber,
    string ValueLabel);
public sealed record PatternPlotEntry(
    int Index,
    string SelectorLabel,
    string Label,
    IReadOnlyList<PatternChartBar> OutputBars,
    IReadOnlyList<PatternChartBar> TargetBars,
    IReadOnlyList<PatternChartBar> InputBars);
public sealed record PatternPlotSession(string Title, IReadOnlyList<PatternPlotEntry> Patterns);
public sealed record PlotWindowSnapshot(
    string Title,
    string Summary,
    string Points,
    string YAxisTopLabel,
    string YAxisMidLabel,
    string YAxisBottomLabel,
    string XAxisLeftLabel,
    string XAxisMidLabel,
    string XAxisRightLabel,
    string XAxisTitle,
    string YAxisTitle,
    IReadOnlyList<PlotMarkerItem> Markers);
