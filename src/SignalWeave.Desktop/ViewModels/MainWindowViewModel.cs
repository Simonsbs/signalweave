using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SignalWeave.Core;

namespace SignalWeave.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private NetworkDefinition? _definition;
    private PatternSet? _patternSet;
    private SignalWeaveEngine? _engine;

    [ObservableProperty]
    private string _sampleTitle = "XOR demo";

    [ObservableProperty]
    private string _configText = SignalWeaveSamples.XorConfig;

    [ObservableProperty]
    private string _patternText = SignalWeaveSamples.XorPatterns;

    [ObservableProperty]
    private string _resultText = "Use the built-in demos or paste your own BasicProp-style config and pattern files.";

    [ObservableProperty]
    private string _featureText = CompatibilityProfile.ToDisplayText();

    public MainWindowViewModel()
    {
        ParseCurrent();
    }

    [RelayCommand]
    private void LoadXorDemo()
    {
        SampleTitle = "XOR demo";
        ConfigText = SignalWeaveSamples.XorConfig;
        PatternText = SignalWeaveSamples.XorPatterns;
        ResultText = "Loaded XOR demo.";
        ParseCurrent();
    }

    [RelayCommand]
    private void LoadSrnDemo()
    {
        SampleTitle = "Echo SRN demo";
        ConfigText = SignalWeaveSamples.EchoSrnConfig;
        PatternText = SignalWeaveSamples.EchoSrnPatterns;
        ResultText = "Loaded SRN demo.";
        ParseCurrent();
    }

    [RelayCommand]
    private void Parse()
    {
        ParseCurrent();
        ResultText = $"{_definition!.ToSummary()}{Environment.NewLine}{_patternSet!.ToSummary()}";
    }

    [RelayCommand]
    private void Train()
    {
        ParseCurrent();
        _engine = new SignalWeaveEngine(_definition!, seed: 42);
        var result = _engine.Train(_patternSet!);
        ResultText =
            $"Trained {_definition!.Name} in {result.History.Count} epochs.{Environment.NewLine}" +
            $"Final error: {result.FinalPoint.AverageError:0.000}{Environment.NewLine}{Environment.NewLine}" +
            result.FinalRun.ToTable();
    }

    [RelayCommand]
    private void Test()
    {
        EnsureEngine();
        ResultText = _engine!.TestAll(_patternSet!).ToTable();
    }

    [RelayCommand]
    private void ClusterOutputs()
    {
        EnsureEngine();
        ResultText = _engine!.ClusterOutputs(_patternSet!).ToDisplayText();
    }

    [RelayCommand]
    private void ClusterHidden()
    {
        EnsureEngine();
        ResultText = _engine!.ClusterHiddenStates(_patternSet!).ToDisplayText();
    }

    private void EnsureEngine()
    {
        ParseCurrent();
        _engine ??= new SignalWeaveEngine(_definition!, seed: 42);
    }

    private void ParseCurrent()
    {
        _definition = BasicPropNetworkConfigParser.Parse(ConfigText, SampleTitle);
        _patternSet = PatternSetParser.Parse(PatternText, SampleTitle);
    }
}
