using System;
using System.Collections.Specialized;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using SignalWeave.Core;
using SignalWeave.Desktop.ViewModels;

namespace SignalWeave.Desktop.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _attachedViewModel;
    private MessageWindow? _messageWindow;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += HandleDataContextChanged;
        AttachViewModel(DataContext as MainWindowViewModel);
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private void HandleDataContextChanged(object? sender, EventArgs e)
    {
        AttachViewModel(DataContext as MainWindowViewModel);
    }

    private void AttachViewModel(MainWindowViewModel? viewModel)
    {
        if (_attachedViewModel is not null)
        {
            _attachedViewModel.FeedbackDialogRequested -= HandleFeedbackDialogRequested;
            _attachedViewModel.DiagramNodes.CollectionChanged -= HandleDiagramCollectionChanged;
            _attachedViewModel.DiagramEdges.CollectionChanged -= HandleDiagramCollectionChanged;
        }

        _attachedViewModel = viewModel;

        if (_attachedViewModel is not null)
        {
            _attachedViewModel.FeedbackDialogRequested += HandleFeedbackDialogRequested;
            _attachedViewModel.DiagramNodes.CollectionChanged += HandleDiagramCollectionChanged;
            _attachedViewModel.DiagramEdges.CollectionChanged += HandleDiagramCollectionChanged;
        }

        RenderDiagram();
    }

    private async void HandleFeedbackDialogRequested(object? sender, FeedbackDialogRequestEventArgs e)
    {
        var window = new FeedbackDialogWindow(e.Title, e.Message);
        await window.ShowDialog(this);
    }

    private void HandleDiagramCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RenderDiagram();
    }

    private void RenderDiagram()
    {
        if (NetworkDiagramCanvas is null)
        {
            return;
        }

        NetworkDiagramCanvas.Children.Clear();

        if (_attachedViewModel is null)
        {
            return;
        }

        foreach (var edge in _attachedViewModel.DiagramEdges)
        {
            NetworkDiagramCanvas.Children.Add(new Line
            {
                StartPoint = Point.Parse(edge.StartPoint),
                EndPoint = Point.Parse(edge.EndPoint),
                Stroke = Brush.Parse(edge.Stroke),
                StrokeThickness = edge.Thickness
            });
        }

        foreach (var node in _attachedViewModel.DiagramNodes)
        {
            var border = new Border
            {
                Width = node.Width,
                Height = node.Height,
                Background = Brush.Parse(node.Fill),
                BorderBrush = Brush.Parse(node.Stroke),
                BorderThickness = new Thickness(1.2),
                Child = new TextBlock
                {
                    Text = node.Label,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brush.Parse("#4A4A4A")
                }
            };

            Canvas.SetLeft(border, node.X);
            Canvas.SetTop(border, node.Y);
            NetworkDiagramCanvas.Children.Add(border);
        }
    }

    private async void ConfigureNetwork_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var window = new NetworkConfigWindow(ViewModel.GetLoadedDefinition());
            window.DefinitionApplied += definition => ViewModel.ApplyConfiguredNetwork(definition);
            var applied = await window.ShowDialog<bool>(this);
            if (applied && window.ResultDefinition is not null)
            {
                ViewModel.ApplyConfiguredNetwork(window.ResultDefinition);
            }
        }
        catch (Exception exception)
        {
            ViewModel.ConsoleText = exception.Message;
        }
    }

    private async void LoadNetwork_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunWithConsoleAsync(async () =>
        {
            var file = await PickOpenFileAsync(
                "Load Network",
                new FilePickerFileType("SignalWeave network") { Patterns = ["*.swcfg", "*.txt"] },
                new FilePickerFileType("All files") { Patterns = ["*.*"] });

            if (file is null)
            {
                return;
            }

            var text = await ReadAllTextAsync(file);
            ViewModel.LoadNetworkText(text, System.IO.Path.GetFileNameWithoutExtension(file.Name));
        });
    }

    private async void SaveNetwork_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunWithConsoleAsync(async () =>
        {
            var definition = ViewModel.GetLoadedDefinition();
            var text = BasicPropNetworkConfigWriter.Write(definition);
            var file = await PickSaveFileAsync(
                "Save Network",
                ViewModel.GetSuggestedNetworkFileName(),
                ".swcfg",
                new FilePickerFileType("SignalWeave network") { Patterns = ["*.swcfg"] },
                new FilePickerFileType("Text files") { Patterns = ["*.txt"] });

            if (file is null)
            {
                return;
            }

            await WriteAllTextAsync(file, text);
        });
    }

    private async void LoadPatterns_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunWithConsoleAsync(async () =>
        {
            var file = await PickOpenFileAsync(
                "Load Patterns",
                new FilePickerFileType("Pattern files") { Patterns = ["*.pat", "*.txt", "*.csv"] },
                new FilePickerFileType("All files") { Patterns = ["*.*"] });

            if (file is null)
            {
                return;
            }

            var text = await ReadAllTextAsync(file);
            ViewModel.LoadPatternText(text, System.IO.Path.GetFileNameWithoutExtension(file.Name));
        });
    }

    private async void LoadFeedForwardWeights_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await LoadWeightsAsync(forSrn: false);
    }

    private async void LoadSrnWeights_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await LoadWeightsAsync(forSrn: true);
    }

    private async Task LoadWeightsAsync(bool forSrn)
    {
        await RunWithConsoleAsync(async () =>
        {
            if (!ViewModel.CanLoadWeightsFromMenu(forSrn))
            {
                return;
            }

            var file = await PickOpenFileAsync(
                "Load Weights",
                new FilePickerFileType("SignalWeave weights") { Patterns = ["*.weights.json", "*.json"] },
                new FilePickerFileType("All files") { Patterns = ["*.*"] });

            if (file is null)
            {
                return;
            }

            var path = file.TryGetLocalPath()
                ?? throw new InvalidOperationException("This platform did not provide a local file path for the selected weight file.");
            var weights = WeightSetSerializer.LoadFile(path);
            ViewModel.LoadWeights(weights);
        });
    }

    private async void SaveWeights_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunWithConsoleAsync(async () =>
        {
            var definition = ViewModel.GetLoadedDefinition();
            var weights = ViewModel.GetCurrentWeights();
            var file = await PickSaveFileAsync(
                "Save Weights",
                ViewModel.GetSuggestedWeightFileName(),
                ".json",
                new FilePickerFileType("SignalWeave weights") { Patterns = ["*.weights.json"] },
                new FilePickerFileType("JSON files") { Patterns = ["*.json"] });

            if (file is null)
            {
                return;
            }

            var path = file.TryGetLocalPath()
                ?? throw new InvalidOperationException("This platform did not provide a local file path for the selected weight file.");
            WeightSetSerializer.SaveFile(path, definition, weights);
        });
    }

    private async void ShowWeightsWindow_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunWithConsoleAsync(async () =>
        {
            var window = new WeightDisplayWindow(ViewModel.CreateWeightDisplaySession());
            await window.ShowDialog(this);
        });
    }

    private async void ShowPatternsWindow_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunWithConsoleAsync(async () =>
        {
            var window = new PatternPlotWindow(ViewModel.CreatePatternPlotSession());
            await window.ShowDialog(this);
        });
    }

    private async void ShowTimeSeriesPlotWindow_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunWithConsoleAsync(async () =>
        {
            var window = new TimeSeriesPlotWindow(ViewModel.CreateTimeSeriesPlotSession());
            await window.ShowDialog(this);
        });
    }

    private async void Show3DPlotWindow_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunWithConsoleAsync(async () =>
        {
            var window = new SurfacePlotSetupWindow(ViewModel.CreateSurfacePlotSetupSession());
            await window.ShowDialog(this);
        });
    }

    private async void ShowMessageWindow_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunWithConsoleAsync(() =>
        {
            if (_messageWindow is null)
            {
                _messageWindow = new MessageWindow(ViewModel.MessageWindow);
                _messageWindow.Closed += (_, _) => _messageWindow = null;
                _messageWindow.Show(this);
            }
            else
            {
                _messageWindow.Activate();
            }

            return Task.CompletedTask;
        });
    }

    private async void OpenRepository_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunWithConsoleAsync(() =>
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/Simonsbs/signalweave",
                UseShellExecute = true
            });

            return Task.CompletedTask;
        });
    }

    private async Task RunWithConsoleAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            ViewModel.ConsoleText = exception.Message;
        }
    }

    private async Task<IStorageFile?> PickOpenFileAsync(string title, params FilePickerFileType[] fileTypes)
    {
        if (!StorageProvider.CanOpen)
        {
            throw new InvalidOperationException("File open dialogs are not available on this platform.");
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        });

        return files.Count > 0 ? files[0] : null;
    }

    private async Task<IStorageFile?> PickSaveFileAsync(string title, string suggestedFileName, string defaultExtension, params FilePickerFileType[] fileTypes)
    {
        if (!StorageProvider.CanSave)
        {
            throw new InvalidOperationException("File save dialogs are not available on this platform.");
        }

        return await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = defaultExtension,
            ShowOverwritePrompt = true,
            FileTypeChoices = fileTypes
        });
    }

    private static async Task<string> ReadAllTextAsync(IStorageFile file)
    {
        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private static async Task WriteAllTextAsync(IStorageFile file, string text)
    {
        await using var stream = await file.OpenWriteAsync();
        stream.SetLength(0);
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync(text);
        await writer.FlushAsync();
    }
}
