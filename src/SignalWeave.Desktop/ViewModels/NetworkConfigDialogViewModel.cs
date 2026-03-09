using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SignalWeave.Core;

namespace SignalWeave.Desktop.ViewModels;

public partial class NetworkConfigDialogViewModel : ViewModelBase
{
    private readonly NetworkDefinition _baseDefinition;

    public NetworkConfigDialogViewModel()
        : this(new NetworkDefinition
        {
            Name = "Untitled",
            InputUnits = 2,
            HiddenUnits = 3,
            OutputUnits = 1
        })
    {
    }

    public NetworkConfigDialogViewModel(NetworkDefinition definition)
    {
        _baseDefinition = definition;

        SelectedTabIndex = definition.NetworkKind == NetworkKind.SimpleRecurrent ? 1 : 0;
        Name = definition.Name;
        InputUnitsValue = definition.InputUnits;
        HiddenUnitsValue = definition.HiddenUnits;
        OutputUnitsValue = definition.OutputUnits;
        SecondHiddenUnitsValue = definition.SecondHiddenUnits > 0 ? definition.SecondHiddenUnits : definition.HiddenUnits;
        FeedForwardLayersValue = definition.NetworkKind == NetworkKind.FeedForward ? definition.TotalLayerCount : 3;
        UseInputBias = definition.UseInputBias;
        UseHiddenBias = definition.UseHiddenBias;
        UseSecondHiddenBias = definition.UseSecondHiddenBias;
        UpdateFeedForwardStatus();
    }

    [ObservableProperty]
    private string _name = "Untitled";

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private double _feedForwardLayersValue = 3;

    [ObservableProperty]
    private double _inputUnitsValue = 2;

    [ObservableProperty]
    private double _hiddenUnitsValue = 3;

    [ObservableProperty]
    private double _secondHiddenUnitsValue = 3;

    [ObservableProperty]
    private double _outputUnitsValue = 1;

    [ObservableProperty]
    private bool _useInputBias = true;

    [ObservableProperty]
    private bool _useHiddenBias = true;

    [ObservableProperty]
    private bool _useSecondHiddenBias = true;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public int FeedForwardLayersDisplay => (int)Math.Round(FeedForwardLayersValue, MidpointRounding.AwayFromZero);
    public int InputUnitsDisplay => (int)Math.Round(InputUnitsValue, MidpointRounding.AwayFromZero);
    public int HiddenUnitsDisplay => (int)Math.Round(HiddenUnitsValue, MidpointRounding.AwayFromZero);
    public int SecondHiddenUnitsDisplay => (int)Math.Round(SecondHiddenUnitsValue, MidpointRounding.AwayFromZero);
    public int OutputUnitsDisplay => (int)Math.Round(OutputUnitsValue, MidpointRounding.AwayFromZero);
    public bool IsFeedForwardSecondLayerEnabled => FeedForwardLayersDisplay >= 4;

    public NetworkDefinition BuildDefinition()
    {
        if (SelectedTabIndex == 0 && FeedForwardLayersDisplay < 3)
        {
            throw new InvalidOperationException("SignalWeave does not support 2-layer feed-forward networks yet.");
        }

        var isSrn = SelectedTabIndex == 1;
        var includeSecondHiddenLayer = !isSrn && FeedForwardLayersDisplay >= 4;
        var definition = new NetworkDefinition
        {
            Name = string.IsNullOrWhiteSpace(Name) ? "Untitled" : Name.Trim(),
            NetworkKind = isSrn ? NetworkKind.SimpleRecurrent : NetworkKind.FeedForward,
            InputUnits = InputUnitsDisplay,
            HiddenUnits = HiddenUnitsDisplay,
            SecondHiddenUnits = includeSecondHiddenLayer ? SecondHiddenUnitsDisplay : 0,
            OutputUnits = OutputUnitsDisplay,
            UseInputBias = UseInputBias,
            UseHiddenBias = UseHiddenBias,
            UseSecondHiddenBias = includeSecondHiddenLayer && UseSecondHiddenBias,
            LearningRate = _baseDefinition.LearningRate,
            Momentum = _baseDefinition.Momentum,
            RandomWeightRange = _baseDefinition.RandomWeightRange,
            SigmoidPrimeOffset = _baseDefinition.SigmoidPrimeOffset,
            MaxEpochs = _baseDefinition.MaxEpochs,
            ErrorThreshold = _baseDefinition.ErrorThreshold,
            UpdateMode = _baseDefinition.UpdateMode,
            CostFunction = _baseDefinition.CostFunction
        };

        definition.Validate();
        return definition;
    }

    partial void OnFeedForwardLayersValueChanged(double value)
    {
        OnPropertyChanged(nameof(FeedForwardLayersDisplay));
        OnPropertyChanged(nameof(IsFeedForwardSecondLayerEnabled));
        UpdateFeedForwardStatus();
    }

    partial void OnInputUnitsValueChanged(double value)
    {
        OnPropertyChanged(nameof(InputUnitsDisplay));
    }

    partial void OnHiddenUnitsValueChanged(double value)
    {
        OnPropertyChanged(nameof(HiddenUnitsDisplay));
    }

    partial void OnSecondHiddenUnitsValueChanged(double value)
    {
        OnPropertyChanged(nameof(SecondHiddenUnitsDisplay));
    }

    partial void OnOutputUnitsValueChanged(double value)
    {
        OnPropertyChanged(nameof(OutputUnitsDisplay));
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        UpdateFeedForwardStatus();
    }

    private void UpdateFeedForwardStatus()
    {
        if (SelectedTabIndex == 0 && FeedForwardLayersDisplay < 3)
        {
            StatusText = "SignalWeave does not support 2-layer feed-forward networks yet.";
            return;
        }

        StatusText = string.Empty;
    }
}
