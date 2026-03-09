using System.Collections.ObjectModel;

namespace SignalWeave.Desktop.ViewModels;

public partial class UtilityPlotWindowViewModel : ViewModelBase
{
    public UtilityPlotWindowViewModel()
        : this(new PlotWindowSnapshot(
            "Utility Plot",
            "No utility plot prepared.",
            "20,160 120,40 220,120",
            "0.0",
            "0.5",
            "1.0",
            "0",
            "5",
            "10",
            "X axis",
            "Y axis",
            new[]
            {
                new PlotMarkerItem(20, 160, 8, 8, "#B24C3D", "p1"),
                new PlotMarkerItem(120, 40, 8, 8, "#B24C3D", "p2")
            }))
    {
    }

    public UtilityPlotWindowViewModel(PlotWindowSnapshot snapshot)
    {
        WindowTitle = snapshot.Title;
        Summary = snapshot.Summary;
        PlotPoints = snapshot.Points;
        YAxisTopLabel = snapshot.YAxisTopLabel;
        YAxisMidLabel = snapshot.YAxisMidLabel;
        YAxisBottomLabel = snapshot.YAxisBottomLabel;
        XAxisLeftLabel = snapshot.XAxisLeftLabel;
        XAxisMidLabel = snapshot.XAxisMidLabel;
        XAxisRightLabel = snapshot.XAxisRightLabel;
        XAxisTitle = snapshot.XAxisTitle;
        YAxisTitle = snapshot.YAxisTitle;
        Markers = new ObservableCollection<PlotMarkerItem>(snapshot.Markers);
    }

    public ObservableCollection<PlotMarkerItem> Markers { get; }

    public string WindowTitle { get; }
    public string Summary { get; }
    public string PlotPoints { get; }
    public string YAxisTopLabel { get; }
    public string YAxisMidLabel { get; }
    public string YAxisBottomLabel { get; }
    public string XAxisLeftLabel { get; }
    public string XAxisMidLabel { get; }
    public string XAxisRightLabel { get; }
    public string XAxisTitle { get; }
    public string YAxisTitle { get; }
}
