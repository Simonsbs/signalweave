using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SignalWeave.Core;

namespace SignalWeave.Modern.Desktop;

public partial class MainWindow : Window
{
    private readonly string[] _learningRateOptions = ["0.1", "0.2", "0.3", "0.4", "0.5", "0.8", "1.0"];
    private readonly string[] _momentumOptions = ["0.0", "0.2", "0.5", "0.8", "0.9"];
    private readonly string[] _learningStepOptions = ["100", "500", "1000", "5000", "10000", "50000"];
    private readonly string[] _weightRangeOptions = ["-0.1 - 0.1", "-1 - 1", "-10 - 10"];
    private readonly object _trainingProgressGate = new();
    private readonly List<TrainingPoint> _trainingHistory = [];

    private SignalWeaveEngine? _engine;
    private NetworkDefinition? _definition;
    private PatternSet _patterns = new([]);
    private string? _currentProjectPath;
    private TestResult? _diagramResult;
    private NetworkDefinition? _graphPreviewDefinition;
    private TrainingPoint? _latestTrainingPoint;
    private List<TrainingPoint> _liveTrainingHistory = [];
    private DispatcherTimer? _trainingProgressTimer;
    private int _currentTrainingSteps;
    private bool _isBusy;

    public MainWindow()
    {
        InitializeComponent();
        PopulateStaticOptions();

        if (ErrorPlotCanvas is not null)
        {
            ErrorPlotCanvas.SizeChanged += (_, _) => RenderErrorPlot(_trainingHistory);
        }

        if (NetworkGraphCanvas is not null)
        {
            NetworkGraphCanvas.SizeChanged += (_, _) => RenderNetworkGraph();
        }

        LoadProjectState(CreateDefaultProject(), null, "Loaded the default Modern sample project.");
    }

