using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SignalWeave.Core;

namespace SignalWeave.Desktop.ViewModels;

public partial class NetworkConfigDialogViewModel : ViewModelBase
{
    private readonly string[] _learningRateOptions = ["0.1", "0.2", "0.3", "0.4", "0.5", "0.6", "0.7", "0.8", "0.9", "1.0", "5.0"];
    private readonly string[] _momentumOptions = ["0", "0.2", "0.5", "0.6", "0.7", "0.8", "0.9", "1.0"];
    private readonly string[] _epochOptions = ["100", "200", "500", "1000", "2000", "5000", "10000", "20000", "50000", "100000"];
    private readonly string[] _rangeOptions = ["0.1", "1.0", "10.0"];
    private readonly string[] _unitOptions = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "10"];

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
        LearningRateOptions = new ReadOnlyCollection<string>(_learningRateOptions);
        MomentumOptions = new ReadOnlyCollection<string>(_momentumOptions);
        EpochOptions = new ReadOnlyCollection<string>(_epochOptions);
        RangeOptions = new ReadOnlyCollection<string>(_rangeOptions);
        UnitOptions = new ReadOnlyCollection<string>(_unitOptions);

        Name = definition.Name;
        SelectedTabIndex = definition.NetworkKind == NetworkKind.SimpleRecurrent ? 1 : 0;
        SelectedInputUnits = definition.InputUnits.ToString(CultureInfo.InvariantCulture);
        SelectedHiddenUnits = definition.HiddenUnits.ToString(CultureInfo.InvariantCulture);
        SelectedOutputUnits = definition.OutputUnits.ToString(CultureInfo.InvariantCulture);
        UseInputBias = definition.UseInputBias;
        UseHiddenBias = definition.UseHiddenBias;
        SelectedLearningRate = PickNearest(_learningRateOptions, definition.LearningRate);
        SelectedMomentum = PickNearest(_momentumOptions, definition.Momentum);
        SelectedEpochs = PickNearest(_epochOptions, definition.MaxEpochs);
        SelectedRange = PickNearest(_rangeOptions, definition.RandomWeightRange);
        BatchUpdate = definition.UpdateMode == UpdateMode.Batch;
        CrossEntropy = definition.CostFunction == CostFunction.CrossEntropy;
        ErrorThreshold = definition.ErrorThreshold.ToString("0.######", CultureInfo.InvariantCulture);
    }

    public IReadOnlyList<string> LearningRateOptions { get; }
    public IReadOnlyList<string> MomentumOptions { get; }
    public IReadOnlyList<string> EpochOptions { get; }
    public IReadOnlyList<string> RangeOptions { get; }
    public IReadOnlyList<string> UnitOptions { get; }

    [ObservableProperty]
    private string _name = "Untitled";

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private string _selectedInputUnits = "2";

    [ObservableProperty]
    private string _selectedHiddenUnits = "3";

    [ObservableProperty]
    private string _selectedOutputUnits = "1";

    [ObservableProperty]
    private bool _useInputBias = true;

    [ObservableProperty]
    private bool _useHiddenBias = true;

    [ObservableProperty]
    private string _selectedLearningRate = "0.3";

    [ObservableProperty]
    private string _selectedMomentum = "0.8";

    [ObservableProperty]
    private string _selectedEpochs = "5000";

    [ObservableProperty]
    private string _selectedRange = "1.0";

    [ObservableProperty]
    private bool _batchUpdate;

    [ObservableProperty]
    private bool _crossEntropy;

    [ObservableProperty]
    private string _errorThreshold = "0.01";

    [ObservableProperty]
    private string _statusText = string.Empty;

    public NetworkDefinition BuildDefinition()
    {
        var definition = new NetworkDefinition
        {
            Name = string.IsNullOrWhiteSpace(Name) ? "Untitled" : Name.Trim(),
            NetworkKind = SelectedTabIndex == 1 ? NetworkKind.SimpleRecurrent : NetworkKind.FeedForward,
            InputUnits = int.Parse(SelectedInputUnits, CultureInfo.InvariantCulture),
            HiddenUnits = int.Parse(SelectedHiddenUnits, CultureInfo.InvariantCulture),
            OutputUnits = int.Parse(SelectedOutputUnits, CultureInfo.InvariantCulture),
            UseInputBias = UseInputBias,
            UseHiddenBias = UseHiddenBias,
            LearningRate = double.Parse(SelectedLearningRate, CultureInfo.InvariantCulture),
            Momentum = double.Parse(SelectedMomentum, CultureInfo.InvariantCulture),
            RandomWeightRange = double.Parse(SelectedRange, CultureInfo.InvariantCulture),
            SigmoidPrimeOffset = 0.1,
            MaxEpochs = int.Parse(SelectedEpochs, CultureInfo.InvariantCulture),
            ErrorThreshold = double.Parse(ErrorThreshold, CultureInfo.InvariantCulture),
            UpdateMode = BatchUpdate ? UpdateMode.Batch : UpdateMode.Pattern,
            CostFunction = CrossEntropy ? CostFunction.CrossEntropy : CostFunction.SumSquaredError
        };

        definition.Validate();
        return definition;
    }

    private static string PickNearest(IEnumerable<string> options, double value)
    {
        return options
            .OrderBy(option => Math.Abs(double.Parse(option, CultureInfo.InvariantCulture) - value))
            .First();
    }

    private static string PickNearest(IEnumerable<string> options, int value)
    {
        return options
            .OrderBy(option => Math.Abs(int.Parse(option, CultureInfo.InvariantCulture) - value))
            .First();
    }
}
