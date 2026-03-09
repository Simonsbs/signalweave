using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SignalWeave.Desktop.ViewModels;

public partial class MessageWindowViewModel : ViewModelBase
{
    private readonly StringBuilder _buffer = new();

    [ObservableProperty]
    private string _windowTitle = "Messages";

    [ObservableProperty]
    private string _messageLogText = string.Empty;

    public void Write(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        _buffer.Append(text);
        MessageLogText = _buffer.ToString();
    }

    public void WriteLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (_buffer.Length > 0)
        {
            _buffer.AppendLine();
        }

        _buffer.Append(text);
        MessageLogText = _buffer.ToString();
    }

    [RelayCommand]
    private void Clear()
    {
        _buffer.Clear();
        MessageLogText = string.Empty;
    }
}
