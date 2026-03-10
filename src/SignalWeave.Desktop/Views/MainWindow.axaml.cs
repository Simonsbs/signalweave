using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.ComponentModel;
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
        if (NetworkDiagramCanvas is not null)
        {
            NetworkDiagramCanvas.SizeChanged += HandleNetworkDiagramCanvasSizeChanged;
        }
        if (ErrorPlotCanvas is not null)
        {
            ErrorPlotCanvas.SizeChanged += HandleErrorPlotCanvasSizeChanged;
        }

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
            _attachedViewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        }

        _attachedViewModel = viewModel;

        if (_attachedViewModel is not null)
        {
            _attachedViewModel.FeedbackDialogRequested += HandleFeedbackDialogRequested;
            _attachedViewModel.DiagramNodes.CollectionChanged += HandleDiagramCollectionChanged;
            _attachedViewModel.DiagramEdges.CollectionChanged += HandleDiagramCollectionChanged;
            _attachedViewModel.PropertyChanged += HandleViewModelPropertyChanged;
            SyncDiagramViewport();
        }

        RenderDiagram();
        RenderErrorPlot();
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

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.ErrorProgressPoints) or
            nameof(MainWindowViewModel.ErrorPlotTopLabel) or
            nameof(MainWindowViewModel.ErrorPlotBottomRightLabel))
        {
            RenderErrorPlot();
        }
    }

    private void HandleNetworkDiagramCanvasSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        SyncDiagramViewport();
    }

    private void SyncDiagramViewport()
    {
        if (_attachedViewModel is null || NetworkDiagramCanvas is null)
        {
            return;
        }

        _attachedViewModel.UpdateDiagramViewport(
            NetworkDiagramCanvas.Bounds.Width,
            NetworkDiagramCanvas.Bounds.Height);
    }

    private void HandleErrorPlotCanvasSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        RenderErrorPlot();
    }

    private void RenderErrorPlot()
    {
        if (ErrorPlotCanvas is null)
        {
            return;
        }

        ErrorPlotCanvas.Children.Clear();

        if (_attachedViewModel is null)
        {
            return;
        }

        var width = Math.Max(220, ErrorPlotCanvas.Bounds.Width);
        var height = Math.Max(170, ErrorPlotCanvas.Bounds.Height);
        var left = 28.0;
        var top = 12.0;
        var right = width - 12.0;
        var bottom = height - 28.0;
        var plotWidth = Math.Max(20, right - left);
        var plotHeight = Math.Max(20, bottom - top);

        ErrorPlotCanvas.Children.Add(new TextBlock
        {
            Text = _attachedViewModel.ErrorPlotTopLabel,
            Foreground = Brush.Parse("#5E5E5E")
        });

        ErrorPlotCanvas.Children.Add(new Line
        {
            StartPoint = new Point(left, top),
            EndPoint = new Point(left, bottom),
            Stroke = Brush.Parse("#5A5A5A"),
            StrokeThickness = 1.5
        });

        ErrorPlotCanvas.Children.Add(new Line
        {
            StartPoint = new Point(left, bottom),
            EndPoint = new Point(right, bottom),
            Stroke = Brush.Parse("#5A5A5A"),
            StrokeThickness = 1.5
        });

        for (var tick = 1; tick <= 4; tick++)
        {
            var x = left + ((plotWidth * tick) / 5.0);
            ErrorPlotCanvas.Children.Add(new Line
            {
                StartPoint = new Point(x, bottom - 4),
                EndPoint = new Point(x, bottom + 4),
                Stroke = Brush.Parse("#5A5A5A"),
                StrokeThickness = 1.2
            });
        }

        var points = ParseErrorPlotPoints(_attachedViewModel.ErrorProgressPoints, left, top, plotWidth, plotHeight);
        if (points.Count >= 2)
        {
            ErrorPlotCanvas.Children.Add(new Polyline
            {
                Points = points,
                Stroke = Brush.Parse("#4E7396"),
                StrokeThickness = 2
            });
        }

        var zeroLabel = new TextBlock
        {
            Text = "0",
            Foreground = Brush.Parse("#5E5E5E")
        };
        Canvas.SetLeft(zeroLabel, 4);
        Canvas.SetTop(zeroLabel, bottom - 2);
        ErrorPlotCanvas.Children.Add(zeroLabel);

        var maxLabel = new TextBlock
        {
            Text = _attachedViewModel.ErrorPlotBottomRightLabel,
            Foreground = Brush.Parse("#5E5E5E"),
            Width = 52,
            TextAlignment = TextAlignment.Right
        };
        Canvas.SetLeft(maxLabel, right - 48);
        Canvas.SetTop(maxLabel, bottom - 2);
        ErrorPlotCanvas.Children.Add(maxLabel);
    }

    private static Avalonia.Collections.AvaloniaList<Point> ParseErrorPlotPoints(string pointsText, double left, double top, double width, double height)
    {
        var points = new Avalonia.Collections.AvaloniaList<Point>();
        var sourceLeft = 24.0;
        var sourceTop = 8.0;
        var sourceWidth = 226.0;
        var sourceHeight = 108.0;

        foreach (var token in pointsText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var commaIndex = token.IndexOf(',');
            if (commaIndex <= 0 || commaIndex >= token.Length - 1)
            {
                continue;
            }

            if (!double.TryParse(token[..commaIndex], out var x) ||
                !double.TryParse(token[(commaIndex + 1)..], out var y))
            {
                continue;
            }

            var normalizedX = (x - sourceLeft) / sourceWidth;
            var normalizedY = (y - sourceTop) / sourceHeight;
            points.Add(new Point(
                left + (normalizedX * width),
                top + (normalizedY * height)));
        }

        return points;
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
                BorderThickness = new Thickness(1)
            };

            if (!string.IsNullOrWhiteSpace(node.Label))
            {
                border.Child = new TextBlock
                {
                    Text = node.Label,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brush.Parse("#4A4A4A")
                };
            }

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

    private async void LoadProject_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunWithConsoleAsync(async () =>
        {
            var file = await PickOpenFileAsync(
                "Load Project",
                new FilePickerFileType("SignalWeave project") { Patterns = ["*.swproj.json"] },
                new FilePickerFileType("JSON files") { Patterns = ["*.json"] },
                new FilePickerFileType("All files") { Patterns = ["*.*"] });

            if (file is null)
            {
                return;
            }

            var path = file.TryGetLocalPath()
                ?? throw new InvalidOperationException("This platform did not provide a local file path for the selected project file.");
            var project = SignalWeaveProjectSerializer.LoadFile(path);
            ViewModel.LoadProject(project);
        });
    }

    private async void SaveProject_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunWithConsoleAsync(async () =>
        {
            var file = await PickSaveFileAsync(
                "Save Project",
                ViewModel.GetSuggestedProjectFileName(),
                ".json",
                new FilePickerFileType("SignalWeave project") { Patterns = ["*.swproj.json"] },
                new FilePickerFileType("JSON files") { Patterns = ["*.json"] });

            if (file is null)
            {
                return;
            }

            var path = file.TryGetLocalPath()
                ?? throw new InvalidOperationException("This platform did not provide a local file path for the selected project file.");
            SignalWeaveProjectSerializer.SaveFile(
                path,
                ViewModel.GetLoadedDefinition(),
                ViewModel.GetLoadedPatternSet(),
                ViewModel.GetCurrentWeights());
        });
    }

    private async void LoadCheckpoint_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunWithConsoleAsync(async () =>
        {
            var file = await PickOpenFileAsync(
                "Load Checkpoint",
                new FilePickerFileType("SignalWeave checkpoint") { Patterns = ["*.swcheckpoint.json"] },
                new FilePickerFileType("JSON files") { Patterns = ["*.json"] },
                new FilePickerFileType("All files") { Patterns = ["*.*"] });

            if (file is null)
            {
                return;
            }

            var path = file.TryGetLocalPath()
                ?? throw new InvalidOperationException("This platform did not provide a local file path for the selected checkpoint file.");
            var checkpoint = SignalWeaveCheckpointSerializer.LoadFile(path);
            ViewModel.LoadCheckpoint(checkpoint);
        });
    }

    private async void SaveCheckpoint_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunWithConsoleAsync(async () =>
        {
            var file = await PickSaveFileAsync(
                "Save Checkpoint",
                ViewModel.GetSuggestedCheckpointFileName(),
                ".json",
                new FilePickerFileType("SignalWeave checkpoint") { Patterns = ["*.swcheckpoint.json"] },
                new FilePickerFileType("JSON files") { Patterns = ["*.json"] });

            if (file is null)
            {
                return;
            }

            var path = file.TryGetLocalPath()
                ?? throw new InvalidOperationException("This platform did not provide a local file path for the selected checkpoint file.");
            SignalWeaveCheckpointSerializer.SaveFile(
                path,
                ViewModel.GetLoadedDefinition(),
                ViewModel.GetLoadedPatternSet(),
                ViewModel.GetCurrentWeights(),
                ViewModel.GetCompletedCycles());
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

    private async void ExportHiddenActivations_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunWithConsoleAsync(async () =>
        {
            var file = await PickSaveFileAsync(
                "Export hidden units activations",
                ViewModel.GetSuggestedHiddenActivationFileName(),
                ".dat",
                new FilePickerFileType("Data - Files") { Patterns = ["*.dat"] },
                new FilePickerFileType("All files") { Patterns = ["*.*"] });

            if (file is null)
            {
                return;
            }

            var text = ViewModel.BuildHiddenActivationExportText();
            await WriteAllTextAsync(file, text);
            var path = file.TryGetLocalPath() ?? file.Name;
            ViewModel.ReportHiddenActivationExport(path);
        });
    }

    private async void ShowOutputClusterReport_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ShowReportWindowAsync(ViewModel.CreateOutputClusterReport());
    }

    private async void ShowHiddenClusterReport_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ShowReportWindowAsync(ViewModel.CreateHiddenClusterReport());
    }

    private async void ShowCompatibilityReport_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ShowReportWindowAsync(ViewModel.CreateCompatibilityReport());
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

    private async void ClearMessageWindow_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunWithConsoleAsync(() =>
        {
            ViewModel.MessageWindow.ClearCommand.Execute(null);
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

    private async Task ShowReportWindowAsync(TextReportSnapshot snapshot)
    {
        await RunWithConsoleAsync(async () =>
        {
            var window = new TextReportWindow(snapshot);
            await window.ShowDialog(this);
        });
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
