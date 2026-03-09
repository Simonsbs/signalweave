using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace SignalWeave.Desktop.ViewModels;

public class SurfacePlotSetupWindowViewModel : ViewModelBase
{
    private readonly IReadOnlyList<SurfacePlotAxisOption> _axisOptions;
    private readonly IReadOnlyList<SurfacePlotZOption> _zOptions;
    private readonly IReadOnlyList<SurfacePlotSample> _samples;

    public SurfacePlotSetupWindowViewModel()
        : this(new SurfacePlotSetupSession(
            "Plot Setup",
            new[]
            {
                new SurfacePlotAxisOption("Input1", "Input1", 0),
                new SurfacePlotAxisOption("Input2", "Input2", 1)
            },
            new[]
            {
                new SurfacePlotZOption("Target1", "Target1", true, 0),
                new SurfacePlotZOption("Output1", "Output1", false, 0)
            },
            new[]
            {
                new SurfacePlotSample("p1", new[] { 0.0, 0.0 }, new[] { 0.0 }, new[] { 0.1 }),
                new SurfacePlotSample("p2", new[] { 0.0, 1.0 }, new[] { 1.0 }, new[] { 0.8 }),
                new SurfacePlotSample("p3", new[] { 1.0, 0.0 }, new[] { 1.0 }, new[] { 0.9 }),
                new SurfacePlotSample("p4", new[] { 1.0, 1.0 }, new[] { 0.0 }, new[] { 0.2 })
            }))
    {
    }

    public SurfacePlotSetupWindowViewModel(SurfacePlotSetupSession session)
    {
        _axisOptions = session.AxisOptions;
        _zOptions = session.ZOptions;
        _samples = session.Samples;

        WindowTitle = "Plot Setup";
        XAxisOptions = new ReadOnlyCollection<string>(_axisOptions.Select(option => option.Label).ToArray());
        YAxisOptions = new ReadOnlyCollection<string>(_axisOptions.Select(option => option.Label).ToArray());
        ZAxisOptions = new ReadOnlyCollection<string>(_zOptions.Select(option => option.Label).ToArray());
        SelectedX = XAxisOptions.FirstOrDefault() ?? "Input1";
        SelectedY = YAxisOptions.Skip(1).FirstOrDefault() ?? SelectedX;
        SelectedZ = ZAxisOptions.FirstOrDefault() ?? "Output1";
        UpdateSummary();
    }

    public IReadOnlyList<string> XAxisOptions { get; }
    public IReadOnlyList<string> YAxisOptions { get; }
    public IReadOnlyList<string> ZAxisOptions { get; }
    public string WindowTitle { get; }

    public string SelectedX { get; set; } = "Input1";
    public string SelectedY { get; set; } = "Input2";
    public string SelectedZ { get; set; } = "Output1";
    public string Summary { get; private set; } = "Select axes and click Show Plot.";

    public void UpdateSummary()
    {
        Summary = $"X: {SelectedX}    Y: {SelectedY}    Z: {SelectedZ}";
        OnPropertyChanged(nameof(Summary));
    }

    public PlotWindowSnapshot BuildPlotSnapshot()
    {
        if (_samples.Count == 0)
        {
            throw new InvalidOperationException("Patterns are not yet loaded.");
        }

        var xOption = _axisOptions.First(option => option.Label == SelectedX);
        var yOption = _axisOptions.First(option => option.Label == SelectedY);
        var zOption = _zOptions.First(option => option.Label == SelectedZ);

        var xValues = _samples.Select(sample => sample.Inputs[xOption.InputIndex]).ToArray();
        var yValues = _samples.Select(sample => sample.Inputs[yOption.InputIndex]).ToArray();
        var zValues = _samples.Select(sample => ReadZValue(sample, zOption)).ToArray();

        var minX = xValues.Min();
        var maxX = xValues.Max();
        var minY = yValues.Min();
        var maxY = yValues.Max();
        var minZ = zValues.Min();
        var maxZ = zValues.Max();

        var markers = new List<PlotMarkerItem>();
        for (var index = 0; index < _samples.Count; index++)
        {
            var plotX = 20 + Normalize(xValues[index], minX, maxX) * 300;
            var plotY = 20 + (1 - Normalize(yValues[index], minY, maxY)) * 190;
            var size = 8 + Normalize(zValues[index], minZ, maxZ) * 10;
            var fill = HeatColor(Normalize(zValues[index], minZ, maxZ));
            markers.Add(new PlotMarkerItem(
                plotX - (size / 2),
                plotY - (size / 2),
                size,
                size,
                fill,
                $"{_samples[index].Label} | {SelectedZ}={zValues[index].ToString("0.000", CultureInfo.InvariantCulture)}"));
        }

        return new PlotWindowSnapshot(
            $"{WindowTitle} - {SelectedZ}",
            $"{SelectedX} vs {SelectedY} using {SelectedZ}",
            string.Empty,
            maxY.ToString("0.000", CultureInfo.InvariantCulture),
            ((minY + maxY) / 2).ToString("0.000", CultureInfo.InvariantCulture),
            minY.ToString("0.000", CultureInfo.InvariantCulture),
            minX.ToString("0.000", CultureInfo.InvariantCulture),
            ((minX + maxX) / 2).ToString("0.000", CultureInfo.InvariantCulture),
            maxX.ToString("0.000", CultureInfo.InvariantCulture),
            SelectedX,
            SelectedY,
            markers);
    }

    private static double ReadZValue(SurfacePlotSample sample, SurfacePlotZOption option)
    {
        if (option.UsesTargets)
        {
            if (sample.Targets is null || option.OutputIndex >= sample.Targets.Length)
            {
                return 0.0;
            }

            return sample.Targets[option.OutputIndex];
        }

        return option.OutputIndex < sample.Outputs.Length ? sample.Outputs[option.OutputIndex] : 0.0;
    }

    private static double Normalize(double value, double min, double max)
    {
        if (Math.Abs(max - min) < 0.000001)
        {
            return 0.5;
        }

        return (value - min) / (max - min);
    }

    private static string HeatColor(double normalized)
    {
        if (normalized < 0.25)
        {
            return "#3A6EAA";
        }

        if (normalized < 0.5)
        {
            return "#5E96B5";
        }

        if (normalized < 0.75)
        {
            return "#C67B47";
        }

        return "#D14D3F";
    }
}
