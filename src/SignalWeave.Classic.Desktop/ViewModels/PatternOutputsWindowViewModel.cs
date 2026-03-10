using System.Collections.ObjectModel;

namespace SignalWeave.Desktop.ViewModels;

public partial class PatternOutputsWindowViewModel : ViewModelBase
{
    public PatternOutputsWindowViewModel()
        : this(new PatternOutputsSnapshot(
            "Patterns and Outputs",
            "Average error: 0.000000",
            new[]
            {
                new PatternOutputRow(1, "pattern-1", "0.000 0.000", "0.000", "0.012", "0.000144")
            }))
    {
    }

    public PatternOutputsWindowViewModel(PatternOutputsSnapshot snapshot)
    {
        WindowTitle = snapshot.Title;
        Summary = snapshot.Summary;
        Rows = new ObservableCollection<PatternOutputRow>(snapshot.Rows);
    }

    public ObservableCollection<PatternOutputRow> Rows { get; }

    public string WindowTitle { get; }
    public string Summary { get; }
}
