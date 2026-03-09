using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SignalWeave.Desktop.ViewModels;

public partial class PatternPlotWindowViewModel : ViewModelBase
{
    private readonly IReadOnlyList<PatternPlotEntry> _patterns;

    public PatternPlotWindowViewModel()
        : this(new PatternPlotSession(
            "Show Patterns and Outputs",
            new[]
            {
                new PatternPlotEntry(
                    0,
                    "[0]: 0,1    >>>1",
                    "pattern-1",
                    new[] { new PatternChartBar("Outputs", "unit 1", 52, 18, 22, 74, "#D6453D", 1, "0.900") },
                    new[] { new PatternChartBar("Targets", "unit 1", 52, 10, 22, 82, "#2C67C7", 1, "1.000") },
                    new[] { new PatternChartBar("Inputs", "unit 1", 52, 51, 22, 41, "#2F9C42", 1, "0.000") })
            }))
    {
    }

    public PatternPlotWindowViewModel(PatternPlotSession session)
    {
        _patterns = session.Patterns;
        WindowTitle = session.Title;
        PatternOptions = new ReadOnlyCollection<string>(_patterns.Select(pattern => pattern.SelectorLabel).ToArray());
        SelectedPattern = PatternOptions.FirstOrDefault() ?? string.Empty;
        ApplySelectedPattern();
    }

    public IReadOnlyList<string> PatternOptions { get; }
    public ObservableCollection<PatternChartBar> OutputBars { get; } = [];
    public ObservableCollection<PatternChartBar> TargetBars { get; } = [];
    public ObservableCollection<PatternChartBar> InputBars { get; } = [];

    [ObservableProperty]
    private string _windowTitle = "Show Patterns and Outputs";

    [ObservableProperty]
    private string _selectedPattern = string.Empty;

    [ObservableProperty]
    private string _summary = string.Empty;

    partial void OnSelectedPatternChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            ApplySelectedPattern();
        }
    }

    private void ApplySelectedPattern()
    {
        if (_patterns.Count == 0)
        {
            Summary = "No patterns are loaded.";
            OutputBars.Clear();
            TargetBars.Clear();
            InputBars.Clear();
            return;
        }

        var pattern = _patterns.FirstOrDefault(item => item.SelectorLabel == SelectedPattern) ?? _patterns[0];
        Summary = pattern.SelectorLabel;
        ResetBars(OutputBars, pattern.OutputBars);
        ResetBars(TargetBars, pattern.TargetBars);
        ResetBars(InputBars, pattern.InputBars);
    }

    private static void ResetBars(ObservableCollection<PatternChartBar> target, IReadOnlyList<PatternChartBar> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }
}
