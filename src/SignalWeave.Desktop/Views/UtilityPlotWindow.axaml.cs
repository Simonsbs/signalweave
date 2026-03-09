using Avalonia.Controls;
using SignalWeave.Desktop.ViewModels;

namespace SignalWeave.Desktop.Views;

public partial class UtilityPlotWindow : Window
{
    public UtilityPlotWindow()
        : this(new PlotWindowSnapshot(
            "Utility Plot",
            "No utility plot prepared.",
            "20,160 120,40 220,120",
            new[]
            {
                new PlotMarkerItem(20, 160, 8, 8, "#B24C3D", "p1"),
                new PlotMarkerItem(120, 40, 8, 8, "#B24C3D", "p2")
            }))
    {
    }

    public UtilityPlotWindow(PlotWindowSnapshot snapshot)
    {
        InitializeComponent();
        DataContext = new UtilityPlotWindowViewModel(snapshot);
    }

    private void Dismiss_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
