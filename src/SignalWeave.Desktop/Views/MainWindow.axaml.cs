using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SignalWeave.Core;
using SignalWeave.Desktop.ViewModels;

namespace SignalWeave.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private async void ConfigureNetwork_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var window = new NetworkConfigWindow(ViewModel.GetCurrentDefinition());
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
            ViewModel.LoadNetworkText(text, Path.GetFileNameWithoutExtension(file.Name));
        });
    }

    private async void SaveNetwork_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunWithConsoleAsync(async () =>
        {
            var definition = ViewModel.GetCurrentDefinition();
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
            ViewModel.ConsoleText = $"Saved network to {file.TryGetLocalPath() ?? file.Path.AbsoluteUri}";
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
            ViewModel.LoadPatternText(text, Path.GetFileNameWithoutExtension(file.Name));
        });
    }

    private async void LoadWeights_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunWithConsoleAsync(async () =>
        {
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
            ViewModel.ConsoleText = $"Loaded weights from {path}";
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
            ViewModel.ConsoleText = $"Saved weights to {path}";
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
