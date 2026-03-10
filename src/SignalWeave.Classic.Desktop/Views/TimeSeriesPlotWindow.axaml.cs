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
