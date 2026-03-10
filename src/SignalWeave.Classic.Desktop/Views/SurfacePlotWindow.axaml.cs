using Avalonia.Controls;
using SignalWeave.Desktop.ViewModels;

namespace SignalWeave.Desktop.Views;

public partial class SurfacePlotWindow : Window
{
    public SurfacePlotWindow()
        : this(new SurfacePlotSnapshot(
            "3D plot",
            "Input0",
            "Input1",
            "Target1",
            "1.000",
            "0.500",
            "0.000",
            "0.000",
            "0.500",
            "1.000",
            new[]
            {
                new SurfacePlotCell(40, 170, 140, 100, "#C67B47", "0, 1 | Target1=0.6", "0.6"),
                new SurfacePlotCell(180, 170, 140, 100, "#D14D3F", "1, 1 | Target1=0.9", "0.9"),
                new SurfacePlotCell(40, 70, 140, 100, "#3A6EAA", "0, 0 | Target1=0.1", "0.1"),
                new SurfacePlotCell(180, 70, 140, 100, "#5E96B5", "1, 0 | Target1=0.4", "0.4")
            }))
    {
    }

    public SurfacePlotWindow(SurfacePlotSnapshot snapshot)
    {
        InitializeComponent();
        DataContext = new SurfacePlotWindowViewModel(snapshot);
    }

    private void Dismiss_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
