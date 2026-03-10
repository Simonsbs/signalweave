namespace SignalWeave.Desktop.ViewModels;

public sealed class TextReportWindowViewModel : ViewModelBase
{
    public TextReportWindowViewModel()
        : this(new TextReportSnapshot("Report", string.Empty))
    {
    }

    public TextReportWindowViewModel(TextReportSnapshot snapshot)
    {
        WindowTitle = snapshot.Title;
        ReportText = snapshot.Text;
    }

    public string WindowTitle { get; }
    public string ReportText { get; }
}
