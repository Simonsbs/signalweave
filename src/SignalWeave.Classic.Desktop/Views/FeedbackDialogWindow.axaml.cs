using Avalonia.Controls;
using SignalWeave.Desktop.ViewModels;

namespace SignalWeave.Desktop.Views;

public partial class FeedbackDialogWindow : Window
{
    public FeedbackDialogWindow()
    {
        InitializeComponent();
    }

    public FeedbackDialogWindow(string title, string message)
    {
        InitializeComponent();
        var viewModel = new FeedbackDialogViewModel(title, message);
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(FeedbackDialogViewModel.CloseRequested) && viewModel.CloseRequested)
            {
                Close(true);
            }
        };

        DataContext = viewModel;
    }
}
