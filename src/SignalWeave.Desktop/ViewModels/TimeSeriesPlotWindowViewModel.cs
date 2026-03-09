using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SignalWeave.Desktop.ViewModels;

public partial class TimeSeriesPlotWindowViewModel : ViewModelBase
{
    private const int MaxPlots = 10;
    private readonly IReadOnlyList<TimeSeriesPlotOption> _options;
    private readonly List<TimeSeriesPlotOption> _activeOptions = [];

    public TimeSeriesPlotWindowViewModel()
        : this(new TimeSeriesPlotSession(
            "Time Series Plot",
            new[]
            {
                new TimeSeriesPlotOption(
                    "Input1",
                    "Input1",
                    "Time series plot of input unit 1.",
                    new[] { 0.0, 1.0, 0.0 },
                    "#2F9C42"),
                new TimeSeriesPlotOption(
                    "Target1",
                    "Target1",
                    "Time series plot of target unit 1.",
                    new[] { 0.0, 1.0, 1.0 },
                    "#2C67C7"),
                new TimeSeriesPlotOption(
                    "Output1",
                    "Output1",
                    "Time series plot of output unit 1.",
                    new[] { 0.1, 0.8, 0.9 },
                    "#D6453D")
            },
            3))
    {
    }

    public TimeSeriesPlotWindowViewModel(TimeSeriesPlotSession session)
    {
        _options = session.Options;
        PatternCount = Math.Max(0, session.PatternCount);
        WindowTitle = session.Title;
        OutputOptions = new ReadOnlyCollection<string>(_options.Select(option => option.Label).ToArray());
        SelectedOutput = OutputOptions.FirstOrDefault() ?? string.Empty;
        RefreshPlotState();
    }

    public IReadOnlyList<string> OutputOptions { get; }
    public ObservableCollection<TimeSeriesPlotSeriesItem> ActivePlots { get; } = [];

    public int PatternCount { get; }

    [ObservableProperty]
    private string _windowTitle = "Time Series Plot";

    [ObservableProperty]
    private string _selectedOutput = string.Empty;

    [ObservableProperty]
    private string _summary = "Select a variable and click Add plot.";

    [ObservableProperty]
    private string _yAxisTopLabel = "1.000";

    [ObservableProperty]
    private string _yAxisMidLabel = "0.500";

    [ObservableProperty]
    private string _yAxisBottomLabel = "0.000";

    [ObservableProperty]
    private string _xAxisLeftLabel = "0";

    [ObservableProperty]
    private string _xAxisMidLabel = "0";

    [ObservableProperty]
    private string _xAxisRightLabel = "0";

    [ObservableProperty]
    private string _xAxisTitle = "Pattern order";

    [RelayCommand]
    private void AddPlot()
    {
        var option = _options.FirstOrDefault(item => item.Label == SelectedOutput) ?? _options.FirstOrDefault();
        if (option is null)
        {
            Summary = "No time series data available.";
            return;
        }

        if (_activeOptions.Count >= MaxPlots)
        {
            Summary = "Max plots exceeded.";
            return;
        }

        _activeOptions.Add(option);
        RefreshPlotState();
    }

    private void RefreshPlotState()
    {
        ActivePlots.Clear();

        if (_activeOptions.Count == 0)
        {
            Summary = "Select a variable and click Add plot.";
            YAxisTopLabel = "1.000";
            YAxisMidLabel = "0.500";
            YAxisBottomLabel = "0.000";
            var right = Math.Max(0, PatternCount - 1);
            XAxisLeftLabel = "0";
            XAxisMidLabel = (right / 2).ToString();
            XAxisRightLabel = right.ToString();
            return;
        }

        var allValues = _activeOptions.SelectMany(option => option.Values).ToArray();
        var minValue = allValues.Min();
        var maxValue = allValues.Max();
        if (Math.Abs(maxValue - minValue) < 0.000001)
        {
            maxValue += 1.0;
            minValue -= 1.0;
        }

        var rightLabel = Math.Max(0, _activeOptions.Max(option => option.Values.Count) - 1);
        XAxisLeftLabel = "0";
        XAxisMidLabel = (rightLabel / 2).ToString();
        XAxisRightLabel = rightLabel.ToString();
        YAxisTopLabel = maxValue.ToString("0.###");
        YAxisMidLabel = ((maxValue + minValue) / 2.0).ToString("0.###");
        YAxisBottomLabel = minValue.ToString("0.###");
        Summary = string.Join(", ", _activeOptions.Select(option => option.Label));

        for (var seriesIndex = 0; seriesIndex < _activeOptions.Count; seriesIndex++)
        {
            var option = _activeOptions[seriesIndex];
            var points = BuildSeriesPoints(option.Values, minValue, maxValue);
            var markers = BuildSeriesMarkers(option, minValue, maxValue);
            ActivePlots.Add(new TimeSeriesPlotSeriesItem(
                $"{option.Id}-{seriesIndex}",
                option.Label,
                points,
                option.Stroke,
                markers));
        }
    }

    private static string BuildSeriesPoints(IReadOnlyList<double> values, double minValue, double maxValue)
    {
        if (values.Count == 0)
        {
            return "20,210 320,210";
        }

        const double left = 20;
        const double top = 20;
        const double width = 300;
        const double height = 190;
        var maxX = Math.Max(1, values.Count - 1);
        var range = Math.Max(0.000001, maxValue - minValue);

        return string.Join(" ", values.Select((value, index) =>
        {
            var x = left + (index * width / maxX);
            var y = top + ((maxValue - value) * height / range);
            return $"{x:0.##},{y:0.##}";
        }));
    }

    private static IReadOnlyList<PlotMarkerItem> BuildSeriesMarkers(TimeSeriesPlotOption option, double minValue, double maxValue)
    {
        const double left = 20;
        const double top = 20;
        const double width = 300;
        const double height = 190;
        var maxX = Math.Max(1, option.Values.Count - 1);
        var range = Math.Max(0.000001, maxValue - minValue);

        return option.Values
            .Select((value, index) =>
            {
                var x = left + (index * width / maxX) - 3;
                var y = top + ((maxValue - value) * height / range) - 3;
                return new PlotMarkerItem(x, y, 6, 6, option.Stroke, $"{option.Label}[{index}]");
            })
            .ToArray();
    }
}

public sealed record TimeSeriesPlotSeriesItem(
    string Key,
    string Label,
    string Points,
    string Stroke,
    IReadOnlyList<PlotMarkerItem> Markers);
