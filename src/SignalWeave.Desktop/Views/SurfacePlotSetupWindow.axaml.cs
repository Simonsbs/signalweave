using Avalonia.Controls;
using SignalWeave.Desktop.ViewModels;

namespace SignalWeave.Desktop.Views;

public partial class SurfacePlotSetupWindow : Window
{
    public SurfacePlotSetupWindow()
        : this(new SurfacePlotSetupSession(
            "Plot Setup",
            new[]
            {
                new SurfacePlotAxisOption("Input1", "Input1", 0),
                new SurfacePlotAxisOption("Input2", "Input2", 1)
            },
            new[]
            {
                new SurfacePlotZOption("Target1", "Target1", true, 0),
                new SurfacePlotZOption("Output1", "Output1", false, 0)
            },
            new[]
            {
                new SurfacePlotSample("p1", new[] { 0.0, 0.0 }, new[] { 0.0 }, new[] { 0.1 }),
                new SurfacePlotSample("p2", new[] { 0.0, 1.0 }, new[] { 1.0 }, new[] { 0.8 }),
                new SurfacePlotSample("p3", new[] { 1.0, 0.0 }, new[] { 1.0 }, new[] { 0.9 }),
                new SurfacePlotSample("p4", new[] { 1.0, 1.0 }, new[] { 0.0 }, new[] { 0.2 })
            }))
    {
    }

    public SurfacePlotSetupWindow(SurfacePlotSetupSession session)
    {
        InitializeComponent();
        DataContext = new SurfacePlotSetupWindowViewModel(session);
    }

    private SurfacePlotSetupWindowViewModel ViewModel => (SurfacePlotSetupWindowViewModel)DataContext!;

    private void ShowPlot_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.UpdateSummary();
        var window = new SurfacePlotWindow(ViewModel.BuildPlotSnapshot());
        window.Show();
    }

    private void Dismiss_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
