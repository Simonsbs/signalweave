using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SignalWeave.Desktop.ViewModels;

public partial class FeedbackDialogViewModel : ViewModelBase
{
    public FeedbackDialogViewModel()
    {
        WindowTitle = "Invalid value";
        Message = "An invalid value was given.";
    }

    public FeedbackDialogViewModel(string title, string message)
    {
        WindowTitle = title;
        Message = message;
    }

    [ObservableProperty]
    private string _windowTitle = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _closeRequested;

    [RelayCommand]
    private void Close()
    {
        CloseRequested = true;
    }
}
