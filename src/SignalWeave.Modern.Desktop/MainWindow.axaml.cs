using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SignalWeave.Core;

namespace SignalWeave.Modern.Desktop;

public partial class MainWindow : Window
{
    private const double PatternLabelColumnWidth = 120;
    private const double PatternResetColumnWidth = 56;
    private const double PatternBinaryCellWidth = 38;
    private const double PatternSeparatorColumnWidth = 12;
    private const double PatternDeleteColumnWidth = 42;

    private readonly string[] _learningRateOptions = ["0.1", "0.2", "0.3", "0.4", "0.5", "0.8", "1.0"];
    private readonly string[] _momentumOptions = ["0.0", "0.2", "0.5", "0.8", "0.9"];
    private readonly string[] _learningStepOptions = ["100", "500", "1000", "5000", "10000", "50000"];
    private readonly string[] _weightRangeOptions = ["-0.1 - 0.1", "-1 - 1", "-10 - 10"];
    private readonly object _trainingProgressGate = new();
    private readonly List<TrainingPoint> _trainingHistory = [];
    private readonly StringBuilder _consoleMarkdown = new();
    private readonly List<string> _recentProjectPaths = [];

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
    private bool _isInitializingUi = true;
    private bool _isUpdatingRecentProjects;
    private bool _isSyncingPatternText;
    private bool _isSyncingPatternTable;

    public MainWindow()
    {
        InitializeComponent();
        PopulateStaticOptions();
        LoadRecentProjectsState();
        PopulateRecentProjectOptions();

        if (ErrorPlotCanvas is not null)
        {
            ErrorPlotCanvas.SizeChanged += (_, _) => RenderErrorPlot(_trainingHistory);
        }

        if (NetworkGraphCanvas is not null)
        {
            NetworkGraphCanvas.SizeChanged += (_, _) => RenderNetworkGraph();
        }

        LoadProjectState(CreateDefaultProject(), null, "Loaded the default Modern sample project.");
        _isInitializingUi = false;
    }

    private sealed record PatternOption(int Index, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record RecentProjectOption(string Path, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record RecentProjectsState(List<string> Paths);

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

    private void LoadRecentProjectsState()
    {
        try
        {
            var path = GetRecentProjectsStatePath();
            if (!File.Exists(path))
            {
                return;
            }

            var state = JsonSerializer.Deserialize<RecentProjectsState>(File.ReadAllText(path));
            if (state?.Paths is null)
            {
                return;
            }

            _recentProjectPaths.Clear();
            _recentProjectPaths.AddRange(state.Paths
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5));
        }
        catch
        {
            _recentProjectPaths.Clear();
        }
    }

    private void SaveRecentProjectsState()
    {
        var path = GetRecentProjectsStatePath();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var state = new RecentProjectsState(_recentProjectPaths.Take(5).ToList());
        File.WriteAllText(path, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void RegisterRecentProject(string path)
    {
        _recentProjectPaths.RemoveAll(existing => string.Equals(existing, path, StringComparison.OrdinalIgnoreCase));
        _recentProjectPaths.Insert(0, path);

        if (_recentProjectPaths.Count > 5)
        {
            _recentProjectPaths.RemoveRange(5, _recentProjectPaths.Count - 5);
        }

        SaveRecentProjectsState();
        PopulateRecentProjectOptions();
    }

    private void PopulateRecentProjectOptions()
    {
        if (RecentProjectsComboBox is null)
        {
            return;
        }

        _isUpdatingRecentProjects = true;
        RecentProjectsComboBox.ItemsSource = _recentProjectPaths
            .Select(path => new RecentProjectOption(path, $"{System.IO.Path.GetFileName(path)}  |  {path}"))
            .ToList();
        RecentProjectsComboBox.SelectedIndex = -1;
        _isUpdatingRecentProjects = false;
    }

    private static string GetRecentProjectsStatePath()
    {
        return System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SignalWeave",
            "modern-ui-recents.json");
    }

    private void LoadProjectState(SignalWeaveProject project, string? path, string consoleMessage)
    {
        _currentProjectPath = path;
        if (!string.IsNullOrWhiteSpace(path))
        {
            RegisterRecentProject(path);
        }

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
        SetPatternEditorText(PatternSetWriter.Write(project.Patterns));
        LearningStepsComboBox.Text = project.Workspace?.LearningSteps.ToString(CultureInfo.InvariantCulture)
            ?? project.Definition.MaxEpochs.ToString(CultureInfo.InvariantCulture);
        UpdatePatternSelector(project.Workspace?.SelectedPatternIndex ?? 0);
        RenderPatternGraphicTable(project.Patterns);

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
        if (NetworkKindComboBox is null ||
            HiddenUnitsSlider is null ||
            SecondHiddenUnitsSlider is null ||
            HiddenBiasCheckBox is null ||
            SecondHiddenBiasCheckBox is null)
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
            return;
        }

        ProjectSummaryTextBlock.Text = string.Join(
            Environment.NewLine,
            _definition.ToSummary(),
            _patterns.ToSummary());

        WeightsSummaryTextBlock.Text = BuildWeightSummary(_engine.Weights, _definition);
        CompletedCyclesTextBlock.Text = _engine.CompletedCycles.ToString(CultureInfo.InvariantCulture);
        NetworkGraphSummaryTextBlock.Text = _diagramResult is null
            ? $"{_definition.TotalLayerCount}-layer topology"
            : $"Showing activations for pattern {_diagramResult.Index + 1}: {_diagramResult.Label}";
        ProjectStateTextBlock.Text =
            $"Project saves persist current weights, {_engine.CompletedCycles.ToString(CultureInfo.InvariantCulture)} completed cycles, " +
            $"learning steps {ParseLearningStepsFromControls().ToString(CultureInfo.InvariantCulture)}, and selected pattern {Math.Max(PatternSelectorComboBox.SelectedIndex, 0) + 1}.";

        UpdateActionAvailability();
    }

    private void SetPatternEditorText(string text)
    {
        if (PatternEditorTextBox is null)
        {
            return;
        }

        _isSyncingPatternText = true;
        PatternEditorTextBox.Text = text;
        _isSyncingPatternText = false;
    }

    private void PatternEditorTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isSyncingPatternText || _isSyncingPatternTable || !ArePatternEditorControlsReady())
        {
            return;
        }

