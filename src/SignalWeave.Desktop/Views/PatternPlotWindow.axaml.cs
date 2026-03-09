using Avalonia.Controls;
using SignalWeave.Desktop.ViewModels;

namespace SignalWeave.Desktop.Views;

public partial class PatternPlotWindow : Window
{
    public PatternPlotWindow()
        : this(new PatternPlotSession(
            "Pattern Plot",
            new[]
            {
                new PatternPlotEntry(
                    0,
                    "[0]: 0.000 1.000 => 1.000",
                    "pattern-1",
                    new[] { new PatternChartBar("Outputs", "unit 1", 52, 18, 22, 74, "#D6453D", 1, "0.900") },
                    new[] { new PatternChartBar("Targets", "unit 1", 52, 10, 22, 82, "#2C67C7", 1, "1.000") },
                    new[] { new PatternChartBar("Inputs", "unit 1", 52, 51, 22, 41, "#2F9C42", 1, "0.000") })
            }))
    {
    }

    public PatternPlotWindow(PatternPlotSession session)
    {
        InitializeComponent();
        DataContext = new PatternPlotWindowViewModel(session);
    }

    private void Dismiss_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
