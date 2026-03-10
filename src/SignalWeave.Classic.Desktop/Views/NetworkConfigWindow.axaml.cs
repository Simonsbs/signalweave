using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SignalWeave.Core;
using SignalWeave.Desktop.ViewModels;

namespace SignalWeave.Desktop.Views;

public partial class NetworkConfigWindow : Window
{
    public NetworkConfigWindow()
        : this(new NetworkDefinition
        {
            Name = "Untitled",
            InputUnits = 2,
            HiddenUnits = 3,
            OutputUnits = 1
        })
    {
    }

    public NetworkConfigWindow(NetworkDefinition definition)
    {
        InitializeComponent();
        DataContext = new NetworkConfigDialogViewModel(definition);
    }

    public NetworkDefinition? ResultDefinition { get; private set; }
    public event Action<NetworkDefinition>? DefinitionApplied;

    private NetworkConfigDialogViewModel ViewModel => (NetworkConfigDialogViewModel)DataContext!;

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        TryApplyAndClose();
    }

    private void Apply_Click(object? sender, RoutedEventArgs e)
    {
        TryApplyWithoutClose();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void TryApplyAndClose()
    {
        try
        {
            ResultDefinition = ViewModel.BuildDefinition();
            ViewModel.StatusText = string.Empty;
            Close(true);
        }
        catch (Exception exception)
        {
            ViewModel.StatusText = exception.Message;
        }
    }

    private void TryApplyWithoutClose()
    {
        try
        {
            ResultDefinition = ViewModel.BuildDefinition();
            ViewModel.StatusText = string.Empty;
            DefinitionApplied?.Invoke(ResultDefinition);
        }
        catch (Exception exception)
        {
            ViewModel.StatusText = exception.Message;
        }
    }
}
