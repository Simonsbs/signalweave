using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SignalWeave.Core;

namespace SignalWeave.Desktop.ViewModels;

public partial class WeightDisplayWindowViewModel : ViewModelBase
{
    private readonly WeightSet _weights;
    private readonly IReadOnlyList<WeightLayerOption> _layerOptions;

    public WeightDisplayWindowViewModel()
        : this(new WeightDisplaySession(
            "Weights",
            new WeightSet(
                new double[,] { { -0.8, -0.7 }, { 0.9, 0.2 }, { -0.1, 0.6 } },
                new double[,] { { 0.8 }, { -0.4 }, { 0.1 } })))
    {
    }

    public WeightDisplayWindowViewModel(WeightDisplaySession session)
    {
        _weights = session.Weights.Clone();
        BaseTitle = $"{session.Title} - Weights";

        _layerOptions = BuildLayerOptions(_weights).ToArray();
        WeightLayerOptions = new ReadOnlyCollection<string>(_layerOptions.Select(option => option.Id).ToArray());
        SelectedWeightLayer = WeightLayerOptions[0];
        RebuildWeightMap();
    }

    public IReadOnlyList<string> WeightLayerOptions { get; }
    public ObservableCollection<WeightGlyphItem> WeightGlyphs { get; } = [];

    [ObservableProperty]
    private string _baseTitle = "Weights";

    [ObservableProperty]
    private string _windowTitle = "Weights";

    [ObservableProperty]
    private string _selectedWeightLayer = string.Empty;

    [ObservableProperty]
    private string _weightMapSummary = "No weights loaded.";

    [ObservableProperty]
    private string _selectedLayerDescription = "Input -> Hidden";

    partial void OnSelectedWeightLayerChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            RebuildWeightMap();
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        RebuildWeightMap();
    }

    private void RebuildWeightMap()
    {
        WeightGlyphs.Clear();

        var layer = _layerOptions.FirstOrDefault(option => option.Id == SelectedWeightLayer) ?? _layerOptions[0];
        var matrix = layer.MatrixSelector(_weights);
        var layerTitle = layer.Description;

        var rows = matrix.GetLength(0);
        var columns = matrix.GetLength(1);
        var maxValue = Flatten(matrix).Select(Math.Abs).DefaultIfEmpty(1.0).Max();
        const double cellSize = 156;

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var weight = matrix[row, column];
                var normalized = Math.Abs(weight) / Math.Max(0.000001, maxValue);
                var size = 16 + (normalized * 118);
                var cellFill = ((row + column) % 2 == 0) ? "#C8C8C8" : "#D0D0D0";
                var cellX = column * cellSize;
                var cellY = row * cellSize;

                WeightGlyphs.Add(new WeightGlyphItem(
                    cellX,
                    cellY,
                    cellSize - 6,
                    cellSize - 6,
                    cellFill,
                    cellX + ((cellSize - 6 - size) / 2),
                    cellY + ((cellSize - 6 - size) / 2),
                    size,
                    size,
                    weight >= 0 ? "#020202" : "#FF1616",
                    weight.ToString("0.000000", CultureInfo.InvariantCulture)));
            }
        }

        WeightMapSummary = $"{layerTitle} | rows={rows}, cols={columns}, max |w|={maxValue.ToString("0.000000", CultureInfo.InvariantCulture)}";
        SelectedLayerDescription = layerTitle;
        WindowTitle = $"{BaseTitle} ({layer.Id}: {layerTitle})";
    }

    private static IEnumerable<WeightLayerOption> BuildLayerOptions(WeightSet weights)
    {
        if (weights.HiddenHidden is not null)
        {
            yield return new WeightLayerOption("1", "Input -> Hidden1", value => value.InputHidden);
            yield return new WeightLayerOption("2", "Hidden1 -> Hidden2", value => value.HiddenHidden!);
            yield return new WeightLayerOption("3", "Hidden2 -> Output", value => value.HiddenOutput);
            yield break;
        }

        yield return new WeightLayerOption("1", "Input -> Hidden", value => value.InputHidden);
        yield return new WeightLayerOption("2", "Hidden -> Output", value => value.HiddenOutput);

        if (weights.RecurrentHidden is not null)
        {
            yield return new WeightLayerOption("3", "Hidden -> Hidden", value => value.RecurrentHidden!);
        }
    }

    private static IEnumerable<double> Flatten(double[,] matrix)
    {
        for (var row = 0; row < matrix.GetLength(0); row++)
        {
            for (var column = 0; column < matrix.GetLength(1); column++)
            {
                yield return matrix[row, column];
            }
        }
    }

    private sealed record WeightLayerOption(string Id, string Description, Func<WeightSet, double[,]> MatrixSelector);
}