        if (TryParsePatternDraft(PatternEditorTextBox.Text, out var parsed))
        {
            _patterns = NormalizePatternSetForDefinition(parsed);
            RenderPatternGraphicTable(_patterns);
            UpdatePatternSelector(Math.Max(PatternSelectorComboBox.SelectedIndex, 0));
            SetPatternEditorStatus($"Pattern text parsed: {_patterns.Examples.Count} rows");
        }
        else
        {
            SetPatternEditorStatus("Pattern text contains an invalid row. Graphic mode is showing the last valid pattern set.");
        }
    }

    private bool ArePatternEditorControlsReady()
    {
        return PatternEditorTextBox is not null &&
               PatternEditorStatusTextBlock is not null &&
               PatternTableHost is not null &&
               PatternSelectorComboBox is not null;
    }

    private void SetPatternEditorStatus(string text)
    {
        if (PatternEditorStatusTextBlock is null)
        {
            return;
        }

        PatternEditorStatusTextBlock.Text = text;
    }

    private bool TryParsePatternDraft(string? text, out PatternSet parsed)
    {
        try
        {
            parsed = NormalizePatternSetForDefinition(PatternSetParser.Parse(text ?? string.Empty, CurrentPatternDraftName()));
            return true;
        }
        catch
        {
            parsed = new PatternSet([]);
            return false;
        }
    }

    private string CurrentPatternDraftName()
    {
        return _graphPreviewDefinition?.Name
            ?? _definition?.Name
            ?? "pattern";
    }

    private PatternSet NormalizePatternSetForDefinition(PatternSet patternSet)
    {
        if (!AreDefinitionControlsReady())
        {
            return patternSet;
        }

        var definition = BuildDefinitionFromControls();
        var normalized = patternSet.Examples.Select(example =>
        {
            var inputs = NormalizeVector(example.Inputs, definition.InputUnits);
            var targets = example.Targets is null
                ? new double[definition.OutputUnits]
                : NormalizeVector(example.Targets, definition.OutputUnits);
            return example with
            {
                Inputs = inputs,
                Targets = targets
            };
        }).ToArray();

        return new PatternSet(normalized);
    }

    private static double[] NormalizeVector(double[] values, int length)
    {
        var normalized = new double[length];
        for (var index = 0; index < length; index++)
        {
            normalized[index] = index < values.Length ? values[index] : 0.0;
        }

        return normalized;
    }

    private void UpdateSliderValueLabels()
    {
        if (InputUnitsValueTextBlock is null ||
            HiddenUnitsValueTextBlock is null ||
            SecondHiddenUnitsValueTextBlock is null ||
            OutputUnitsValueTextBlock is null)
        {
            return;
        }

        InputUnitsValueTextBlock.Text = ReadSliderValue(InputUnitsSlider).ToString(CultureInfo.InvariantCulture);
        HiddenUnitsValueTextBlock.Text = ReadSliderValue(HiddenUnitsSlider).ToString(CultureInfo.InvariantCulture);
        SecondHiddenUnitsValueTextBlock.Text = ReadSliderValue(SecondHiddenUnitsSlider).ToString(CultureInfo.InvariantCulture);
        OutputUnitsValueTextBlock.Text = ReadSliderValue(OutputUnitsSlider).ToString(CultureInfo.InvariantCulture);
    }

    private void RefreshGraphPreview()
    {
        if (!AreDefinitionControlsReady())
        {
            return;
        }

        if (TryBuildPreviewDefinition(out var previewDefinition))
        {
            _graphPreviewDefinition = previewDefinition;
            NetworkGraphSummaryTextBlock.Text = $"Preview: {previewDefinition.TotalLayerCount}-layer topology";
            RenderNetworkGraph();
        }
    }

    private bool TryBuildPreviewDefinition(out NetworkDefinition definition)
    {
        if (!AreDefinitionControlsReady())
        {
            definition = null!;
            return false;
        }

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

    private void RenderPatternGraphicTable(PatternSet patternSet)
    {
        if (PatternTableHost is null)
        {
            return;
        }

        _isSyncingPatternTable = true;
        PatternTableHost.Children.Clear();

        var inputCount = AreDefinitionControlsReady() ? ReadSliderValue(InputUnitsSlider) : _definition?.InputUnits ?? 0;
        var outputCount = AreDefinitionControlsReady() ? ReadSliderValue(OutputUnitsSlider) : _definition?.OutputUnits ?? 0;

        PatternTableHost.Children.Add(BuildPatternTableHeader(inputCount, outputCount));

        for (var rowIndex = 0; rowIndex < patternSet.Examples.Count; rowIndex++)
        {
            PatternTableHost.Children.Add(BuildPatternTableRow(patternSet.Examples[rowIndex], rowIndex, inputCount, outputCount));
        }

        _isSyncingPatternTable = false;
    }

    private Grid BuildPatternTableHeader(int inputCount, int outputCount)
    {
        var grid = new Grid
        {
            ColumnSpacing = 6
        };
        grid.ColumnDefinitions = BuildPatternColumnDefinitions(inputCount, outputCount);

        AddPatternHeaderText(grid, "Label", 0);
        AddPatternHeaderText(grid, "Reset", 1);

        var column = 2;
        for (var index = 0; index < inputCount; index++, column++)
        {
            AddPatternHeaderText(grid, $"I{index + 1}", column);
        }

        var hasTargets = outputCount > 0;
        if (hasTargets)
        {
            AddPatternSectionSeparator(grid, column, true);
            column++;
        }

        for (var index = 0; index < outputCount; index++, column++)
        {
            AddPatternHeaderText(grid, $"O{index + 1}", column);
        }

        AddPatternHeaderText(grid, "Del", column);
        return grid;
    }

    private static void AddPatternHeaderText(Grid grid, string text, int column)
    {
        var header = new TextBlock
        {
            Text = text,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush.Parse("#6A6258"),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(header, column);
        grid.Children.Add(header);
    }

    private static void AddPatternSectionSeparator(Grid grid, int column, bool isHeader)
    {
        var separator = new Border
        {
            Width = isHeader ? 12 : 10,
            MinHeight = isHeader ? 24 : 34,
            Background = Brush.Parse(isHeader ? "#D0C7BC" : "#E2DCD4"),
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetColumn(separator, column);
        grid.Children.Add(separator);
    }

    private Grid BuildPatternTableRow(PatternExample example, int rowIndex, int inputCount, int outputCount)
    {
        var grid = new Grid
        {
            ColumnSpacing = 6
        };
        grid.ColumnDefinitions = BuildPatternColumnDefinitions(inputCount, outputCount);

        var labelBox = new TextBox
        {
            Text = example.Label,
            Tag = new PatternCellTag(rowIndex, PatternCellKind.Label, -1),
            Width = PatternLabelColumnWidth
        };
        labelBox.LostFocus += PatternTableTextBox_LostFocus;
        Grid.SetColumn(labelBox, 0);
        grid.Children.Add(labelBox);

        var resetCheckBox = new CheckBox
        {
            IsChecked = example.ResetsContextAfter,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Tag = new PatternCellTag(rowIndex, PatternCellKind.Reset, -1)
        };
        resetCheckBox.Click += PatternTableReset_Click;
        Grid.SetColumn(resetCheckBox, 1);
        grid.Children.Add(resetCheckBox);

        var column = 2;
        for (var index = 0; index < inputCount; index++, column++)
        {
            grid.Children.Add(BuildPatternBinaryCell(example.Inputs.ElementAtOrDefault(index), rowIndex, PatternCellKind.Input, index, column));
        }

        var hasTargets = outputCount > 0;
        if (hasTargets)
        {
            AddPatternSectionSeparator(grid, column, false);
            column++;
        }

        var targets = example.Targets ?? new double[outputCount];
        for (var index = 0; index < outputCount; index++, column++)
        {
            grid.Children.Add(BuildPatternBinaryCell(targets.ElementAtOrDefault(index), rowIndex, PatternCellKind.Target, index, column));
        }

        var deleteButton = new Button
        {
            Content = "X",
            Tag = rowIndex,
            Padding = new Thickness(6, 2)
        };
        deleteButton.Click += DeletePatternRow_Click;
        Grid.SetColumn(deleteButton, column);
        grid.Children.Add(deleteButton);

        return grid;
    }

    private Button BuildPatternBinaryCell(double value, int rowIndex, PatternCellKind kind, int vectorIndex, int column)
    {
        var button = new Button
        {
            Content = ToBinaryCellText(value),
            Width = PatternBinaryCellWidth,
            Height = 30,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brush.Parse(kind == PatternCellKind.Input ? "#E9F1FA" : "#F7EEE0"),
            BorderBrush = Brush.Parse(kind == PatternCellKind.Input ? "#8BAFD0" : "#C9A36B"),
            BorderThickness = new Thickness(1),
            Tag = new PatternCellTag(rowIndex, kind, vectorIndex)
        };
        button.Click += PatternToggleButton_Click;
        Grid.SetColumn(button, column);
        return button;
    }

    private static ColumnDefinitions BuildPatternColumnDefinitions(int inputCount, int outputCount)
    {
        var columns = new ColumnDefinitions
        {
            new ColumnDefinition(PatternLabelColumnWidth, GridUnitType.Pixel),
            new ColumnDefinition(PatternResetColumnWidth, GridUnitType.Pixel)
        };

        for (var index = 0; index < inputCount + outputCount; index++)
        {
            columns.Add(new ColumnDefinition(PatternBinaryCellWidth, GridUnitType.Pixel));
        }

        if (outputCount > 0)
        {
            columns.Insert(2 + inputCount, new ColumnDefinition(PatternSeparatorColumnWidth, GridUnitType.Pixel));
        }

        columns.Add(new ColumnDefinition(PatternDeleteColumnWidth, GridUnitType.Pixel));
        return columns;
    }

    private void AddPatternRow_Click(object? sender, RoutedEventArgs e)
    {
        var inputCount = AreDefinitionControlsReady() ? ReadSliderValue(InputUnitsSlider) : _definition?.InputUnits ?? 1;
        var outputCount = AreDefinitionControlsReady() ? ReadSliderValue(OutputUnitsSlider) : _definition?.OutputUnits ?? 1;
        var examples = _patterns.Examples.ToList();
        examples.Add(new PatternExample(
            $"pattern-{examples.Count + 1}",
            new double[inputCount],
            new double[outputCount],
            false));
        ApplyGraphicPatternSet(new PatternSet(examples));
    }

    private void DeletePatternRow_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int rowIndex })
        {
            return;
        }

        var examples = _patterns.Examples.ToList();
        if (rowIndex < 0 || rowIndex >= examples.Count)
        {
            return;
        }

        examples.RemoveAt(rowIndex);
        ApplyGraphicPatternSet(new PatternSet(examples));
    }

    private void PatternTableReset_Click(object? sender, RoutedEventArgs e)
    {
        CommitPatternTableFromGraphic();
    }

    private void PatternTableTextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        CommitPatternTableFromGraphic();
    }

    private void PatternToggleButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isSyncingPatternTable || sender is not Button button)
        {
            return;
        }

        button.Content = string.Equals(button.Content?.ToString(), "1", StringComparison.Ordinal) ? "0" : "1";
        CommitPatternTableFromGraphic();
    }

    private void CommitPatternTableFromGraphic()
    {
        if (_isSyncingPatternTable || !ArePatternEditorControlsReady())
        {
            return;
        }

        if (!TryReadPatternTable(out var patternSet))
        {
            SetPatternEditorStatus("Graphic mode contains an invalid numeric value.");
            return;
        }

        ApplyGraphicPatternSet(patternSet);
    }

    private void ApplyGraphicPatternSet(PatternSet patternSet)
    {
        _patterns = NormalizePatternSetForDefinition(patternSet);
        RenderPatternGraphicTable(_patterns);
        if (PatternSelectorComboBox is not null)
        {
            var selection = Math.Max(PatternSelectorComboBox.SelectedIndex, 0);
            UpdatePatternSelector(selection);
        }

        SetPatternEditorText(PatternSetWriter.Write(_patterns));
        SetPatternEditorStatus($"Graphic editor updated: {_patterns.Examples.Count} rows");
    }

    private bool TryReadPatternTable(out PatternSet patternSet)
    {
        var inputCount = AreDefinitionControlsReady() ? ReadSliderValue(InputUnitsSlider) : _definition?.InputUnits ?? 0;
        var outputCount = AreDefinitionControlsReady() ? ReadSliderValue(OutputUnitsSlider) : _definition?.OutputUnits ?? 0;
        var rowGrids = PatternTableHost.Children.OfType<Grid>().Skip(1).ToList();
        var examples = new List<PatternExample>(rowGrids.Count);

        foreach (var rowGrid in rowGrids)
        {
            var label = rowGrid.Children
                .OfType<TextBox>()
                .FirstOrDefault(child => child.Tag is PatternCellTag { Kind: PatternCellKind.Label })?.Text?.Trim();

            if (string.IsNullOrWhiteSpace(label))
            {
                label = $"pattern-{examples.Count + 1}";
            }

            var reset = rowGrid.Children
                .OfType<CheckBox>()
                .FirstOrDefault(child => child.Tag is PatternCellTag { Kind: PatternCellKind.Reset })?.IsChecked == true;

            var inputs = new double[inputCount];
            var targets = new double[outputCount];

            foreach (var button in rowGrid.Children.OfType<Button>())
            {
                if (button.Tag is not PatternCellTag tag)
                {
                    continue;
                }

                var value = string.Equals(button.Content?.ToString(), "1", StringComparison.Ordinal) ? 1.0 : 0.0;

                if (tag.Kind == PatternCellKind.Input && tag.Index >= 0 && tag.Index < inputs.Length)
                {
                    inputs[tag.Index] = value;
                }
                else if (tag.Kind == PatternCellKind.Target && tag.Index >= 0 && tag.Index < targets.Length)
                {
                    targets[tag.Index] = value;
                }
            }

            examples.Add(new PatternExample(label, inputs, targets, reset));
        }

        patternSet = new PatternSet(examples);
        return true;
    }

    private static string ToBinaryCellText(double value)
    {
        return value >= 0.5 ? "1" : "0";
    }

    private enum PatternCellKind
    {
        Label,
        Reset,
        Input,
        Target
    }

    private sealed record PatternCellTag(int Row, PatternCellKind Kind, int Index);

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

    private void RecentProjectsComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializingUi || _isUpdatingRecentProjects || _isBusy || RecentProjectsComboBox.SelectedItem is not RecentProjectOption option)
        {
            return;
        }

        try
        {
            if (!File.Exists(option.Path))
            {
                _recentProjectPaths.Remove(option.Path);
                SaveRecentProjectsState();
                PopulateRecentProjectOptions();
                AppendConsole($"Recent project not found: {option.Path}");
                SetStatus("Recent project no longer exists.");
                return;
            }

            LoadProjectState(SignalWeaveProjectSerializer.LoadFile(option.Path), option.Path, $"Loaded recent project from {option.Path}.");
        }
        catch (Exception ex)
        {
            AppendConsole($"Load recent project failed: {ex.Message}");
            SetStatus("Load recent project failed.");
        }
        finally
        {
            _isUpdatingRecentProjects = true;
            RecentProjectsComboBox.SelectedIndex = -1;
            _isUpdatingRecentProjects = false;
        }
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
            RegisterRecentProject(path!);
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
        if (_isInitializingUi)
        {
            return;
        }

        SyncNetworkKindControls();
        RefreshGraphPreview();
    }

    private void TopologySliderChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializingUi)
        {
            return;
        }

        UpdateTopologyControlState();
        _patterns = NormalizePatternSetForDefinition(_patterns);
        RenderPatternGraphicTable(_patterns);
        SetPatternEditorText(PatternSetWriter.Write(_patterns));
        RefreshGraphPreview();
    }

    private void TopologyOptionChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isInitializingUi)
        {
            return;
        }

        _patterns = NormalizePatternSetForDefinition(_patterns);
        RenderPatternGraphicTable(_patterns);
        SetPatternEditorText(PatternSetWriter.Write(_patterns));
        RefreshGraphPreview();
    }

    private void ClearConsole_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _consoleMarkdown.Clear();
        ConsoleContentHost.Children.Clear();
    }

    private async void CopyConsole_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null || _consoleMarkdown.Length == 0)
        {
            return;
        }

        await topLevel.Clipboard.SetTextAsync(_consoleMarkdown.ToString());
        SetStatus("Console copied to clipboard.");
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
            AppendConsole(BuildTrainingResultMarkdown(result));
            RenderErrorPlot(_trainingHistory);
            RenderNetworkGraph();
            UpdateWorkspaceSummary();
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
            AppendConsole(BuildSingleResultMarkdown(result));
            RenderNetworkGraph();
            UpdateWorkspaceSummary();
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
            AppendConsole(BuildRunResultMarkdown("## Test all", result));
            RenderNetworkGraph();
            UpdateWorkspaceSummary();
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
        if (!AreDefinitionControlsReady())
        {
            throw new InvalidOperationException("Network controls are not initialized yet.");
        }

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

    private bool AreDefinitionControlsReady()
    {
        return ProjectNameTextBox is not null &&
               InputUnitsSlider is not null &&
               HiddenUnitsSlider is not null &&
               SecondHiddenUnitsSlider is not null &&
               OutputUnitsSlider is not null &&
               InputBiasCheckBox is not null &&
               HiddenBiasCheckBox is not null &&
               SecondHiddenBiasCheckBox is not null &&
               LearningRateComboBox is not null &&
               MomentumComboBox is not null &&
               WeightRangeComboBox is not null &&
               ErrorThresholdTextBox is not null &&
               BatchUpdateCheckBox is not null &&
               CrossEntropyCheckBox is not null &&
               LearningStepsComboBox is not null;
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
        if (ConsoleContentHost is null)
        {
            return;
        }

        if (_consoleMarkdown.Length > 0)
        {
            _consoleMarkdown.AppendLine();
            _consoleMarkdown.AppendLine();
        }

        _consoleMarkdown.Append(message);

        foreach (var control in BuildMarkdownConsoleEntry(message))
        {
            ConsoleContentHost.Children.Add(control);
        }

        ScrollConsoleToEnd();
    }

    private void ScrollConsoleToEnd()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (ConsoleScrollViewer is not null)
            {
                ConsoleScrollViewer.Offset = new Vector(ConsoleScrollViewer.Offset.X, ConsoleScrollViewer.Extent.Height);
            }
        });
    }

    private IEnumerable<Control> BuildMarkdownConsoleEntry(string markdown)
    {
        var lines = (markdown ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var controls = new List<Control>();
        var fencedCodeLines = new List<string>();
        var inCodeFence = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine ?? string.Empty;
            var trimmed = line.Trim();

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                if (inCodeFence)
                {
                    controls.Add(BuildConsoleCodeBlock(string.Join(Environment.NewLine, fencedCodeLines)));
                    fencedCodeLines.Clear();
                    inCodeFence = false;
                }
                else
                {
                    inCodeFence = true;
                }

                continue;
            }

            if (inCodeFence)
            {
                fencedCodeLines.Add(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                controls.Add(new Border { Height = 6, Background = Brushes.Transparent });
                continue;
            }

            if (trimmed.StartsWith("### ", StringComparison.Ordinal))
            {
                controls.Add(BuildConsoleTextBlock(trimmed[4..], 16, FontWeight.SemiBold));
                continue;
            }

            if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                controls.Add(BuildConsoleTextBlock(trimmed[3..], 18, FontWeight.SemiBold));
                continue;
            }

            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                controls.Add(BuildConsoleTextBlock(trimmed[2..], 20, FontWeight.Bold));
                continue;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                controls.Add(BuildConsoleParagraph($"• {trimmed[2..]}"));
                continue;
            }

            var orderedPrefixLength = GetOrderedListPrefixLength(trimmed);
            if (orderedPrefixLength > 0)
            {
                controls.Add(BuildConsoleParagraph(trimmed));
                continue;
            }

            if (trimmed.All(character => character == '-' || character == '*') && trimmed.Length >= 3)
            {
                controls.Add(new Border
                {
                    Height = 1,
                    Margin = new Thickness(0, 4),
                    Background = Brush.Parse("#D7CCBD")
                });
                continue;
            }

            controls.Add(BuildConsoleParagraph(line));
        }

        if (fencedCodeLines.Count > 0)
        {
            controls.Add(BuildConsoleCodeBlock(string.Join(Environment.NewLine, fencedCodeLines)));
        }

        return controls;
    }

    private static int GetOrderedListPrefixLength(string text)
    {
        var digitCount = 0;
        while (digitCount < text.Length && char.IsDigit(text[digitCount]))
        {
            digitCount++;
        }

        return digitCount > 0 &&
               digitCount + 1 < text.Length &&
               text[digitCount] == '.' &&
               text[digitCount + 1] == ' '
            ? digitCount + 2
            : 0;
    }

    private static Control BuildConsoleTextBlock(string text, double fontSize, FontWeight fontWeight)
    {
        return new SelectableTextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = fontWeight,
            Foreground = Brush.Parse("#211D19"),
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static Control BuildConsoleParagraph(string text)
    {
        var block = new SelectableTextBlock
        {
            Foreground = Brush.Parse("#2D2924"),
            TextWrapping = TextWrapping.Wrap
        };

        if (block.Inlines is { } inlines)
        {
            AppendMarkdownInlines(inlines, text);
        }
        else
        {
            block.Text = text;
        }

        return block;
    }

    private static Control BuildConsoleCodeBlock(string text)
    {
        return new Border
        {
            Background = Brush.Parse("#F1E8DA"),
            BorderBrush = Brush.Parse("#D7CCBD"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8),
            Child = new SelectableTextBlock
            {
                Text = text,
                FontFamily = FontFamily.Parse("Consolas, Menlo, monospace"),
                Foreground = Brush.Parse("#3B3025"),
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    private static void AppendMarkdownInlines(InlineCollection inlines, string text)
    {
        var index = 0;
        while (index < text.Length)
        {
            if (TryConsumeInline(text, ref index, "**", content => new Run(content) { FontWeight = FontWeight.Bold }, inlines))
            {
                continue;
            }

            if (TryConsumeInline(text, ref index, "`", content =>
            {
                var run = new Run(content) { Background = Brush.Parse("#F1E8DA") };
                return run;
            }, inlines))
            {
                continue;
            }

            if (TryConsumeInline(text, ref index, "*", content => new Run(content) { FontStyle = FontStyle.Italic }, inlines))
            {
                continue;
            }

            var nextMarker = FindNextMarker(text, index);
            var length = nextMarker < 0 ? text.Length - index : nextMarker - index;
            inlines.Add(new Run(text.Substring(index, length)));
            index += length;
        }
    }

    private static bool TryConsumeInline(string text, ref int index, string marker, Func<string, Inline> inlineFactory, InlineCollection inlines)
    {
        if (!text.AsSpan(index).StartsWith(marker, StringComparison.Ordinal))
        {
            return false;
        }

        var start = index + marker.Length;
        var end = text.IndexOf(marker, start, StringComparison.Ordinal);
        if (end < 0)
        {
            return false;
        }

        inlines.Add(inlineFactory(text[start..end]));
        index = end + marker.Length;
        return true;
    }

    private static int FindNextMarker(string text, int start)
    {
        var candidates = new[]
        {
            text.IndexOf("**", start, StringComparison.Ordinal),
            text.IndexOf('*', start),
            text.IndexOf('`', start)
        };

        return candidates.Where(candidate => candidate >= 0).DefaultIfEmpty(-1).Min();
    }

    private string BuildTrainingResultMarkdown(TrainResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Training");
        builder.AppendLine();
        builder.AppendLine($"- Steps executed: `{result.History.Count.ToString(CultureInfo.InvariantCulture)}`");
        builder.AppendLine($"- Final displayed average error: `{FormatNumber(result.FinalRun.DisplayAverageError)}`");
        builder.AppendLine();
        builder.Append(BuildRunResultMarkdownBody(result.FinalRun));
        return builder.ToString();
    }

    private string BuildRunResultMarkdown(string heading, RunResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine(heading);
        builder.AppendLine();
        builder.Append(BuildRunResultMarkdownBody(result));
        return builder.ToString();
    }

    private string BuildRunResultMarkdownBody(RunResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"- Patterns evaluated: `{result.Results.Count.ToString(CultureInfo.InvariantCulture)}`");
        builder.AppendLine($"- Displayed average error: `{FormatNumber(result.DisplayAverageError)}`");
        builder.AppendLine();
        builder.AppendLine("```text");
        builder.AppendLine("Idx  Label               Outputs                  Targets                  Error");

        foreach (var item in result.Results)
        {
            var outputs = FormatVector(item.Outputs);
            var targets = item.Targets is null ? "-" : FormatVector(item.Targets);
            builder.AppendLine($"{(item.Index + 1).ToString(CultureInfo.InvariantCulture),-4} {TrimToWidth(item.Label, 18),-18} {TrimToWidth(outputs, 24),-24} {TrimToWidth(targets, 24),-24} {FormatNumber(item.Error)}");
        }

        builder.AppendLine("```");
        return builder.ToString();
    }

    private static string FormatVector(IEnumerable<double> values)
    {
        return string.Join(" ", values.Select(FormatNumber));
    }

    private static string TrimToWidth(string value, int width)
    {
        return value.Length <= width ? value : value[..Math.Max(0, width - 1)] + "…";
    }

    private static string EscapeMarkdown(string value)
    {
        return value.Replace("`", "\\`", StringComparison.Ordinal);
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

    private string BuildSingleResultMarkdown(TestResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Test one");
        builder.AppendLine();
        builder.AppendLine($"- Pattern: `{result.Index + 1}`");
        builder.AppendLine($"- Label: `{EscapeMarkdown(result.Label)}`");
        builder.AppendLine($"- Error: `{FormatNumber(result.Error)}`");
        builder.AppendLine();
        builder.AppendLine("```text");
        builder.AppendLine("Field    Values");
        builder.AppendLine($"Inputs   {FormatVector(result.Inputs)}");
        builder.AppendLine($"Outputs  {FormatVector(result.Outputs)}");
        if (result.Targets is not null)
        {
            builder.AppendLine($"Targets  {FormatVector(result.Targets)}");
        }

        if (result.HiddenActivations.Length > 0)
        {
            builder.AppendLine($"Hidden   {FormatVector(result.HiddenActivations)}");
        }

        builder.AppendLine("```");
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

    private static int ReadSliderValue(Slider? slider)
    {
        if (slider is null)
        {
            return 0;
        }

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
