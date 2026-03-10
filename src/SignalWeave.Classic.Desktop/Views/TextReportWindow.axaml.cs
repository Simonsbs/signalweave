using Avalonia.Controls;
using SignalWeave.Desktop.ViewModels;

namespace SignalWeave.Desktop.Views;

public partial class TextReportWindow : Window
{
    public TextReportWindow()
    {
        InitializeComponent();
    }

    public TextReportWindow(TextReportSnapshot snapshot)
        : this()
    {
        DataContext = new TextReportWindowViewModel(snapshot);
    }

    private void Dismiss_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
