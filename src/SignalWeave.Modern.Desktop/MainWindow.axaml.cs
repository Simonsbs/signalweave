using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
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
        PlotModeComboBox.SelectedIndex = 0;
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
            new ProjectWorkspaceState(5000, 0, "Line"));
    }

    private void LoadProjectState(SignalWeaveProject project, string? path, string consoleMessage)
    {
        _currentProjectPath = path;
        _definition = project.Definition;
        _patterns = project.Patterns;
        _engine = new SignalWeaveEngine(project.Definition, project.Weights);
        _engine.RestoreCompletedCycles(project.CompletedCycles);
        _trainingHistory.Clear();
        _latestTrainingPoint = null;
        _liveTrainingHistory.Clear();

        ApplyDefinitionToControls(project.Definition);
        PatternEditorTextBox.Text = PatternSetWriter.Write(project.Patterns);
        LearningStepsComboBox.Text = project.Workspace?.LearningSteps.ToString(CultureInfo.InvariantCulture)
            ?? project.Definition.MaxEpochs.ToString(CultureInfo.InvariantCulture);
        SetPlotMode(project.Workspace?.ErrorPlotDisplayMode ?? "Line");
        UpdatePatternSelector(project.Workspace?.SelectedPatternIndex ?? 0);

        ResultsTextBox.Text = "No run executed yet.";
        LatestRunSummaryTextBlock.Text = "No run executed yet.";
        ProgressLabelTextBlock.Text = project.CompletedCycles > 0
            ? $"{project.CompletedCycles.ToString(CultureInfo.InvariantCulture)} completed cycles"
            : "Idle";
        TrainingProgressBar.Maximum = Math.Max(ParseLearningStepsFromControls(), 1);
        TrainingProgressBar.Value = 0;

        SyncNetworkKindControls();
        UpdateWorkspaceSummary();
        RenderErrorPlot(_trainingHistory);
        AppendConsole(consoleMessage);
        SetStatus(path is null
            ? "Working in an unsaved project."
            : $"Loaded project: {System.IO.Path.GetFileName(path)}");
    }

    private void ApplyDefinitionToControls(NetworkDefinition definition)
    {
        ProjectNameTextBox.Text = definition.Name;
        NetworkKindComboBox.SelectedIndex = definition.NetworkKind == NetworkKind.FeedForward ? 0 : 1;
        InputUnitsTextBox.Text = definition.InputUnits.ToString(CultureInfo.InvariantCulture);
        HiddenUnitsTextBox.Text = definition.HiddenUnits.ToString(CultureInfo.InvariantCulture);
        SecondHiddenUnitsTextBox.Text = definition.SecondHiddenUnits.ToString(CultureInfo.InvariantCulture);
        OutputUnitsTextBox.Text = definition.OutputUnits.ToString(CultureInfo.InvariantCulture);
        InputBiasCheckBox.IsChecked = definition.UseInputBias;
        HiddenBiasCheckBox.IsChecked = definition.UseHiddenBias;
        SecondHiddenBiasCheckBox.IsChecked = definition.UseSecondHiddenBias;
        LearningRateComboBox.Text = FormatNumber(definition.LearningRate);
        MomentumComboBox.Text = FormatNumber(definition.Momentum);
        WeightRangeComboBox.Text = FormatWeightRange(definition.RandomWeightRange);
        ErrorThresholdTextBox.Text = FormatNumber(definition.ErrorThreshold);
        BatchUpdateCheckBox.IsChecked = definition.UpdateMode == UpdateMode.Batch;
        CrossEntropyCheckBox.IsChecked = definition.CostFunction == CostFunction.CrossEntropy;
    }

    private void SyncNetworkKindControls()
    {
        if (NetworkKindComboBox is null || SecondHiddenUnitsTextBox is null || SecondHiddenBiasCheckBox is null)
        {
            return;
        }

        var isFeedForward = GetSelectedNetworkKind() == NetworkKind.FeedForward;
        SecondHiddenUnitsTextBox.IsEnabled = isFeedForward;
        SecondHiddenBiasCheckBox.IsEnabled = isFeedForward;

        if (!isFeedForward)
        {
            SecondHiddenUnitsTextBox.Text = "0";
            SecondHiddenBiasCheckBox.IsChecked = false;
        }
    }

    private void UpdateWorkspaceSummary()
    {
        if (_definition is null || _engine is null)
        {
            ProjectSummaryTextBlock.Text = "No active project.";
            WeightsSummaryTextBlock.Text = "No active weights.";
            ProjectStateTextBlock.Text = "No active project loaded.";
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
        ProjectStateTextBlock.Text =
            $"Project saves persist current weights, {_engine.CompletedCycles.ToString(CultureInfo.InvariantCulture)} completed cycles, " +
            $"learning steps {ParseLearningStepsFromControls().ToString(CultureInfo.InvariantCulture)}, and selected pattern {Math.Max(PatternSelectorComboBox.SelectedIndex, 0) + 1}.";

        UpdateActionAvailability();
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
    }

    private void PlotModeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        RenderErrorPlot(_trainingHistory);
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
            _trainingHistory.Clear();
            _latestTrainingPoint = null;
            ResultsTextBox.Text = "Weights reset using the current project settings.";
            LatestRunSummaryTextBlock.Text = "Weights reset.";
            TrainingProgressBar.Value = 0;
            ProgressLabelTextBlock.Text = "Idle";
            RenderErrorPlot(_trainingHistory);
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
            ResultsTextBox.Text = result.FinalRun.ToTable();
            LatestRunSummaryTextBlock.Text =
                $"Training complete. Final displayed average error: {FormatNumber(result.FinalRun.DisplayAverageError)}";
            RenderErrorPlot(_trainingHistory);
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
            ResultsTextBox.Text = BuildSingleResultText(result);
            LatestRunSummaryTextBlock.Text = $"Tested pattern {index + 1}: {result.Label}";
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
            ResultsTextBox.Text = result.ToTable();
            LatestRunSummaryTextBlock.Text = $"Test all complete. Displayed average error: {FormatNumber(result.DisplayAverageError)}";
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

        var selectedIndex = Math.Max(PatternSelectorComboBox.SelectedIndex, 0);
        UpdatePatternSelector(selectedIndex);
        UpdateWorkspaceSummary();

        if (definitionChanged || patternsChanged || !canReuseWeights)
        {
            _trainingHistory.Clear();
            RenderErrorPlot(_trainingHistory);
            if (resultsChangedTextRequired())
            {
                ResultsTextBox.Text = "Project settings updated. Run Train, Test One, or Test All to refresh results.";
                LatestRunSummaryTextBlock.Text = "Project settings updated.";
            }
        }

        bool resultsChangedTextRequired()
        {
            return true;
        }
    }

    private ProjectWorkspaceState CaptureWorkspaceState()
    {
        return new ProjectWorkspaceState(
            ParseLearningStepsFromControls(),
            Math.Max(PatternSelectorComboBox.SelectedIndex, 0),
            GetSelectedPlotMode());
    }

    private NetworkDefinition BuildDefinitionFromControls()
    {
        var networkKind = GetSelectedNetworkKind();
        var secondHiddenUnits = networkKind == NetworkKind.FeedForward
            ? ParseInt(SecondHiddenUnitsTextBox.Text, "Second hidden units")
            : 0;

        var definition = new NetworkDefinition
        {
            Name = string.IsNullOrWhiteSpace(ProjectNameTextBox.Text) ? "Untitled Modern Project" : ProjectNameTextBox.Text.Trim(),
            NetworkKind = networkKind,
            InputUnits = ParseInt(InputUnitsTextBox.Text, "Inputs"),
            HiddenUnits = ParseInt(HiddenUnitsTextBox.Text, "Hidden layer 1"),
            SecondHiddenUnits = secondHiddenUnits,
            OutputUnits = ParseInt(OutputUnitsTextBox.Text, "Outputs"),
            UseInputBias = InputBiasCheckBox.IsChecked ?? false,
            UseHiddenBias = HiddenBiasCheckBox.IsChecked ?? false,
            UseSecondHiddenBias = networkKind == NetworkKind.FeedForward && (SecondHiddenBiasCheckBox.IsChecked ?? false),
            LearningRate = ParseDouble(LearningRateComboBox.Text, "Learning rate"),
            Momentum = ParseDouble(MomentumComboBox.Text, "Momentum"),
            RandomWeightRange = ParseWeightRange(WeightRangeComboBox.Text),
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

        var width = Math.Max(ErrorPlotCanvas.Bounds.Width, 320);
        var height = Math.Max(ErrorPlotCanvas.Bounds.Height, 160);
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

        if (string.Equals(GetSelectedPlotMode(), "Dots", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var point in points)
            {
                var dot = new Ellipse
                {
                    Width = 3,
                    Height = 3,
                    Fill = Brush.Parse("#B31B1B")
                };
                Canvas.SetLeft(dot, point.X - 1.5);
                Canvas.SetTop(dot, point.Y - 1.5);
                ErrorPlotCanvas.Children.Add(dot);
            }
        }
        else
        {
            ErrorPlotCanvas.Children.Add(new Polyline
            {
                Points = [.. points],
                Stroke = Brush.Parse("#B31B1B"),
                StrokeThickness = 1.8
            });
        }
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

    private void SetPlotMode(string mode)
    {
        PlotModeComboBox.SelectedIndex = string.Equals(mode, "Dots", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private string GetSelectedPlotMode()
    {
        if (PlotModeComboBox.SelectedItem is ComboBoxItem comboBoxItem &&
            comboBoxItem.Content is string content)
        {
            return content;
        }

        return PlotModeComboBox.SelectedIndex == 1 ? "Dots" : "Line";
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

    private static string FormatNumber(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