    private sealed record PatternOption(int Index, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private void PopulateStaticOptions()
    {
        LearningRateComboBox.ItemsSource = _learningRateOptions;
        MomentumComboBox.ItemsSource = _momentumOptions;
        LearningStepsComboBox.ItemsSource = _learningStepOptions;
        WeightRangeComboBox.ItemsSource = _weightRangeOptions;
        WeightRangeComboBox.SelectedIndex = 1;
    }

    private static SignalWeaveProject CreateDefaultProject()
    {
        var definition = BasicPropNetworkConfigParser.Parse(SignalWeaveSamples.XorConfig, "XOR demo");
        var patterns = PatternSetParser.Parse(SignalWeaveSamples.XorPatterns, "xor");
        return new SignalWeaveProject(
            definition,
            patterns,
            null,
            0,
            new ProjectWorkspaceState(5000, 0, "Dots"));
    }

    private void LoadProjectState(SignalWeaveProject project, string? path, string consoleMessage)
    {
        _currentProjectPath = path;
        _definition = project.Definition;
        _patterns = project.Patterns;
        _engine = new SignalWeaveEngine(project.Definition, project.Weights);
        _engine.RestoreCompletedCycles(project.CompletedCycles);
        _diagramResult = null;
        _graphPreviewDefinition = null;
        _trainingHistory.Clear();
        _latestTrainingPoint = null;
        _liveTrainingHistory.Clear();

        ApplyDefinitionToControls(project.Definition);
        PatternEditorTextBox.Text = PatternSetWriter.Write(project.Patterns);
        LearningStepsComboBox.Text = project.Workspace?.LearningSteps.ToString(CultureInfo.InvariantCulture)
            ?? project.Definition.MaxEpochs.ToString(CultureInfo.InvariantCulture);
        UpdatePatternSelector(project.Workspace?.SelectedPatternIndex ?? 0);

        LatestRunSummaryTextBlock.Text = "No run executed yet.";
        ProgressLabelTextBlock.Text = project.CompletedCycles > 0
            ? $"{project.CompletedCycles.ToString(CultureInfo.InvariantCulture)} completed cycles"
            : "Idle";
        TrainingProgressBar.Maximum = Math.Max(ParseLearningStepsFromControls(), 1);
        TrainingProgressBar.Value = 0;

        SyncNetworkKindControls();
        UpdateWorkspaceSummary();
        RenderErrorPlot(_trainingHistory);
        RenderNetworkGraph();
        AppendConsole(consoleMessage);
        SetStatus(path is null
            ? "Working in an unsaved project."
            : $"Loaded project: {System.IO.Path.GetFileName(path)}");
    }

    private void ApplyDefinitionToControls(NetworkDefinition definition)
    {
        ProjectNameTextBox.Text = definition.Name;
        NetworkKindComboBox.SelectedIndex = definition.NetworkKind == NetworkKind.FeedForward ? 0 : 1;
        InputUnitsSlider.Value = definition.InputUnits;
        HiddenUnitsSlider.Value = definition.HiddenUnits;
        SecondHiddenUnitsSlider.Value = definition.SecondHiddenUnits;
        OutputUnitsSlider.Value = definition.OutputUnits;
        InputBiasCheckBox.IsChecked = definition.UseInputBias;
        HiddenBiasCheckBox.IsChecked = definition.UseHiddenBias;
        SecondHiddenBiasCheckBox.IsChecked = definition.UseSecondHiddenBias;
        LearningRateComboBox.Text = FormatNumber(definition.LearningRate);
        MomentumComboBox.Text = FormatNumber(definition.Momentum);
        WeightRangeComboBox.SelectedItem = FindMatchingWeightRange(definition.RandomWeightRange);
        ErrorThresholdTextBox.Text = FormatNumber(definition.ErrorThreshold);
        BatchUpdateCheckBox.IsChecked = definition.UpdateMode == UpdateMode.Batch;
        CrossEntropyCheckBox.IsChecked = definition.CostFunction == CostFunction.CrossEntropy;
        UpdateTopologyControlState();
    }

    private void SyncNetworkKindControls()
    {
        if (NetworkKindComboBox is null)
        {
            return;
        }

        UpdateTopologyControlState();
    }

    private void UpdateTopologyControlState()
    {
        if (NetworkKindComboBox is null)
        {
            return;
        }

        var isFeedForward = GetSelectedNetworkKind() == NetworkKind.FeedForward;
        HiddenUnitsSlider.Minimum = isFeedForward ? 0 : 1;

        if (!isFeedForward && HiddenUnitsSlider.Value < 1)
        {
            HiddenUnitsSlider.Value = 1;
        }

        if (HiddenUnitsSlider.Value <= 0)
        {
            HiddenBiasCheckBox.IsChecked = false;
        }

        if (!isFeedForward || HiddenUnitsSlider.Value <= 0)
        {
            SecondHiddenUnitsSlider.Value = 0;
        }

        if (SecondHiddenUnitsSlider.Value <= 0)
        {
            SecondHiddenBiasCheckBox.IsChecked = false;
        }

        SecondHiddenUnitsSlider.IsEnabled = isFeedForward && HiddenUnitsSlider.Value > 0;
        HiddenBiasCheckBox.IsEnabled = HiddenUnitsSlider.Value > 0;
        SecondHiddenBiasCheckBox.IsEnabled = isFeedForward && SecondHiddenUnitsSlider.Value > 0;
        UpdateSliderValueLabels();

        HiddenBiasCheckBox.Opacity = HiddenBiasCheckBox.IsEnabled ? 1.0 : 0.55;
        SecondHiddenBiasCheckBox.Opacity = SecondHiddenBiasCheckBox.IsEnabled ? 1.0 : 0.55;
    }

    private void UpdateWorkspaceSummary()
    {
        if (_definition is null || _engine is null)
        {
            ProjectSummaryTextBlock.Text = "No active project.";
            WeightsSummaryTextBlock.Text = "No active weights.";
            ProjectStateTextBlock.Text = "No active project loaded.";
            NetworkGraphSummaryTextBlock.Text = "No graph available.";
            CompletedCyclesTextBlock.Text = "0";
            ProjectPathTextBlock.Text = "Project file: unsaved project";
            return;
        }

        ProjectSummaryTextBlock.Text = string.Join(
            Environment.NewLine,
            _definition.ToSummary(),
            _patterns.ToSummary());

        WeightsSummaryTextBlock.Text = BuildWeightSummary(_engine.Weights, _definition);
        CompletedCyclesTextBlock.Text = _engine.CompletedCycles.ToString(CultureInfo.InvariantCulture);
        ProjectPathTextBlock.Text = $"Project file: {_currentProjectPath ?? "unsaved project"}";
        NetworkGraphSummaryTextBlock.Text = _diagramResult is null
            ? $"{_definition.TotalLayerCount}-layer topology"
            : $"Showing activations for pattern {_diagramResult.Index + 1}: {_diagramResult.Label}";
        ProjectStateTextBlock.Text =
            $"Project saves persist current weights, {_engine.CompletedCycles.ToString(CultureInfo.InvariantCulture)} completed cycles, " +
            $"learning steps {ParseLearningStepsFromControls().ToString(CultureInfo.InvariantCulture)}, and selected pattern {Math.Max(PatternSelectorComboBox.SelectedIndex, 0) + 1}.";

        UpdateActionAvailability();
    }

    private void UpdateSliderValueLabels()
    {
        InputUnitsValueTextBlock.Text = ReadSliderValue(InputUnitsSlider).ToString(CultureInfo.InvariantCulture);
        HiddenUnitsValueTextBlock.Text = ReadSliderValue(HiddenUnitsSlider).ToString(CultureInfo.InvariantCulture);
        SecondHiddenUnitsValueTextBlock.Text = ReadSliderValue(SecondHiddenUnitsSlider).ToString(CultureInfo.InvariantCulture);
        OutputUnitsValueTextBlock.Text = ReadSliderValue(OutputUnitsSlider).ToString(CultureInfo.InvariantCulture);
    }

    private void RefreshGraphPreview()
    {
        if (TryBuildPreviewDefinition(out var previewDefinition))
        {
            _graphPreviewDefinition = previewDefinition;
            NetworkGraphSummaryTextBlock.Text = $"Preview: {previewDefinition.TotalLayerCount}-layer topology";
            RenderNetworkGraph();
        }
    }

    private bool TryBuildPreviewDefinition(out NetworkDefinition definition)
    {
        try
        {
            definition = BuildDefinitionFromControls();
            return true;
        }
        catch
        {
            definition = null!;
            return false;
        }
    }

    private void UpdateActionAvailability()
    {
        var hasPatterns = _patterns.Examples.Count > 0;
        var hasPatternTargets = hasPatterns && _patterns.Examples.All(example => example.Targets is not null);

        PatternSelectorComboBox.IsEnabled = !_isBusy && hasPatterns;
        ResetButton.IsEnabled = !_isBusy && _definition is not null;
        TrainButton.IsEnabled = !_isBusy && _definition is not null && hasPatternTargets;
        TestAllButton.IsEnabled = !_isBusy && _definition is not null && hasPatterns;
        TestOneButton.IsEnabled = !_isBusy && _definition is not null && hasPatterns && PatternSelectorComboBox.SelectedIndex >= 0;
    }

    private void UpdatePatternSelector(int preferredIndex)
    {
        var options = _patterns.Examples
            .Select((example, index) => new PatternOption(index, BuildPatternLabel(example, index)))
            .ToList();

        PatternSelectorComboBox.ItemsSource = options;

        if (options.Count == 0)
        {
            PatternSelectorComboBox.SelectedIndex = -1;
            return;
        }

        PatternSelectorComboBox.SelectedIndex = Math.Clamp(preferredIndex, 0, options.Count - 1);
    }

    private static string BuildPatternLabel(PatternExample example, int index)
    {
        var inputs = string.Join(" ", example.Inputs.Select(FormatNumber));
        var targets = example.Targets is null
            ? "-"
            : string.Join(" ", example.Targets.Select(FormatNumber));
        return $"{index + 1}. {example.Label} | {inputs} => {targets}";
    }

    private static string BuildWeightSummary(WeightSet weights, NetworkDefinition definition)
    {
        var lines = new List<string>
        {
            $"Input -> {(definition.IsDirectFeedForward ? "Output" : "Hidden")} {weights.InputHidden.GetLength(0)}x{weights.InputHidden.GetLength(1)}",
            $"Range {MeasureRange(weights.InputHidden)}"
        };

        if (weights.HiddenHidden is not null)
        {
            lines.Add($"Hidden1 -> Hidden2 {weights.HiddenHidden.GetLength(0)}x{weights.HiddenHidden.GetLength(1)}");
            lines.Add($"Range {MeasureRange(weights.HiddenHidden)}");
        }

        if (weights.RecurrentHidden is not null)
        {
            lines.Add($"Recurrent {weights.RecurrentHidden.GetLength(0)}x{weights.RecurrentHidden.GetLength(1)}");
            lines.Add($"Range {MeasureRange(weights.RecurrentHidden)}");
        }

        if (!definition.IsDirectFeedForward)
        {
            lines.Add($"Hidden -> Output {weights.HiddenOutput.GetLength(0)}x{weights.HiddenOutput.GetLength(1)}");
            lines.Add($"Range {MeasureRange(weights.HiddenOutput)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string MeasureRange(double[,] matrix)
    {
        if (matrix.Length == 0)
        {
            return "0 .. 0";
        }

        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;

        foreach (var value in matrix)
        {
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }

        return $"{FormatNumber(min)} .. {FormatNumber(max)}";
    }

    private async void NewProject_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        LoadProjectState(CreateDefaultProject(), null, "Started a new project from the default Modern sample.");
        await Task.CompletedTask;
    }

    private async void LoadProject_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        try
        {
            var file = await PickOpenFileAsync(
                "Load Project",
                new FilePickerFileType("SignalWeave project") { Patterns = ["*.swproj.json"] },
                new FilePickerFileType("JSON files") { Patterns = ["*.json"] },
                new FilePickerFileType("All files") { Patterns = ["*.*"] });

            if (file is null)
            {
                return;
            }

            var path = file.TryGetLocalPath()
                ?? throw new InvalidOperationException("This platform did not provide a local file path for the selected project file.");
            LoadProjectState(SignalWeaveProjectSerializer.LoadFile(path), path, $"Loaded project from {path}.");
        }
        catch (Exception ex)
        {
            AppendConsole($"Load project failed: {ex.Message}");
            SetStatus("Load project failed.");
        }
    }

    private async void SaveProject_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await SaveProjectInternalAsync(forcePicker: string.IsNullOrWhiteSpace(_currentProjectPath));
    }

    private async void SaveProjectAs_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await SaveProjectInternalAsync(forcePicker: true);
    }

