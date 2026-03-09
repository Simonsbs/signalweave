using Avalonia.Controls;
using SignalWeave.Desktop.ViewModels;

namespace SignalWeave.Desktop.Views;

public partial class WeightDisplayWindow : Window
{
    public WeightDisplayWindow()
        : this(new WeightDisplaySession(
            "Weights",
            () => new SignalWeave.Core.WeightSet(
                new double[,] { { -0.8, -0.7 }, { 0.9, 0.2 }, { -0.1, 0.6 } },
                new double[,] { { 0.8 }, { -0.4 }, { 0.1 } })))
    {
    }

    public WeightDisplayWindow(WeightDisplaySession session)
    {
        InitializeComponent();
        DataContext = new WeightDisplayWindowViewModel(session);
    }

    private void Dismiss_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
