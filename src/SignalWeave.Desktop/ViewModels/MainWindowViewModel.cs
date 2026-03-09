using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SignalWeave.Core;

namespace SignalWeave.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private enum ControllerActivity
    {
        Idle,
        Learning,
        TestingOne,
        TestingAll
    }

    private const string InvalidValueDialogTitle = "Invalid value";
    private const string MissingPatternsDialogTitle = "No can do!";
    private const string MissingPatternsDialogMessage = "No training patterns have been provided!\nWhere are my patterns?";
    private const string StartupPatternsNote = "You must load patterns before running any simulations or analyses";
    private const string PatternsNotInitializedNote = "SimControl.checkPatternsAvailable: Patterns have not been initialized";
    private const string LoadWeightsSrnNote = "Please select Load Weights (SRN) instead";
    private const string LoadWeightsFfNote = "Please select Load Weights (FF) instead";
    private readonly string[] _learningRateOptions = ["0.1", "0.2", "0.3", "0.4", "0.5", "0.6", "0.7", "0.8", "0.9", "1.0", "5.0"];
    private readonly string[] _momentumOptions = ["0", "0.2", "0.5", "0.6", "0.7", "0.8", "0.9", "1.0"];
    private readonly string[] _learningStepOptions = ["100", "200", "500", "1000", "2000", "5000", "10000", "20000", "50000", "100000"];
    private readonly string[] _weightRangeOptions = ["-0.1 - 0.1", "-1 - 1", "-10 - 10"];

    private NetworkDefinition? _definition;
    private PatternSet? _patternSet;
    private SignalWeaveEngine? _engine;
    private RunResult? _lastRun;
    private string? _engineSignature;
    private string _patternListCaption = "Untitled";
    private bool _suppressMessageMirror;

    public MainWindowViewModel()
    {
        LearningRateOptions = new ReadOnlyCollection<string>(_learningRateOptions);
        MomentumOptions = new ReadOnlyCollection<string>(_momentumOptions);
        LearningStepOptions = new ReadOnlyCollection<string>(_learningStepOptions);
        WeightRangeOptions = new ReadOnlyCollection<string>(_weightRangeOptions);
        MessageWindow = new MessageWindowViewModel();

        ParseEditorInternal(syncControlsFromEditor: true, resetWeights: true, consoleMessage: null);
        Inform(StartupPatternsNote);
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
    public event EventHandler<FeedbackDialogRequestEventArgs>? FeedbackDialogRequested;
    [ObservableProperty]
    private string _sampleTitle = "Untitled";

    [ObservableProperty]
    private string _configText = SignalWeaveSamples.DefaultFeedForwardConfig;

    [ObservableProperty]
    private string _patternText = SignalWeaveSamples.EmptyPatterns;

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
    private string _consoleText = string.Empty;

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

    private ControllerActivity _controllerActivityState = ControllerActivity.Idle;

    public bool IsControllerIdle => _controllerActivityState == ControllerActivity.Idle;
    public bool CanAdjustControls => IsControllerIdle;
    public bool CanToggleCrossEntropy => true;
    public bool CanRunReset => IsControllerIdle;
    public bool CanRunTrain => IsControllerIdle;
    public bool CanRunTestAll => IsControllerIdle;
    public bool CanSelectPattern => IsControllerIdle;
    public bool EffectiveCanTestOne => IsControllerIdle && CanTestOne;

    [RelayCommand]
    private void LoadXorDemo()
    {
        SampleTitle = "XOR demo";
        _patternListCaption = SampleTitle;
        ConfigText = SignalWeaveSamples.XorConfig;
        PatternText = SignalWeaveSamples.XorPatterns;
        ParseEditorInternal(syncControlsFromEditor: true, resetWeights: true, consoleMessage: "Loaded XOR demo.");
    }

    [RelayCommand]
    private void LoadSrnDemo()
    {
        SampleTitle = "Echo SRN demo";
        _patternListCaption = SampleTitle;
        ConfigText = SignalWeaveSamples.EchoSrnConfig;
        PatternText = SignalWeaveSamples.EchoSrnPatterns;
        ParseEditorInternal(syncControlsFromEditor: true, resetWeights: true, consoleMessage: "Loaded SRN demo.");
    }

    [RelayCommand]
    private void ParseEditor()
    {
        _patternListCaption = SampleTitle;
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
        });
    }

    [RelayCommand]
    private async Task TrainAsync()
    {
        await RunSafeAsync(async () =>
        {
            EnsureContext(resetWeights: false);
            EnsurePatternsAvailable();

            var engine = _engine!;
            var patternSet = _patternSet!;
            var steps = GetLearningStepsValue();
            var startingCycles = engine.CompletedCycles;
            var liveHistory = new List<TrainingPoint>(steps);
            ProgressMaximum = Math.Max(engine.CompletedCycles + steps, 1);
            ProgressValue = engine.CompletedCycles;
            ProgressLabel = engine.CompletedCycles == 0
                ? "Untrained"
                : engine.CompletedCycles.ToString(CultureInfo.InvariantCulture);
            var progress = new Progress<TrainingPoint>(point =>
            {
                liveHistory.Add(point);
                var completedCycles = startingCycles + point.Epoch;
                ProgressValue = completedCycles;
                ProgressLabel = completedCycles.ToString(CultureInfo.InvariantCulture);
                ErrorProgressPoints = BuildErrorPolyline(liveHistory);
                UpdateErrorPlotScale(liveHistory);
            });

            await WithBusyControllerAsync(ControllerActivity.Learning, async () =>
            {
                var result = await Task.Run(() => engine.Train(patternSet, steps, progress));
                _lastRun = result.FinalRun;

                SetTrainedState(engine.CompletedCycles);
                HistoryText = BuildHistoryText(result.History);
                ErrorProgressPoints = BuildErrorPolyline(result.History);
                UpdateErrorPlotScale(result.History);
                WeightsText = BuildWeightsText(result.Weights);
                RefreshDiagram();
                RebuildWeightMap();
                RebuildPatternOutputs(result.FinalRun);
                BuildTimeSeriesPlot(result.FinalRun);

                ConsoleText = $"{steps}  Training steps{Environment.NewLine}Training finished";
                AnalysisText = result.FinalRun.ToTable();
            });
        });
    }

    [RelayCommand]
    private async Task TestAllAsync()
    {
        await RunSafeAsync(async () =>
        {
            EnsureContext(resetWeights: false);
            EnsurePatternsAvailable();

            var engine = _engine!;
            var patternSet = _patternSet!;

            await WithBusyControllerAsync(ControllerActivity.TestingAll, async () =>
            {
                var run = await Task.Run(() => engine.TestAll(patternSet));
                _lastRun = run;
                ConsoleText = $"Test All: Average per pattern error: {run.DisplayAverageError.ToString("0.######", CultureInfo.InvariantCulture)}";
                AnalysisText = run.ToTable();
                WeightsText = BuildWeightsText(engine.Weights);
                RefreshDiagram();
                RebuildWeightMap();
                RebuildPatternOutputs(run);
                BuildTimeSeriesPlot(run);
            });
        });
    }

    [RelayCommand]
    private async Task TestOneAsync()
    {
        await RunSafeAsync(async () =>
        {
            EnsureContext(resetWeights: false);
            if (!CanTestOne)
            {
                Inform("Test one is only available when 24 or fewer patterns are loaded.");
                return;
            }

            EnsurePatternsAvailable();
            var engine = _engine!;
            var patternSet = _patternSet!;
            var patternIndex = PatternOptions.IndexOf(SelectedPattern);
            if (patternIndex < 0)
            {
                patternIndex = 0;
            }

            await WithBusyControllerAsync(ControllerActivity.TestingOne, async () =>
            {
                var result = await Task.Run(() => engine.TestOne(patternSet, patternIndex));
                var selectorText = BasicPropDisplayFormatter.FormatPatternSelector(result.Index, result.Inputs, result.Targets);
                var resultText = BasicPropDisplayFormatter.FormatPattern(result.Outputs);
                ConsoleText = $"Pattern: \"{selectorText}\"{Environment.NewLine}Result: \"{resultText}\"";
                AnalysisText = BuildSinglePatternResult(result);
                ResultsTabIndex = 0;
            });
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
        });
    }

    [RelayCommand]
    private void ClusterOutputs()
    {
        RunSafe(() =>
        {
            EnsureContext(resetWeights: false);
            AnalysisText = _engine!.ClusterOutputs(_patternSet!).ToDisplayText();
        });
    }

    [RelayCommand]
    private void ClusterHidden()
    {
        RunSafe(() =>
        {
            EnsureContext(resetWeights: false);
            AnalysisText = _engine!.ClusterHiddenStates(_patternSet!).ToDisplayText();
        });
    }

    [RelayCommand]
    private void ShowCompatibility()
    {
        AnalysisText = CompatibilityProfile.ToDisplayText();
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
        });
    }

    private void ParseEditorInternal(bool syncControlsFromEditor, bool resetWeights, string? consoleMessage)
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
            if (!string.IsNullOrWhiteSpace(consoleMessage))
            {
                ConsoleText = consoleMessage;
            }
            else
            {
                ConsoleText = string.Empty;
            }
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
        if (effectiveDefinition.NetworkKind == NetworkKind.SimpleRecurrent)
        {
            BatchUpdate = false;
        }

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
        if (_suppressMessageMirror)
        {
            _suppressMessageMirror = false;
            return;
        }

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
        BatchUpdate = definition.NetworkKind == NetworkKind.FeedForward && definition.UpdateMode == UpdateMode.Batch;
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
            UpdateMode = definition.NetworkKind == NetworkKind.SimpleRecurrent
                ? UpdateMode.Pattern
                : BatchUpdate ? UpdateMode.Batch : UpdateMode.Pattern,
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
                PatternOptions.Add(BasicPropDisplayFormatter.FormatPatternSelector(PatternOptions.Count, example.Inputs, example.Targets));
            }

            CanTestOne = patternSet.Examples.Count > 0;
            SelectedPattern = PatternOptions.FirstOrDefault() ?? string.Empty;
            return;
        }

        PatternOptions.Add(_patternListCaption);
        SelectedPattern = PatternOptions[0];
        CanTestOne = patternSet.Examples.Count is > 0 and <= 24;
    }

    private void UpdateWeightLayerOptions(WeightSet weights)
    {
        var current = SelectedWeightLayer;
        WeightLayerOptions.Clear();

        if (weights.HiddenOutput.Length == 0 && weights.HiddenHidden is null && weights.RecurrentHidden is null)
        {
            WeightLayerOptions.Add("Input -> Output");
        }
        else if (weights.HiddenHidden is not null)
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

        const double inputX = 165;
        const double hiddenX = 285;
        const double hidden2X = 405;
        const double outputX = 525;
        const double nodeWidth = 48;
        const double nodeHeight = 40;
        const double biasWidth = 50;
        const double biasHeight = 54;
        const double inputBiasX = 105;
        const double hiddenBiasX = 225;
        const double secondHiddenBiasX = 345;

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

        if (_definition.IsDirectFeedForward)
        {
            for (var index = 0; index < _definition.InputUnits; index++)
            {
                DiagramNodes.Add(new DiagramNodeItem(inputX, inputYs[index], nodeWidth, nodeHeight, string.Empty, "#F4F4F2", "#585858"));
            }

            for (var index = 0; index < _definition.OutputUnits; index++)
            {
                DiagramNodes.Add(new DiagramNodeItem(outputX, outputYs[index], nodeWidth, nodeHeight, string.Empty, "#F4F4F2", "#585858"));
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
                        ToPoint(outputX, outputYs[target] + (nodeHeight / 2)),
                        WeightColor(weight),
                        WeightThickness(weight, maxWeight),
                        false));
                }
            }

            return;
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
            DiagramNodes.Add(new DiagramNodeItem(inputX, inputYs[index], nodeWidth, nodeHeight, string.Empty, "#F4F4F2", "#585858"));
        }

        for (var index = 0; index < _definition.HiddenUnits; index++)
        {
            DiagramNodes.Add(new DiagramNodeItem(hiddenX, hiddenYs[index], nodeWidth, nodeHeight, string.Empty, "#F4F4F2", "#585858"));
        }

        if (_definition.HasSecondHiddenLayer)
        {
            for (var index = 0; index < _definition.SecondHiddenUnits; index++)
            {
                DiagramNodes.Add(new DiagramNodeItem(hidden2X, secondHiddenYs[index], nodeWidth, nodeHeight, string.Empty, "#F4F4F2", "#585858"));
            }
        }

        for (var index = 0; index < _definition.OutputUnits; index++)
        {
            DiagramNodes.Add(new DiagramNodeItem(outputX, outputYs[index], nodeWidth, nodeHeight, string.Empty, "#F4F4F2", "#585858"));
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
        var topology = definition.IsDirectFeedForward
            ? $"{definition.InputUnits}-{definition.OutputUnits}"
            : definition.HasSecondHiddenLayer
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

        PatternOutputSummary = $"Average error: {run.DisplayAverageError.ToString("0.000000", CultureInfo.InvariantCulture)}";
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
        });
    }

    private void BuildTimeSeriesPlot(RunResult run)
    {
        BuildTimeSeriesPlot(run, 0, scaleForPopup: false);
    }

    private static TimeSeriesPlotOption BuildTimeSeriesSeries(string id, string label, string summary, IReadOnlyList<double> values, string stroke)
    {
        return new TimeSeriesPlotOption(id, label, summary, values.ToArray(), stroke);
    }

    private TimeSeriesPlotOption BuildTimeSeriesPlot(RunResult run, int outputIndex, bool scaleForPopup)
    {
        if (run.Results.Count == 0)
        {
            var emptyOption = new TimeSeriesPlotOption(
                $"Output{outputIndex + 1}",
                $"Output{outputIndex + 1}",
                "No time series data available.",
                Array.Empty<double>(),
                "#D6453D");

            if (!scaleForPopup)
            {
                UtilityPlotMarkers.Clear();
                UtilityPlotPoints = "0,110 240,110";
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
            $"Output{outputIndex + 1}",
            $"Output{outputIndex + 1}",
            $"Time series plot of output unit {outputIndex + 1} across pattern order.",
            run.Results.Select(result => result.Outputs[outputIndex]).ToArray(),
            "#D6453D");

        if (!scaleForPopup)
        {
            UtilityPlotMarkers.Clear();
            foreach (var marker in markers)
            {
                UtilityPlotMarkers.Add(marker);
            }

            UtilityPlotPoints = points;
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

    private string BuildHiddenActivationData()
    {
        EnsureContext(resetWeights: false);
        EnsureRun();
        var rows = _engine!.GetExportHiddenActivations(_patternSet!);
        return BasicPropDisplayFormatter.FormatHiddenActivationExport(rows);
    }

    private static string BuildWeightsText(WeightSet weights)
    {
        var builder = new StringBuilder();
        builder.AppendLine(weights.HiddenOutput.Length == 0 && weights.HiddenHidden is null ? "Input -> Output" : weights.HiddenHidden is null ? "Input -> Hidden" : "Input -> Hidden1");
        builder.AppendLine(FormatMatrix(weights.InputHidden));
        if (weights.HiddenOutput.Length == 0 && weights.HiddenHidden is null)
        {
            return builder.ToString();
        }

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
        var rounded = Math.Max(2.0, RoundLegendValue(maxWeight));
        WeightLegendLeftLabel = $"<< -{rounded.ToString("0.##", CultureInfo.InvariantCulture)}";
        WeightLegendMidLeftLabel = rounded == 2.0
            ? "-1"
            : (-rounded / 2).ToString("0.##", CultureInfo.InvariantCulture);
        WeightLegendZeroLabel = "0";
        WeightLegendMidRightLabel = rounded == 2.0
            ? "1"
            : (rounded / 2).ToString("0.##", CultureInfo.InvariantCulture);
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

        if (definition.IsDirectFeedForward)
        {
            return weights.InputHidden.GetLength(0) == inputRows &&
                   weights.InputHidden.GetLength(1) == definition.OutputUnits &&
                   weights.HiddenOutput.GetLength(0) == 0 &&
                   weights.HiddenOutput.GetLength(1) == 0 &&
                   weights.HiddenHidden is null &&
                   weights.RecurrentHidden is null;
        }

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
            "Input -> Output" => (weights.InputHidden, "Input -> Output"),
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
        EnsurePatternsAvailable();
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
        catch (BasicPropModalException exception)
        {
            FeedbackDialogRequested?.Invoke(this, new FeedbackDialogRequestEventArgs(exception.Title, exception.Message));
        }
        catch (BasicPropNoteException exception)
        {
            Inform(exception.Message);
        }
        catch (Exception exception)
        {
            ConsoleText = exception.Message;
        }
    }

    private async Task RunSafeAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (BasicPropModalException exception)
        {
            FeedbackDialogRequested?.Invoke(this, new FeedbackDialogRequestEventArgs(exception.Title, exception.Message));
        }
        catch (BasicPropNoteException exception)
        {
            Inform(exception.Message);
        }
        catch (Exception exception)
        {
            ConsoleText = exception.Message;
        }
    }

    private async Task WithBusyControllerAsync(ControllerActivity activity, Func<Task> action)
    {
        SetControllerActivity(activity);
        await Task.Yield();

        try
        {
            await action();
        }
        finally
        {
            SetControllerActivity(ControllerActivity.Idle);
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

    public string GetSuggestedProjectFileName()
    {
        return $"{Slugify(SampleTitle)}.swproj.json";
    }

    public string GetSuggestedCheckpointFileName()
    {
        return $"{Slugify(SampleTitle)}.swcheckpoint.json";
    }

    public string GetSuggestedHiddenActivationFileName()
    {
        return $"{Slugify(SampleTitle)}-hidden.dat";
    }

    public PatternSet GetLoadedPatternSet()
    {
        EnsureContext(resetWeights: false);
        return _patternSet!;
    }

    public int GetCompletedCycles()
    {
        return _engine?.CompletedCycles ?? 0;
    }

    public void LoadNetworkText(string text, string? sourceName = null)
    {
        if (!string.IsNullOrWhiteSpace(sourceName))
        {
            SampleTitle = sourceName;
            _patternListCaption = sourceName;
        }

        ConfigText = text;
        ParseEditorInternal(syncControlsFromEditor: true, resetWeights: true, consoleMessage: null);
    }

    public void LoadPatternText(string text, string? sourceName = null)
    {
        PatternText = text;
        if (!string.IsNullOrWhiteSpace(sourceName))
        {
            _patternListCaption = sourceName;
        }

        if (_definition is null || _engine is null)
        {
            Inform("Load patterns failed. You must first set up a network");
            return;
        }

        PatternSet parsedPatterns;
        try
        {
            parsedPatterns = PatternSetParser.Parse(text, _definition.Name);
            parsedPatterns.ValidateAgainst(_definition, requireTargets: false);
        }
        catch
        {
            Inform("Failed to load patterns");
            return;
        }

        _patternSet = parsedPatterns;
        _lastRun = null;
        UpdatePatternOptions(parsedPatterns);
        NetworkSummary = BuildNetworkSummary(_definition, parsedPatterns);
        HistoryText = "No training history yet.";
        ErrorProgressPoints = "0,132 240,132";
        UpdateErrorPlotScale([]);
        PatternOutputRows.Clear();
        PatternOutputSummary = "No pattern outputs calculated yet.";
        UtilityPlotMarkers.Clear();
        UtilityPlotPoints = "0,110 240,110";
        UtilityPlotSummary = "No utility plot prepared.";
        AnalysisText = $"{_definition.ToSummary()}{Environment.NewLine}{parsedPatterns.ToSummary()}";
    }

    public void ApplyConfiguredNetwork(NetworkDefinition definition)
    {
        SampleTitle = definition.Name;
        _patternListCaption = definition.Name;
        ConfigText = BasicPropNetworkConfigWriter.Write(definition);
        ParseEditorInternal(syncControlsFromEditor: true, resetWeights: true, consoleMessage: null);
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
        SetUntrainedState();
        TrainButtonLabel = "Train";
        WeightsText = BuildWeightsText(_engine.Weights);
        HistoryText = "No training history yet.";
        ErrorProgressPoints = "0,132 240,132";
        RefreshDiagram();
        RebuildWeightMap();
        AnalysisText = "Weights loaded into the current network.";
    }

    public void LoadProject(SignalWeaveProject project)
    {
        SampleTitle = project.Definition.Name;
        _patternListCaption = project.Definition.Name;
        ConfigText = BasicPropNetworkConfigWriter.Write(project.Definition);
        PatternText = PatternSetWriter.Write(project.Patterns);
        ParseEditorInternal(syncControlsFromEditor: true, resetWeights: true, consoleMessage: null);

        if (project.Weights is not null)
        {
            LoadWeights(project.Weights);
        }
    }

    public void LoadCheckpoint(SignalWeaveCheckpoint checkpoint)
    {
        SampleTitle = checkpoint.Definition.Name;
        _patternListCaption = checkpoint.Definition.Name;
        ConfigText = BasicPropNetworkConfigWriter.Write(checkpoint.Definition);
        PatternText = PatternSetWriter.Write(checkpoint.Patterns);
        ParseEditorInternal(syncControlsFromEditor: true, resetWeights: true, consoleMessage: null);
        LoadWeights(checkpoint.Weights);

        _engine!.RestoreCompletedCycles(checkpoint.CompletedCycles);
        if (checkpoint.CompletedCycles > 0)
        {
            ProgressMaximum = checkpoint.CompletedCycles;
            ProgressValue = checkpoint.CompletedCycles;
            ProgressLabel = checkpoint.CompletedCycles.ToString(CultureInfo.InvariantCulture);
        }

        AnalysisText = $"Checkpoint loaded:{Environment.NewLine}{checkpoint.CompletedCycles.ToString(CultureInfo.InvariantCulture)} completed cycles";
    }

    public bool CanLoadWeightsFromMenu(bool forSrn)
    {
        EnsureContext(resetWeights: false);

        if (_definition!.NetworkKind == NetworkKind.SimpleRecurrent && !forSrn)
        {
            Inform(LoadWeightsSrnNote);
            return false;
        }

        if (_definition.NetworkKind == NetworkKind.FeedForward && forSrn)
        {
            Inform(LoadWeightsFfNote);
            return false;
        }

        return true;
    }

    public string BuildHiddenActivationExportText()
    {
        EnsureContext(resetWeights: false);
        return BuildHiddenActivationData();
    }

    public void ReportHiddenActivationExport(string path)
    {
        AnalysisText = $"Hidden activations exported:{Environment.NewLine}{path}";
    }

    public TextReportSnapshot CreateOutputClusterReport()
    {
        EnsureContext(resetWeights: false);
        var text = _engine!.ClusterOutputs(_patternSet!).ToDisplayText();
        AnalysisText = text;
        return new TextReportSnapshot("Output Clustering", text);
    }

    public TextReportSnapshot CreateHiddenClusterReport()
    {
        EnsureContext(resetWeights: false);
        var text = _engine!.ClusterHiddenStates(_patternSet!).ToDisplayText();
        AnalysisText = text;
        return new TextReportSnapshot("Hidden-State Clustering", text);
    }

    public TextReportSnapshot CreateCompatibilityReport()
    {
        AnalysisText = CompatibilityProfile.ToDisplayText();
        return new TextReportSnapshot("Compatibility Summary", AnalysisText);
    }

    public WeightDisplaySession CreateWeightDisplaySession()
    {
        EnsureContext(resetWeights: false);
        RebuildWeightMap();
        return new WeightDisplaySession("Weights", () => _engine!.Weights.Clone());
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
            var selectorLabel = BasicPropDisplayFormatter.FormatPatternSelector(result.Index, result.Inputs, result.Targets);
            return new PatternPlotEntry(
                result.Index,
                selectorLabel,
                result.Label,
                BuildPatternChartBars("Outputs", "output", result.Outputs, "#D6453D", -0.1, 1.1),
                BuildPatternChartBars("Targets", "target", result.Targets ?? Array.Empty<double>(), "#2C67C7", -0.1, 1.1),
                BuildPatternChartBars("Inputs", "input", result.Inputs, "#2F9C42", -1.1, 1.1));
        }).ToArray();

        return new PatternPlotSession("Show Patterns and Outputs", patterns);
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
        var options = new List<TimeSeriesPlotOption>();

        for (var inputIndex = 0; inputIndex < _definition!.InputUnits; inputIndex++)
        {
            var values = run.Results.Select(result => result.Inputs[inputIndex]).ToArray();
            options.Add(BuildTimeSeriesSeries(
                $"Input{inputIndex + 1}",
                $"Input{inputIndex + 1}",
                $"Time series plot of input unit {inputIndex + 1}.",
                values,
                "#2F9C42"));
        }

        if (run.Results.Count > 0 && run.Results.All(result => result.Targets is not null))
        {
            var outputUnits = run.Results[0].Targets!.Length;
            for (var targetIndex = 0; targetIndex < outputUnits; targetIndex++)
            {
                var values = run.Results.Select(result => result.Targets![targetIndex]).ToArray();
                options.Add(BuildTimeSeriesSeries(
                    $"Target{targetIndex + 1}",
                    $"Target{targetIndex + 1}",
                    $"Time series plot of target unit {targetIndex + 1}.",
                    values,
                    "#2C67C7"));
            }
        }

        var outputCount = run.Results.FirstOrDefault()?.Outputs.Length ?? 1;
        for (var outputIndex = 0; outputIndex < outputCount; outputIndex++)
        {
            var values = run.Results.Select(result => result.Outputs[outputIndex]).ToArray();
            options.Add(BuildTimeSeriesSeries(
                $"Output{outputIndex + 1}",
                $"Output{outputIndex + 1}",
                $"Time series plot of output unit {outputIndex + 1}.",
                values,
                "#D6453D"));
        }

        return new TimeSeriesPlotSession("Time Series Plot", options, run.Results.Count);
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
    }

    partial void OnCanTestOneChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveCanTestOne));
    }

    private void SetControllerActivity(ControllerActivity value)
    {
        if (_controllerActivityState == value)
        {
            return;
        }

        _controllerActivityState = value;
        TrainButtonLabel = value == ControllerActivity.Learning ? "continue" : "Train";
        OnPropertyChanged(nameof(IsControllerIdle));
        OnPropertyChanged(nameof(CanAdjustControls));
        OnPropertyChanged(nameof(CanToggleCrossEntropy));
        OnPropertyChanged(nameof(CanRunReset));
        OnPropertyChanged(nameof(CanRunTrain));
        OnPropertyChanged(nameof(CanRunTestAll));
        OnPropertyChanged(nameof(CanSelectPattern));
        OnPropertyChanged(nameof(EffectiveCanTestOne));
    }

    private void SetUntrainedState()
    {
        ProgressMaximum = 1;
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
        TrainButtonLabel = "Train";
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

        throw new BasicPropModalException(InvalidValueDialogTitle, "An invalid value for the learning steps was given!");
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

        throw new BasicPropModalException(InvalidValueDialogTitle, message);
    }

    private void EnsurePatternsAvailable()
    {
        if (_patternSet is null)
        {
            throw new BasicPropNoteException(PatternsNotInitializedNote);
        }

        if (_patternSet.Examples.Count == 0)
        {
            throw new BasicPropModalException(MissingPatternsDialogTitle, MissingPatternsDialogMessage);
        }
    }

    private void Inform(string message)
    {
        var note = $"Note: {message}";
        _suppressMessageMirror = true;
        ConsoleText = string.IsNullOrWhiteSpace(ConsoleText)
            ? note
            : $"{ConsoleText}{Environment.NewLine}{note}";
        MessageWindow.WriteLine(note);
    }

    private sealed class BasicPropModalException(string title, string message) : InvalidOperationException(message)
    {
        public string Title { get; } = title;
    }

    private sealed class BasicPropNoteException(string message) : InvalidOperationException(message)
    {
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

    private static IReadOnlyList<PatternChartBar> BuildPatternChartBars(string chartTitle, string categoryPrefix, IReadOnlyList<double> values, string fill, double minValue, double maxValue)
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
                $"{categoryPrefix}{index + 1}",
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
public sealed record TimeSeriesPlotOption(string Id, string Label, string Summary, IReadOnlyList<double> Values, string Stroke);
public sealed record WeightDisplaySession(string Title, Func<WeightSet> WeightSource);
public sealed record TextReportSnapshot(string Title, string Text);
public sealed record PatternOutputsSnapshot(string Title, string Summary, IReadOnlyList<PatternOutputRow> Rows);
public sealed record TimeSeriesPlotSession(
    string Title,
    IReadOnlyList<TimeSeriesPlotOption> Options,
    int PatternCount);
public sealed record SurfacePlotAxisOption(string Id, string Label, int InputIndex);
public sealed record SurfacePlotZOption(string Id, string Label, bool UsesTargets, int OutputIndex);
public sealed record SurfacePlotSample(string Label, double[] Inputs, double[]? Targets, double[] Outputs);
public sealed record SurfacePlotSetupSession(
    string Title,
    IReadOnlyList<SurfacePlotAxisOption> AxisOptions,
    IReadOnlyList<SurfacePlotZOption> ZOptions,
    IReadOnlyList<SurfacePlotSample> Samples);
public sealed record SurfacePlotCell(double X, double Y, double Width, double Height, string Fill, string Tooltip, string ValueLabel);
public sealed record SurfacePlotSnapshot(
    string Title,
    string XAxisTitle,
    string YAxisTitle,
    string ZAxisTitle,
    string YAxisTopLabel,
    string YAxisMidLabel,
    string YAxisBottomLabel,
    string XAxisLeftLabel,
    string XAxisMidLabel,
    string XAxisRightLabel,
    IReadOnlyList<SurfacePlotCell> Cells);
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