    private async Task SaveProjectInternalAsync(bool forcePicker)
    {
        if (_isBusy)
        {
            return;
        }

        try
        {
            ApplyUiToRuntime(preserveWeights: true);

            var path = _currentProjectPath;
            if (forcePicker || string.IsNullOrWhiteSpace(path))
            {
                var file = await PickSaveFileAsync(
                    "Save Project",
                    GetSuggestedProjectFileName(),
                    ".json",
                    new FilePickerFileType("SignalWeave project") { Patterns = ["*.swproj.json"] },
                    new FilePickerFileType("JSON files") { Patterns = ["*.json"] });

                if (file is null)
                {
                    return;
                }

                path = file.TryGetLocalPath()
                    ?? throw new InvalidOperationException("This platform did not provide a local file path for the selected project file.");
            }

            SignalWeaveProjectSerializer.SaveFile(
                path!,
                _definition!,
                _patterns,
                _engine?.Weights.Clone(),
                _engine?.CompletedCycles ?? 0,
                CaptureWorkspaceState());

            _currentProjectPath = path;
            UpdateWorkspaceSummary();
            AppendConsole($"Saved project to {path}.");
            SetStatus($"Saved project: {System.IO.Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            AppendConsole($"Save project failed: {ex.Message}");
            SetStatus("Save project failed.");
        }
    }

    private void Exit_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void NetworkKindComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SyncNetworkKindControls();
        RefreshGraphPreview();
    }

