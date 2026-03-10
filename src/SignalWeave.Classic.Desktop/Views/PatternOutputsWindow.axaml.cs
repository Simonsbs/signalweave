using Avalonia.Controls;
using SignalWeave.Desktop.ViewModels;

namespace SignalWeave.Desktop.Views;

public partial class PatternOutputsWindow : Window
{
    public PatternOutputsWindow()
        : this(new PatternOutputsSnapshot(
            "Patterns and Outputs",
            "Average error: 0.000000",
            new[]
            {
                new PatternOutputRow(1, "pattern-1", "0.000 0.000", "0.000", "0.012", "0.000144")
            }))
    {
    }

    public PatternOutputsWindow(PatternOutputsSnapshot snapshot)
    {
        InitializeComponent();
        DataContext = new PatternOutputsWindowViewModel(snapshot);
    }

    private void Dismiss_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
