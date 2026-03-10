using Avalonia.Controls;
using SignalWeave.Desktop.ViewModels;

namespace SignalWeave.Desktop.Views;

public partial class MessageWindow : Window
{
    public MessageWindow()
    {
        InitializeComponent();
    }

    public MessageWindow(MessageWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