    private void TopologySliderChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        UpdateTopologyControlState();
        RefreshGraphPreview();
    }

    private void TopologyOptionChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        RefreshGraphPreview();
    }

    private void ClearConsole_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ConsoleTextBox.Text = string.Empty;
    }

    private void Reset_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        try
        {
            ApplyUiToRuntime(preserveWeights: false);
            _diagramResult = null;
            _trainingHistory.Clear();
            _latestTrainingPoint = null;
            LatestRunSummaryTextBlock.Text = "Weights reset.";
            TrainingProgressBar.Value = 0;
            ProgressLabelTextBlock.Text = "Idle";
            RenderErrorPlot(_trainingHistory);
            RenderNetworkGraph();
            UpdateWorkspaceSummary();
            AppendConsole("Reset network weights from the current project settings.");
            SetStatus("Weights reset.");
        }
        catch (Exception ex)
        {
            AppendConsole($"Reset failed: {ex.Message}");
            SetStatus("Reset failed.");
        }
    }

    private async void Train_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        try
        {
            ApplyUiToRuntime(preserveWeights: true);
            _patterns.ValidateAgainst(_definition!, requireTargets: true);

            var steps = ParseLearningStepsFromControls();
            _currentTrainingSteps = steps;
            _trainingHistory.Clear();
            _liveTrainingHistory = [];
            _latestTrainingPoint = null;
            _diagramResult = null;
            TrainingProgressBar.Maximum = Math.Max(steps, 1);
            TrainingProgressBar.Value = 0;
            ProgressLabelTextBlock.Text = $"0 / {steps.ToString(CultureInfo.InvariantCulture)}";
            SetBusy(true, "Training is running...");
            StartTrainingProgressPump();

            TrainResult result;
            lock (_trainingProgressGate)
            {
                _liveTrainingHistory = [];
                _latestTrainingPoint = null;
            }

            result = await Task.Factory.StartNew(() =>
            {
                var backgroundProgress = new Progress<TrainingPoint>(point =>
                {
                    lock (_trainingProgressGate)
                    {
                        _latestTrainingPoint = point;
                        _liveTrainingHistory.Add(point);
                    }
                });

                return _engine!.Train(_patterns, steps, backgroundProgress);
            }, TaskCreationOptions.LongRunning);

            StopTrainingProgressPump();
            _trainingHistory.Clear();
            _trainingHistory.AddRange(result.History);
            TrainingProgressBar.Value = result.History.Count > 0 ? result.History[^1].Epoch : 0;
            ProgressLabelTextBlock.Text = result.History.Count > 0
                ? $"{result.History[^1].Epoch.ToString(CultureInfo.InvariantCulture)} / {steps.ToString(CultureInfo.InvariantCulture)}"
                : "Idle";
            LatestRunSummaryTextBlock.Text =
                $"Training complete. Final displayed average error: {FormatNumber(result.FinalRun.DisplayAverageError)}";
            AppendConsole(result.FinalRun.ToTable());
            RenderErrorPlot(_trainingHistory);
            RenderNetworkGraph();
            UpdateWorkspaceSummary();
            AppendConsole($"Training finished after {result.History.Count.ToString(CultureInfo.InvariantCulture)} steps.");
            SetStatus("Training complete.");
        }
        catch (Exception ex)
        {
            StopTrainingProgressPump();
            AppendConsole($"Train failed: {ex.Message}");
            SetStatus("Training failed.");
        }
        finally
        {
            SetBusy(false, "Ready.");
        }
    }

    private void TestOne_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        try
        {
            ApplyUiToRuntime(preserveWeights: true);
            EnsurePatternSelectionAvailable();

            var index = PatternSelectorComboBox.SelectedIndex;
            var result = _engine!.TestOne(_patterns, index);
            _diagramResult = result;
            LatestRunSummaryTextBlock.Text = $"Tested pattern {index + 1}: {result.Label}";
            AppendConsole(BuildSingleResultText(result));
            RenderNetworkGraph();
            UpdateWorkspaceSummary();
            AppendConsole($"Tested pattern {index + 1}: {result.Label}");
            SetStatus("Test one complete.");
        }
        catch (Exception ex)
        {
            AppendConsole($"Test one failed: {ex.Message}");
            SetStatus("Test one failed.");
        }
    }

    private void TestAll_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        try
        {
            ApplyUiToRuntime(preserveWeights: true);
            EnsurePatternsAvailable();

            var result = _engine!.TestAll(_patterns);
            var selectedIndex = Math.Clamp(PatternSelectorComboBox.SelectedIndex, 0, result.Results.Count - 1);
            _diagramResult = result.Results.Count == 0 ? null : result.Results[selectedIndex];
            LatestRunSummaryTextBlock.Text = $"Test all complete. Displayed average error: {FormatNumber(result.DisplayAverageError)}";
            AppendConsole(result.ToTable());
            RenderNetworkGraph();
            UpdateWorkspaceSummary();
            AppendConsole($"Test all finished. Displayed average error: {FormatNumber(result.DisplayAverageError)}");
            SetStatus("Test all complete.");
        }
        catch (Exception ex)
        {
            AppendConsole($"Test all failed: {ex.Message}");
            SetStatus("Test all failed.");
        }
    }

    private void ApplyUiToRuntime(bool preserveWeights)
    {
        var previousDefinition = _definition;
        var nextDefinition = BuildDefinitionFromControls();
        var nextPatterns = PatternSetParser.Parse(PatternEditorTextBox.Text ?? string.Empty, nextDefinition.Name);
        var canReuseWeights = preserveWeights && CanReuseWeights(previousDefinition, nextDefinition) && _engine is not null;
        var weights = canReuseWeights ? _engine!.Weights.Clone() : null;
        var completedCycles = canReuseWeights ? _engine!.CompletedCycles : 0;
        var definitionChanged = previousDefinition is null || !DefinitionsEquivalent(previousDefinition, nextDefinition);
        var patternsChanged = !PatternSetsEquivalent(_patterns, nextPatterns);

        _definition = nextDefinition;
        _patterns = nextPatterns;
        _engine = new SignalWeaveEngine(nextDefinition, weights);
        _engine.RestoreCompletedCycles(completedCycles);
        _diagramResult = canReuseWeights ? _diagramResult : null;
        _graphPreviewDefinition = null;

        var selectedIndex = Math.Max(PatternSelectorComboBox.SelectedIndex, 0);
        UpdatePatternSelector(selectedIndex);
        UpdateWorkspaceSummary();
        RenderNetworkGraph();

        if (definitionChanged || patternsChanged || !canReuseWeights)
        {
            _trainingHistory.Clear();
            RenderErrorPlot(_trainingHistory);
            LatestRunSummaryTextBlock.Text = "Project settings updated.";
        }
    }

    private ProjectWorkspaceState CaptureWorkspaceState()
    {
        return new ProjectWorkspaceState(
            ParseLearningStepsFromControls(),
            Math.Max(PatternSelectorComboBox.SelectedIndex, 0),
            "Dots");
    }

    private NetworkDefinition BuildDefinitionFromControls()
    {
        var networkKind = GetSelectedNetworkKind();
        var secondHiddenUnits = networkKind == NetworkKind.FeedForward
            ? ReadSliderValue(SecondHiddenUnitsSlider)
            : 0;

        var definition = new NetworkDefinition
        {
            Name = string.IsNullOrWhiteSpace(ProjectNameTextBox.Text) ? "Untitled Modern Project" : ProjectNameTextBox.Text.Trim(),
            NetworkKind = networkKind,
            InputUnits = ReadSliderValue(InputUnitsSlider),
            HiddenUnits = ReadSliderValue(HiddenUnitsSlider),
            SecondHiddenUnits = secondHiddenUnits,
            OutputUnits = ReadSliderValue(OutputUnitsSlider),
            UseInputBias = InputBiasCheckBox.IsChecked ?? false,
            UseHiddenBias = HiddenUnitsSlider.Value > 0 && (HiddenBiasCheckBox.IsChecked ?? false),
            UseSecondHiddenBias = networkKind == NetworkKind.FeedForward && SecondHiddenUnitsSlider.Value > 0 && (SecondHiddenBiasCheckBox.IsChecked ?? false),
            LearningRate = ParseDouble(LearningRateComboBox.Text, "Learning rate"),
            Momentum = ParseDouble(MomentumComboBox.Text, "Momentum"),
            RandomWeightRange = ParseWeightRange(GetSelectedWeightRangeText()),
            SigmoidPrimeOffset = 0.1,
            MaxEpochs = ParseLearningStepsFromControls(),
            ErrorThreshold = ParseDouble(ErrorThresholdTextBox.Text, "Error threshold"),
            UpdateMode = BatchUpdateCheckBox.IsChecked == true ? UpdateMode.Batch : UpdateMode.Pattern,
            CostFunction = CrossEntropyCheckBox.IsChecked == true ? CostFunction.CrossEntropy : CostFunction.SumSquaredError
        };

        definition.Validate();
        return definition;
    }

    private static bool CanReuseWeights(NetworkDefinition? current, NetworkDefinition next)
    {
        if (current is null)
        {
            return false;
        }

        return current.NetworkKind == next.NetworkKind &&
               current.InputUnits == next.InputUnits &&
               current.HiddenUnits == next.HiddenUnits &&
               current.SecondHiddenUnits == next.SecondHiddenUnits &&
               current.OutputUnits == next.OutputUnits &&
               current.UseInputBias == next.UseInputBias &&
               current.UseHiddenBias == next.UseHiddenBias &&
               current.UseSecondHiddenBias == next.UseSecondHiddenBias;
    }

    private static bool DefinitionsEquivalent(NetworkDefinition left, NetworkDefinition right)
    {
        return left.Name == right.Name &&
               left.NetworkKind == right.NetworkKind &&
               left.InputUnits == right.InputUnits &&
               left.HiddenUnits == right.HiddenUnits &&
               left.SecondHiddenUnits == right.SecondHiddenUnits &&
               left.OutputUnits == right.OutputUnits &&
               left.UseInputBias == right.UseInputBias &&
               left.UseHiddenBias == right.UseHiddenBias &&
               left.UseSecondHiddenBias == right.UseSecondHiddenBias &&
               left.LearningRate.Equals(right.LearningRate) &&
               left.Momentum.Equals(right.Momentum) &&
               left.RandomWeightRange.Equals(right.RandomWeightRange) &&
               left.ErrorThreshold.Equals(right.ErrorThreshold) &&
               left.MaxEpochs == right.MaxEpochs &&
               left.UpdateMode == right.UpdateMode &&
               left.CostFunction == right.CostFunction;
    }

    private static bool PatternSetsEquivalent(PatternSet left, PatternSet right)
    {
        if (left.Examples.Count != right.Examples.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Examples.Count; index++)
        {
            var a = left.Examples[index];
            var b = right.Examples[index];

            if (!string.Equals(a.Label, b.Label, StringComparison.Ordinal) ||
                a.ResetsContextAfter != b.ResetsContextAfter ||
                !a.Inputs.SequenceEqual(b.Inputs))
            {
                return false;
            }

            if ((a.Targets is null) != (b.Targets is null))
            {
                return false;
            }

            if (a.Targets is not null && b.Targets is not null && !a.Targets.SequenceEqual(b.Targets))
            {
                return false;
            }
        }

        return true;
    }

    private void StartTrainingProgressPump()
    {
        _trainingProgressTimer?.Stop();
        _trainingProgressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _trainingProgressTimer.Tick += (_, _) =>
        {
            TrainingPoint? latestPoint;
            List<TrainingPoint> snapshot;

            lock (_trainingProgressGate)
            {
                latestPoint = _latestTrainingPoint;
                snapshot = _liveTrainingHistory.Count == 0 ? [] : [.. _liveTrainingHistory];
            }

            if (latestPoint is not null)
            {
                TrainingProgressBar.Value = latestPoint.Epoch;
                ProgressLabelTextBlock.Text =
                    $"{latestPoint.Epoch.ToString(CultureInfo.InvariantCulture)} / {_currentTrainingSteps.ToString(CultureInfo.InvariantCulture)}";
            }

            if (snapshot.Count > 0)
            {
                RenderErrorPlot(DownsampleHistory(snapshot, 1200));
            }
        };
        _trainingProgressTimer.Start();
    }

    private void StopTrainingProgressPump()
    {
        _trainingProgressTimer?.Stop();
        _trainingProgressTimer = null;
    }

    private void SetBusy(bool isBusy, string status)
    {
        _isBusy = isBusy;
        ControlTabs.IsEnabled = !isBusy;
        PatternEditorTextBox.IsEnabled = !isBusy;
        UpdateActionAvailability();
        SetStatus(status);
    }

    private void SetStatus(string status)
    {
        WorkspaceStatusTextBlock.Text = status;
    }

    private void AppendConsole(string message)
    {
        var next = string.IsNullOrWhiteSpace(ConsoleTextBox.Text)
            ? message
            : $"{ConsoleTextBox.Text}{Environment.NewLine}{message}";
        ConsoleTextBox.Text = next;
        ScrollConsoleToEnd();
    }

    private void ScrollConsoleToEnd()
    {
        Dispatcher.UIThread.Post(() =>
        {
            ConsoleTextBox.CaretIndex = ConsoleTextBox.Text?.Length ?? 0;
            var lineCount = string.IsNullOrEmpty(ConsoleTextBox.Text)
                ? 0
                : ConsoleTextBox.Text.Split(["\r\n", "\n"], StringSplitOptions.None).Length;
            if (lineCount > 0)
            {
                ConsoleTextBox.ScrollToLine(lineCount - 1);
            }
        });
    }

    private void EnsurePatternsAvailable()
    {
        if (_patterns.Examples.Count == 0)
        {
            throw new InvalidOperationException("The current project does not contain any patterns.");
        }
    }

    private void EnsurePatternSelectionAvailable()
    {
        EnsurePatternsAvailable();

        if (PatternSelectorComboBox.SelectedIndex < 0)
        {
            throw new InvalidOperationException("Select a pattern before running Test One.");
        }
    }

    private string BuildSingleResultText(TestResult result)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"Pattern: {result.Index + 1} - {result.Label}");
        builder.AppendLine($"Inputs:  {string.Join(" ", result.Inputs.Select(FormatNumber))}");
        builder.AppendLine($"Outputs: {string.Join(" ", result.Outputs.Select(FormatNumber))}");
        if (result.Targets is not null)
        {
            builder.AppendLine($"Targets: {string.Join(" ", result.Targets.Select(FormatNumber))}");
        }

        if (result.HiddenActivations.Length > 0)
        {
            builder.AppendLine($"Hidden:  {string.Join(" ", result.HiddenActivations.Select(FormatNumber))}");
        }

        builder.AppendLine($"Error:   {FormatNumber(result.Error)}");
        return builder.ToString();
    }

    private void RenderErrorPlot(IReadOnlyList<TrainingPoint> history)
    {
        if (ErrorPlotCanvas is null)
        {
            return;
        }

        ErrorPlotCanvas.Children.Clear();

        var width = Math.Max(ErrorPlotCanvas.Bounds.Width, 200);
        var height = Math.Max(ErrorPlotCanvas.Bounds.Height, 140);
        var left = 40.0;
        var top = 14.0;
        var right = width - 14.0;
        var bottom = height - 30.0;
        var plotWidth = Math.Max(right - left, 24);
        var plotHeight = Math.Max(bottom - top, 24);

        ErrorPlotCanvas.Children.Add(new Line
        {
            StartPoint = new Point(left, top),
            EndPoint = new Point(left, bottom),
            Stroke = Brush.Parse("#554A40"),
            StrokeThickness = 1.2
        });
        ErrorPlotCanvas.Children.Add(new Line
        {
            StartPoint = new Point(left, bottom),
            EndPoint = new Point(right, bottom),
            Stroke = Brush.Parse("#554A40"),
            StrokeThickness = 1.2
        });

        var topValue = history.Count == 0 ? 1.0 : Math.Max(history.Max(point => point.AverageError), 0.001);
        var pointCount = Math.Max(history.Count, 1);

        ErrorPlotCanvas.Children.Add(new TextBlock
        {
            Text = FormatNumber(topValue),
            Foreground = Brush.Parse("#6A6258")
        });

        var xLabel = new TextBlock
        {
            Text = pointCount.ToString(CultureInfo.InvariantCulture),
            Foreground = Brush.Parse("#6A6258")
        };
        Canvas.SetLeft(xLabel, right - 30);
        Canvas.SetTop(xLabel, bottom + 4);
        ErrorPlotCanvas.Children.Add(xLabel);

        if (history.Count == 0)
        {
            return;
        }

        var points = history
            .Select((point, index) =>
            {
                var x = left + (plotWidth * index / Math.Max(history.Count - 1, 1));
                var y = bottom - ((point.AverageError / topValue) * plotHeight);
                return new Point(x, Math.Clamp(y, top, bottom));
            })
            .ToList();

        foreach (var point in points)
        {
            var dot = new Ellipse
            {
                Width = 2,
                Height = 2,
                Fill = Brush.Parse("#B31B1B")
            };
            Canvas.SetLeft(dot, point.X - 1);
            Canvas.SetTop(dot, point.Y - 1);
            ErrorPlotCanvas.Children.Add(dot);
        }
    }

    private void RenderNetworkGraph()
    {
        if (NetworkGraphCanvas is null)
        {
            return;
        }

        NetworkGraphCanvas.Children.Clear();

        var definition = _graphPreviewDefinition ?? _definition;
        if (definition is null)
        {
            return;
        }

        var canvasWidth = Math.Max(NetworkGraphCanvas.Bounds.Width, 180);
        var canvasHeight = Math.Max(NetworkGraphCanvas.Bounds.Height, 140);
        var rows = BuildGraphRows(definition, _diagramResult);
        var maxNodesInRow = Math.Max(rows.Max(row => row.Values.Length), 1);
        var labelGutter = Math.Clamp(canvasWidth * 0.11, 28.0, 56.0);
        var left = labelGutter + 8.0;
        var right = canvasWidth - 10.0;
        var availableWidth = Math.Max(right - left, 36.0);
        var horizontalNodeLimit = availableWidth / (maxNodesInRow + 1.2);
        var verticalNodeLimit = (canvasHeight - 24.0) / (rows.Count + 0.65);
        var nodeSize = Math.Clamp(Math.Min(horizontalNodeLimit, verticalNodeLimit), 10.0, 40.0);
        var topInset = (nodeSize / 2.0) + 12.0;
        var bottomInset = (nodeSize / 2.0) + 10.0;
        var top = topInset;
        var bottom = Math.Max(topInset, canvasHeight - bottomInset);
        var availableHeight = Math.Max(bottom - top, 10.0);
        var rowGap = rows.Count == 1 ? 0.0 : availableHeight / (rows.Count - 1);
        var graphRows = new List<List<GraphNode>>(rows.Count);

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var y = top + (rowGap * rowIndex);
            var title = new TextBlock
            {
                Text = row.Label,
                Foreground = Brush.Parse("#6A6258"),
                FontSize = Math.Max(9, Math.Min(11, nodeSize * 0.28))
            };
            Canvas.SetLeft(title, 10);
            Canvas.SetTop(title, Math.Max(0, y - 8));
            NetworkGraphCanvas.Children.Add(title);

            var biasReservedWidth = row.HasBias ? (nodeSize * 1.05) : 0.0;
            var usableWidth = Math.Max(right - left - biasReservedWidth, nodeSize);
            var count = Math.Max(row.Values.Length, 1);
            var spacing = count == 1 ? 0.0 : (usableWidth - nodeSize) / (count - 1);
            var rowLeft = left + biasReservedWidth + ((usableWidth - ((count - 1) * spacing + nodeSize)) / 2.0);
            var nodes = new List<GraphNode>(row.Values.Length + (row.HasBias ? 1 : 0));

            if (row.HasBias)
            {
                nodes.Add(new GraphNode(left, y, nodeSize * 0.74, 1.0, "B"));
            }

            for (var index = 0; index < row.Values.Length; index++)
            {
                var x = count == 1
                    ? rowLeft
                    : rowLeft + (spacing * index);
                nodes.Add(new GraphNode(x, y, nodeSize, row.Values[index], row.ValueLabels[index]));
            }

            graphRows.Add(nodes);
        }

        DrawGraphConnections(graphRows, definition);

        foreach (var row in graphRows)
        {
            foreach (var node in row)
            {
                DrawGraphNode(node);
            }
        }
    }

    private void DrawGraphConnections(IReadOnlyList<List<GraphNode>> rows, NetworkDefinition definition)
    {
        if (rows.Count < 2)
        {
            return;
        }

        var canUseLiveWeights = _engine is not null && CanReuseWeights(_definition, definition);
        var weights = canUseLiveWeights ? _engine!.Weights : null;

        if (definition.IsDirectFeedForward)
        {
            DrawConnections(rows[1], rows[0], weights?.InputHidden, definition.UseInputBias);
            return;
        }

        if (definition.HasSecondHiddenLayer)
        {
            DrawConnections(rows[3], rows[2], weights?.InputHidden, definition.UseInputBias);
            DrawConnections(rows[2], rows[1], weights?.HiddenHidden, definition.UseHiddenBias);
            DrawConnections(rows[1], rows[0], weights?.HiddenOutput, definition.UseSecondHiddenBias);
            return;
        }

        DrawConnections(rows[2], rows[1], weights?.InputHidden, definition.UseInputBias);
        DrawConnections(rows[1], rows[0], weights?.HiddenOutput, definition.UseHiddenBias);

        if (definition.NetworkKind == NetworkKind.SimpleRecurrent && weights?.RecurrentHidden is not null)
        {
            DrawRecurrentHints(rows[1], weights.RecurrentHidden);
        }
    }

    private void DrawConnections(IReadOnlyList<GraphNode> sourceRow, IReadOnlyList<GraphNode> targetRow, double[,]? weights, bool hasBias)
    {
        var sourceOffset = hasBias ? 1 : 0;
        var targetOffset = targetRow.Count > 0 && targetRow[0].IsBias ? 1 : 0;

        for (var sourceIndex = 0; sourceIndex < sourceRow.Count; sourceIndex++)
        {
            var matrixRow = sourceIndex < sourceOffset
                ? (weights?.GetLength(0) ?? 1) - 1
                : sourceIndex - sourceOffset;

            for (var targetIndex = targetOffset; targetIndex < targetRow.Count; targetIndex++)
            {
                var targetColumn = targetIndex - targetOffset;
                var weight = weights is null ? 0.0 : weights[matrixRow, targetColumn];
                NetworkGraphCanvas!.Children.Add(new Line
                {
                    StartPoint = new Point(sourceRow[sourceIndex].CenterX, sourceRow[sourceIndex].CenterY),
                    EndPoint = new Point(targetRow[targetIndex].CenterX, targetRow[targetIndex].CenterY),
                    Stroke = weights is null ? Brush.Parse("#B5ADA3") : GetWeightBrush(weight),
                    StrokeThickness = weights is null ? 1.0 : 0.75 + (Math.Min(Math.Abs(weight), 1.5) * 1.15),
                    Opacity = weights is null ? 0.48 : 0.72
                });
            }
        }
    }

    private void DrawRecurrentHints(IReadOnlyList<GraphNode> hiddenRow, double[,] recurrentWeights)
    {
        var hiddenOffset = hiddenRow.Count > 0 && hiddenRow[0].IsBias ? 1 : 0;
        for (var index = hiddenOffset; index < hiddenRow.Count; index++)
        {
            var node = hiddenRow[index];
            var weight = recurrentWeights[index - hiddenOffset, index - hiddenOffset];
            var loop = new Ellipse
            {
                Width = node.Size * 0.75,
                Height = node.Size * 0.34,
                Stroke = GetWeightBrush(weight),
                StrokeThickness = 1.1,
                Fill = Brushes.Transparent,
                Opacity = 0.66
            };
            Canvas.SetLeft(loop, node.CenterX - (loop.Width / 2.0));
            Canvas.SetTop(loop, Math.Max(2, node.CenterY - (node.Size * 0.82)));
            NetworkGraphCanvas!.Children.Add(loop);
        }
    }

    private void DrawGraphNode(GraphNode node)
    {
        var shape = new Rectangle
        {
            Width = node.Size,
            Height = node.Size,
            RadiusX = node.IsBias ? 4 : node.Size * 0.18,
            RadiusY = node.IsBias ? 4 : node.Size * 0.18,
            Fill = node.IsBias
                ? Brush.Parse("#E7DED0")
                : new SolidColorBrush(GetActivationColor(node.Activation)),
            Stroke = Brush.Parse("#3C372F"),
            StrokeThickness = 1.05
        };

        Canvas.SetLeft(shape, node.X);
        Canvas.SetTop(shape, node.CenterY - (node.Size / 2.0));
        NetworkGraphCanvas!.Children.Add(shape);

        var label = new TextBlock
        {
            Text = node.Label,
            FontSize = Math.Max(10, node.Size * 0.2),
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush.Parse("#2A251F")
        };
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(label, node.CenterX - (label.DesiredSize.Width / 2.0));
        Canvas.SetTop(label, node.CenterY - (label.DesiredSize.Height / 2.0));
        NetworkGraphCanvas.Children.Add(label);
    }

    private IReadOnlyList<GraphRow> BuildGraphRows(NetworkDefinition definition, TestResult? diagramResult)
    {
        var rows = new List<GraphRow>
        {
            new(
                "Outputs",
                diagramResult?.Outputs ?? new double[definition.OutputUnits],
                Enumerable.Range(1, definition.OutputUnits).Select(index => $"O{index}").ToArray(),
                false)
        };

        if (definition.HasSecondHiddenLayer)
        {
            var firstHidden = diagramResult?.HiddenActivations.Take(definition.HiddenUnits).ToArray() ?? new double[definition.HiddenUnits];
            var secondHidden = diagramResult?.HiddenActivations.Skip(definition.HiddenUnits).Take(definition.SecondHiddenUnits).ToArray() ?? new double[definition.SecondHiddenUnits];
            rows.Add(new GraphRow(
                "Hidden 2",
                secondHidden,
                Enumerable.Range(1, definition.SecondHiddenUnits).Select(index => $"H2-{index}").ToArray(),
                definition.UseSecondHiddenBias));
            rows.Add(new GraphRow(
                "Hidden 1",
                firstHidden,
                Enumerable.Range(1, definition.HiddenUnits).Select(index => $"H1-{index}").ToArray(),
                definition.UseHiddenBias));
        }
        else if (!definition.IsDirectFeedForward)
        {
            rows.Add(new GraphRow(
                definition.NetworkKind == NetworkKind.SimpleRecurrent ? "Hidden / Context" : "Hidden",
                diagramResult?.HiddenActivations ?? new double[definition.HiddenUnits],
                Enumerable.Range(1, definition.HiddenUnits).Select(index => $"H{index}").ToArray(),
                definition.UseHiddenBias));
        }

        rows.Add(new GraphRow(
            "Inputs",
            diagramResult?.Inputs ?? new double[definition.InputUnits],
            Enumerable.Range(1, definition.InputUnits).Select(index => $"I{index}").ToArray(),
            definition.UseInputBias));

        return rows;
    }

    private static IBrush GetWeightBrush(double weight)
    {
        var magnitude = Math.Min(Math.Abs(weight), 1.0);
        return weight < 0
            ? new SolidColorBrush(Color.FromRgb((byte)(122 + (magnitude * 108)), 72, 72))
            : weight > 0
                ? new SolidColorBrush(Color.FromRgb(72, (byte)(118 + (magnitude * 116)), 86))
                : Brush.Parse("#8B8278");
    }

    private static Color GetActivationColor(double activation)
    {
        var clamped = Math.Clamp(activation, 0.0, 1.0);
        var red = (byte)(247 - (clamped * 82));
        var green = (byte)(243 - (clamped * 26));
        var blue = (byte)(237 - (clamped * 122));
        return Color.FromRgb(red, green, blue);
    }

    private sealed record GraphRow(string Label, double[] Values, string[] ValueLabels, bool HasBias);

    private sealed record GraphNode(double X, double CenterY, double Size, double Activation, string Label)
    {
        public bool IsBias => string.Equals(Label, "B", StringComparison.Ordinal);
        public double CenterX => X + (Size / 2.0);
    }

    private IReadOnlyList<TrainingPoint> DownsampleHistory(IReadOnlyList<TrainingPoint> history, int maxPoints)
    {
        if (history.Count <= maxPoints)
        {
            return history;
        }

        var stride = (double)(history.Count - 1) / (maxPoints - 1);
        var sampled = new List<TrainingPoint>(maxPoints);

        for (var index = 0; index < maxPoints; index++)
        {
            sampled.Add(history[(int)Math.Round(index * stride, MidpointRounding.AwayFromZero)]);
        }

        return sampled;
    }

    private async Task<IStorageFile?> PickOpenFileAsync(string title, params FilePickerFileType[] fileTypes)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypes.ToList()
        });

        return files.Count == 0 ? null : files[0];
    }

    private async Task<IStorageFile?> PickSaveFileAsync(string title, string suggestedName, string defaultExtension, params FilePickerFileType[] fileTypes)
    {
        return await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = defaultExtension,
            FileTypeChoices = fileTypes.ToList(),
            ShowOverwritePrompt = true
        });
    }

    private string GetSuggestedProjectFileName()
    {
        var rawName = string.IsNullOrWhiteSpace(ProjectNameTextBox.Text) ? "signalweave-modern-project" : ProjectNameTextBox.Text.Trim();
        var invalidChars = System.IO.Path.GetInvalidFileNameChars();
        var cleaned = new string(rawName.Select(character => invalidChars.Contains(character) ? '-' : character).ToArray());
        return $"{cleaned}.swproj.json";
    }

    private NetworkKind GetSelectedNetworkKind()
    {
        if (NetworkKindComboBox is null)
        {
            return NetworkKind.FeedForward;
        }

        return NetworkKindComboBox.SelectedIndex == 1
            ? NetworkKind.SimpleRecurrent
            : NetworkKind.FeedForward;
    }

    private int ParseLearningStepsFromControls()
    {
        return ParseInt(LearningStepsComboBox.Text, "Learning steps");
    }

    private static int ParseInt(string? value, string label)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidOperationException($"{label} must be a whole number.");
        }

        return parsed;
    }

    private static double ParseDouble(string? value, string label)
    {
        if (!double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidOperationException($"{label} must be numeric.");
        }

        return parsed;
    }

    private static double ParseWeightRange(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Weight range must be provided.");
        }

        var tokens = value
            .Replace("to", " ", StringComparison.OrdinalIgnoreCase)
            .Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => double.TryParse(token, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : (double?)null)
            .Where(parsed => parsed.HasValue)
            .Select(parsed => parsed!.Value)
            .ToArray();

        if (tokens.Length == 0)
        {
            throw new InvalidOperationException("Weight range must contain at least one numeric bound.");
        }

        return tokens.Max(bound => Math.Abs(bound));
    }

    private static string FormatWeightRange(double range)
    {
        return $"-{FormatNumber(range)} - {FormatNumber(range)}";
    }

    private static int ReadSliderValue(Slider slider)
    {
        return (int)Math.Round(slider.Value, MidpointRounding.AwayFromZero);
    }

    private string GetSelectedWeightRangeText()
    {
        return WeightRangeComboBox.SelectedItem as string
            ?? WeightRangeComboBox.SelectionBoxItem as string
            ?? _weightRangeOptions[1];
    }

    private string FindMatchingWeightRange(double range)
    {
        var desired = FormatWeightRange(range);
        return _weightRangeOptions.FirstOrDefault(option => string.Equals(option, desired, StringComparison.Ordinal))
            ?? _weightRangeOptions.OrderBy(option => Math.Abs(ParseWeightRange(option) - range)).First();
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
