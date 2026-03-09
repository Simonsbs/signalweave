using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SignalWeave.Desktop.ViewModels;

public partial class TimeSeriesPlotWindowViewModel : ViewModelBase
{
    private readonly IReadOnlyList<TimeSeriesPlotOption> _options;

    public TimeSeriesPlotWindowViewModel()
        : this(new TimeSeriesPlotSession(
            "Time Series Plot",
            new[]
            {
                new TimeSeriesPlotOption(
                    "Output 1",
                    "Output 1",
                    "Time series plot of output unit 1 across pattern order.",
                    "20,210 170,90 320,40",
                    new[]
                    {
                        new PlotMarkerItem(20, 210, 6, 6, "#4E7396", "pattern-1"),
                        new PlotMarkerItem(170, 90, 6, 6, "#4E7396", "pattern-2"),
                        new PlotMarkerItem(320, 40, 6, 6, "#4E7396", "pattern-3")
                    })
            },
            "1.000",
            "0.500",
            "0.000",
            "1",
            "2",
            "3",
            "Pattern order"))
    {
    }

    public TimeSeriesPlotWindowViewModel(TimeSeriesPlotSession session)
    {
        _options = session.Options;
        WindowTitle = session.Title;
        OutputOptions = new ReadOnlyCollection<string>(_options.Select(option => option.Label).ToArray());
        SelectedOutput = OutputOptions.FirstOrDefault() ?? "Output 1";
        YAxisTopLabel = session.YAxisTopLabel;
        YAxisMidLabel = session.YAxisMidLabel;
        YAxisBottomLabel = session.YAxisBottomLabel;
        XAxisLeftLabel = session.XAxisLeftLabel;
        XAxisMidLabel = session.XAxisMidLabel;
        XAxisRightLabel = session.XAxisRightLabel;
        XAxisTitle = session.XAxisTitle;
        ApplySelectedPlot();
    }

    public IReadOnlyList<string> OutputOptions { get; }
    public ObservableCollection<PlotMarkerItem> Markers { get; } = [];

    [ObservableProperty]
    private string _windowTitle = "Time Series Plot";

    [ObservableProperty]
    private string _selectedOutput = "Output 1";

    [ObservableProperty]
    private string _summary = "No time series data available.";

    [ObservableProperty]
    private string _plotPoints = "20,210 320,210";

    [ObservableProperty]
    private string _yAxisTopLabel = "1.000";

    [ObservableProperty]
    private string _yAxisMidLabel = "0.500";

    [ObservableProperty]
    private string _yAxisBottomLabel = "0.000";

    [ObservableProperty]
    private string _xAxisLeftLabel = "1";

    [ObservableProperty]
    private string _xAxisMidLabel = "2";

    [ObservableProperty]
    private string _xAxisRightLabel = "3";

    [ObservableProperty]
    private string _xAxisTitle = "Pattern order";

    [RelayCommand]
    private void AddPlot()
    {
        ApplySelectedPlot();
    }

    private void ApplySelectedPlot()
    {
        var option = _options.FirstOrDefault(item => item.Label == SelectedOutput) ?? _options[0];
        Summary = option.Summary;
        PlotPoints = option.Points;
        Markers.Clear();
        foreach (var marker in option.Markers)
        {
            Markers.Add(marker);
        }
    }
}
