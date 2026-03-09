using System;

namespace SignalWeave.Desktop.ViewModels;

public sealed class FeedbackDialogRequestEventArgs(string title, string message) : EventArgs
{
    public string Title { get; } = title;
    public string Message { get; } = message;
}
