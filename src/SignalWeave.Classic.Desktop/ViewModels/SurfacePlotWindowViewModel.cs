using System.Collections.ObjectModel;

namespace SignalWeave.Desktop.ViewModels;

public class SurfacePlotWindowViewModel : ViewModelBase
{
    public SurfacePlotWindowViewModel()
        : this(new SurfacePlotSnapshot(
            "3D plot",
            "Input0",
            "Input1",
            "Target1",
            "1.000",
            "0.500",
            "0.000",
            "0.000",
            "0.500",
            "1.000",
            new[]
            {
                new SurfacePlotCell(40, 170, 140, 100, "#C67B47", "0, 1 | Target1=0.6", "0.6"),
                new SurfacePlotCell(180, 170, 140, 100, "#D14D3F", "1, 1 | Target1=0.9", "0.9"),
                new SurfacePlotCell(40, 70, 140, 100, "#3A6EAA", "0, 0 | Target1=0.1", "0.1"),
                new SurfacePlotCell(180, 70, 140, 100, "#5E96B5", "1, 0 | Target1=0.4", "0.4")
            }))
    {
    }

    public SurfacePlotWindowViewModel(SurfacePlotSnapshot snapshot)
    {
        WindowTitle = snapshot.Title;
        XAxisTitle = snapshot.XAxisTitle;
        YAxisTitle = snapshot.YAxisTitle;
        ZAxisTitle = snapshot.ZAxisTitle;
        YAxisTopLabel = snapshot.YAxisTopLabel;
        YAxisMidLabel = snapshot.YAxisMidLabel;
        YAxisBottomLabel = snapshot.YAxisBottomLabel;
        XAxisLeftLabel = snapshot.XAxisLeftLabel;
        XAxisMidLabel = snapshot.XAxisMidLabel;
        XAxisRightLabel = snapshot.XAxisRightLabel;
        Cells = new ObservableCollection<SurfacePlotCell>(snapshot.Cells);
    }

    public ObservableCollection<SurfacePlotCell> Cells { get; }

    public string WindowTitle { get; }
    public string XAxisTitle { get; }
    public string YAxisTitle { get; }
    public string ZAxisTitle { get; }
    public string YAxisTopLabel { get; }
    public string YAxisMidLabel { get; }
    public string YAxisBottomLabel { get; }
    public string XAxisLeftLabel { get; }
    public string XAxisMidLabel { get; }
    public string XAxisRightLabel { get; }
}
