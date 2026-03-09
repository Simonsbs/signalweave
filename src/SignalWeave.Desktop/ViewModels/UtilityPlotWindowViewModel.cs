using System.Collections.ObjectModel;

namespace SignalWeave.Desktop.ViewModels;

public partial class UtilityPlotWindowViewModel : ViewModelBase
{
    public UtilityPlotWindowViewModel()
        : this(new PlotWindowSnapshot(
            "Utility Plot",
            "No utility plot prepared.",
            "20,160 120,40 220,120",
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
        Markers = new ObservableCollection<PlotMarkerItem>(snapshot.Markers);
    }

    public ObservableCollection<PlotMarkerItem> Markers { get; }

    public string WindowTitle { get; }
    public string Summary { get; }
    public string PlotPoints { get; }
}
