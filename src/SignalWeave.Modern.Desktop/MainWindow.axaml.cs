using Avalonia.Controls;
using SignalWeave.Core;

namespace SignalWeave.Modern.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var xorDefinition = BasicPropNetworkConfigParser.Parse(SignalWeaveSamples.XorConfig, "XOR demo");
        var srnDefinition = BasicPropNetworkConfigParser.Parse(SignalWeaveSamples.EchoSrnConfig, "Echo SRN demo");

        EngineSummaryText.Text =
            "Shared modules: parser, trainer, clustering, checkpointing, and sample assets from SignalWeave.Core.";
        XorSummaryText.Text = xorDefinition.ToSummary();
        SrnSummaryText.Text = srnDefinition.ToSummary();
    }
}
