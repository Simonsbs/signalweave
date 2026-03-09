using Avalonia.Controls;
using SignalWeave.Desktop.ViewModels;

namespace SignalWeave.Desktop.Views;

public partial class TimeSeriesPlotWindow : Window
{
    public TimeSeriesPlotWindow()
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

    public TimeSeriesPlotWindow(TimeSeriesPlotSession session)
    {
        InitializeComponent();
        DataContext = new TimeSeriesPlotWindowViewModel(session);
    }

    private void Dismiss_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
