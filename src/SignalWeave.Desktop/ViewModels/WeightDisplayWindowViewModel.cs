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
    private const double CellSize = 96;
    private const double CellInset = 4;
    private const double CanvasPadding = 10;
    private readonly WeightDisplaySession _session;
    private WeightSet _weights;
    private IReadOnlyList<WeightLayerOption> _layerOptions = [];

    public WeightDisplayWindowViewModel()
        : this(new WeightDisplaySession(
            "Weights",
            () => new WeightSet(
                new double[,] { { -0.8, -0.7 }, { 0.9, 0.2 }, { -0.1, 0.6 } },
                new double[,] { { 0.8 }, { -0.4 }, { 0.1 } })))
    {
    }

    public WeightDisplayWindowViewModel(WeightDisplaySession session)
    {
        _session = session;
        _weights = session.WeightSource();
        BaseTitle = session.Title;
        RebuildLayerOptions(preserveSelection: false);
    }

    public ObservableCollection<string> WeightLayerOptions { get; } = [];
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
    private double _canvasWidth = 320;

    [ObservableProperty]
    private double _canvasHeight = 320;

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
        _weights = _session.WeightSource();
        RebuildLayerOptions(preserveSelection: true);
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
        CanvasWidth = Math.Max(280, (columns * CellSize) + (CanvasPadding * 2));
        CanvasHeight = Math.Max(280, (rows * CellSize) + (CanvasPadding * 2));

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var weight = matrix[row, column];
                var normalized = Math.Abs(weight) / Math.Max(0.000001, maxValue);
                var size = 12 + (normalized * 68);
                var cellFill = ((row + column) % 2 == 0) ? "#C8C8C8" : "#D0D0D0";
                var cellX = CanvasPadding + (column * CellSize);
                var cellY = CanvasPadding + (row * CellSize);

                WeightGlyphs.Add(new WeightGlyphItem(
                    cellX,
                    cellY,
                    CellSize - CellInset,
                    CellSize - CellInset,
                    cellFill,
                    cellX + (((CellSize - CellInset) - size) / 2),
                    cellY + (((CellSize - CellInset) - size) / 2),
                    size,
                    size,
                    weight >= 0 ? "#020202" : "#FF1616",
                    weight.ToString("0.000000", CultureInfo.InvariantCulture)));
            }
        }

        WeightMapSummary = $"{layerTitle} | rows={rows}, cols={columns}, max |w|={maxValue.ToString("0.000000", CultureInfo.InvariantCulture)}";
        WindowTitle = $"{BaseTitle} - {layer.Id}";
    }

    private void RebuildLayerOptions(bool preserveSelection)
    {
        var previousSelection = SelectedWeightLayer;
        _layerOptions = BuildLayerOptions(_weights).ToArray();

        WeightLayerOptions.Clear();
        foreach (var option in _layerOptions)
        {
            WeightLayerOptions.Add(option.Id);
        }

        if (WeightLayerOptions.Count == 0)
        {
            SelectedWeightLayer = string.Empty;
            WeightGlyphs.Clear();
            WeightMapSummary = "No weights loaded.";
            WindowTitle = BaseTitle;
            return;
        }

        if (preserveSelection && !string.IsNullOrWhiteSpace(previousSelection) && WeightLayerOptions.Contains(previousSelection))
        {
            SelectedWeightLayer = previousSelection;
            RebuildWeightMap();
            return;
        }

        SelectedWeightLayer = WeightLayerOptions[0];
    }

    private static IEnumerable<WeightLayerOption> BuildLayerOptions(WeightSet weights)
    {
        if (weights.HiddenOutput.Length == 0 && weights.HiddenHidden is null && weights.RecurrentHidden is null)
        {
            yield return new WeightLayerOption("1", "Input -> Output", value => value.InputHidden);
            yield break;
        }

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
            yield return new WeightLayerOption("Rec", "Hidden -> Hidden", value => value.RecurrentHidden!);
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
