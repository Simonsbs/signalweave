using SignalWeave.Core;

namespace SignalWeave.Core.Tests;

public class SignalWeaveEngineTests
{
    [Fact]
    public void ParsesFeedForwardConfig()
    {
        var definition = BasicPropNetworkConfigParser.Parse("""
            name = Parser test
            network = feedforward
            inputs = 3
            hidden = 5
            outputs = 2
            learningRate = 0.4
            momentum = 0.1
            update = batch
            cost = crossentropy
            """);

        Assert.Equal("Parser test", definition.Name);
        Assert.Equal(NetworkKind.FeedForward, definition.NetworkKind);
        Assert.Equal(3, definition.InputUnits);
        Assert.Equal(5, definition.HiddenUnits);
        Assert.Equal(0, definition.SecondHiddenUnits);
        Assert.Equal(2, definition.OutputUnits);
        Assert.Equal(UpdateMode.Batch, definition.UpdateMode);
        Assert.Equal(CostFunction.CrossEntropy, definition.CostFunction);
    }

    [Fact]
    public void ParsesFourLayerFeedForwardConfig()
    {
        var definition = BasicPropNetworkConfigParser.Parse("""
            name = Four layer parser test
            network = feedforward
            inputs = 2
            hidden = 4
            hidden2 = 3
            outputs = 1
            inputBias = true
            hiddenBias = true
            hidden2Bias = true
            """);

        Assert.Equal("Four layer parser test", definition.Name);
        Assert.Equal(NetworkKind.FeedForward, definition.NetworkKind);
        Assert.Equal(2, definition.InputUnits);
        Assert.Equal(4, definition.HiddenUnits);
        Assert.Equal(3, definition.SecondHiddenUnits);
        Assert.True(definition.HasSecondHiddenLayer);
        Assert.True(definition.UseSecondHiddenBias);
        Assert.Equal(4, definition.TotalLayerCount);
    }

    [Fact]
    public void ParsesPatternSequences()
    {
        var patterns = PatternSetParser.Parse("""
            reset
            a: 0 => 0
            b: 1 => 1
            reset
            c: 0 => 1
            """);

        Assert.Equal(3, patterns.Examples.Count);
        Assert.False(patterns.Examples[0].ResetsContextAfter);
        Assert.True(patterns.Examples[1].ResetsContextAfter);
        Assert.False(patterns.Examples[2].ResetsContextAfter);
        Assert.Equal(2, patterns.ToSequences().Count);
    }

    [Fact]
    public void TrainsAndFunctionToLowError()
    {
        var definition = BasicPropNetworkConfigParser.Parse("""
            name = AND
            network = feedforward
            inputs = 2
            hidden = 2
            outputs = 1
            learningRate = 0.7
            momentum = 0.2
            randomWeightRange = 0.5
            sigmoidPrimeOffset = 0.05
            maxEpochs = 6000
            errorThreshold = 0.005
            update = pattern
            cost = sse
            """);

        var patterns = PatternSetParser.Parse("""
            a: 0 0 => 0
            b: 0 1 => 0
            c: 1 0 => 0
            d: 1 1 => 1
            """);

        var engine = new SignalWeaveEngine(definition, seed: 7);
        var result = engine.Train(patterns);

        var run = engine.TestAll(patterns);
        Assert.True(run.AverageError < 0.07);
        Assert.True(run.Results[0].Outputs[0] < 0.2);
        Assert.True(run.Results[1].Outputs[0] < 0.2);
        Assert.True(run.Results[2].Outputs[0] < 0.2);
        Assert.True(run.Results[3].Outputs[0] > 0.8);
    }

    [Fact]
    public void MatchesBasicPropProbeForFixedWeightForwardPass()
    {
        var definition = BasicPropNetworkConfigParser.Parse("""
            name = XOR probe parity
            network = feedforward
            inputs = 2
            hidden = 2
            outputs = 1
            inputBias = true
            hiddenBias = true
            learningRate = 0.3
            momentum = 0.8
            randomWeightRange = 1.0
            maxEpochs = 1
            errorThreshold = 0.0
            update = pattern
            cost = sse
            """);

        var patterns = PatternSetParser.Parse("""
            a: 0 0 => 0
            b: 0 1 => 1
            c: 1 0 => 1
            d: 1 1 => 0
            """);

        var weights = new WeightSet(
            new double[,]
            {
                { 0.2, 0.5 },
                { -0.3, 0.6 },
                { 0.1, -0.4 }
            },
            new double[,]
            {
                { -0.8 },
                { 0.9 },
                { 0.7 }
            });

        var engine = new SignalWeaveEngine(definition, weights);
        var run = engine.TestAll(patterns);

        Assert.Equal(0.532614204303, run.AverageError, 12);

        Assert.Equal(0.655024164758, run.Results[0].Outputs[0], 12);
        Assert.Equal(0.697351557772, run.Results[1].Outputs[0], 12);
        Assert.Equal(0.671047653282, run.Results[2].Outputs[0], 12);
        Assert.Equal(0.711230750243, run.Results[3].Outputs[0], 12);

        Assert.Equal(0.524979187479, run.Results[0].HiddenActivations[0], 12);
        Assert.Equal(0.401312339888, run.Results[0].HiddenActivations[1], 12);
        Assert.Equal(0.450166002688, run.Results[1].HiddenActivations[0], 12);
        Assert.Equal(0.549833997312, run.Results[1].HiddenActivations[1], 12);
        Assert.Equal(0.574442516812, run.Results[2].HiddenActivations[0], 12);
        Assert.Equal(0.524979187479, run.Results[2].HiddenActivations[1], 12);
        Assert.Equal(0.5, run.Results[3].HiddenActivations[0], 12);
        Assert.Equal(0.668187772168, run.Results[3].HiddenActivations[1], 12);
    }

    [Fact]
    public void SupportsFixedWeightForwardPassForFourLayerFeedForward()
    {
        var definition = BasicPropNetworkConfigParser.Parse("""
            name = Four layer forward
            network = feedforward
            inputs = 2
            hidden = 2
            hidden2 = 2
            outputs = 1
            inputBias = false
            hiddenBias = false
            hidden2Bias = false
            learningRate = 0.3
            momentum = 0.8
            randomWeightRange = 1.0
            maxEpochs = 1
            errorThreshold = 0.0
            update = pattern
            cost = sse
            """);

        var patterns = PatternSetParser.Parse("""
            a: 1 0 => 1
            """);

        var weights = new WeightSet(
            new double[,]
            {
                { 0.1, 0.2 },
                { 0.3, 0.4 }
            },
            new double[,]
            {
                { 0.9 },
                { -1.0 }
            },
            hiddenHidden: new double[,]
            {
                { 0.5, -0.6 },
                { 0.7, 0.8 }
            });

        var engine = new SignalWeaveEngine(definition, weights);
        var run = engine.TestAll(patterns);

        Assert.Equal(0.514894860369, run.Results[0].Outputs[0], 12);
        Assert.Equal(4, run.Results[0].HiddenActivations.Length);
        Assert.Equal(0.524979187479, run.Results[0].HiddenActivations[0], 12);
        Assert.Equal(0.549833997312, run.Results[0].HiddenActivations[1], 12);
        Assert.Equal(0.656418318669, run.Results[0].HiddenActivations[2], 12);
        Assert.Equal(0.531179411791, run.Results[0].HiddenActivations[3], 12);
    }

    [Fact]
    public void MatchesBasicPropProbeForFixedWeightSrnForwardPass()
    {
        var definition = BasicPropNetworkConfigParser.Parse("""
            name = SRN probe parity
            network = srn
            inputs = 1
            hidden = 2
            outputs = 1
            inputBias = true
            hiddenBias = true
            learningRate = 0.3
            momentum = 0.8
            randomWeightRange = 1.0
            maxEpochs = 1
            errorThreshold = 0.0
            update = pattern
            cost = sse
            """);

        var patterns = PatternSetParser.Parse("""
            a: 0 => 0
            b: 1 => 1
            reset
            c: 1 => 0
            d: 0 => 1
            reset
            """);

        var weights = new WeightSet(
            new double[,]
            {
                { 0.4, 0.7 },
                { 0.2, -0.3 }
            },
            new double[,]
            {
                { -0.5 },
                { 0.8 },
                { 0.1 }
            },
            new double[,]
            {
                { 0.6, 0.1 },
                { -0.2, 0.4 }
            });

        var engine = new SignalWeaveEngine(definition, weights);
        var run = engine.TestAll(patterns);
        Assert.Equal(0.51637638731, run.Results[0].Outputs[0], 12);
        Assert.Equal(0.542741670816, run.Results[1].Outputs[0], 12);
        Assert.Equal(0.538951408832, run.Results[2].Outputs[0], 12);
        Assert.Equal(0.523331468988, run.Results[3].Outputs[0], 12);
    }

    [Fact]
    public void MatchesBasicPropProbeForSequentialSrnTraining()
    {
        var definition = BasicPropNetworkConfigParser.Parse("""
            name = SRN train parity
            network = srn
            inputs = 1
            hidden = 2
            outputs = 1
            inputBias = true
            hiddenBias = true
            learningRate = 0.3
            momentum = 0.8
            randomWeightRange = 1.0
            maxEpochs = 4
            errorThreshold = 0.0
            update = pattern
            cost = sse
            """);

        var patterns = PatternSetParser.Parse("""
            a: 0 => 0
            b: 1 => 1
            reset
            c: 1 => 0
            d: 0 => 1
            reset
            """);

        var weights = new WeightSet(
            new double[,]
            {
                { 0.4, 0.7 },
                { 0.2, -0.3 }
            },
            new double[,]
            {
                { -0.5 },
                { 0.8 },
                { 0.1 }
            },
            new double[,]
            {
                { 0.6, 0.1 },
                { -0.2, 0.4 }
            });

        var engine = new SignalWeaveEngine(definition, weights);
        var result = engine.Train(patterns, 4);
        var run = engine.TestAll(patterns);

        Assert.Equal(4, result.History.Count);
        Assert.Equal(0.398576432913, engine.Weights.InputHidden[0, 0], 12);
        Assert.Equal(0.702655047097, engine.Weights.InputHidden[0, 1], 12);
        Assert.Equal(0.203340134128, engine.Weights.InputHidden[1, 0], 12);
        Assert.Equal(-0.304381361609, engine.Weights.InputHidden[1, 1], 12);

        Assert.Equal(-0.503859340742, engine.Weights.HiddenOutput[0, 0], 12);
        Assert.Equal(0.800209495605, engine.Weights.HiddenOutput[1, 0], 12);
        Assert.Equal(0.084758793661, engine.Weights.HiddenOutput[2, 0], 12);

        Assert.Equal(0.593964802018, engine.Weights.RecurrentHidden![0, 0], 12);
        Assert.Equal(0.110344928474, engine.Weights.RecurrentHidden[0, 1], 12);
        Assert.Equal(-0.205093551689, engine.Weights.RecurrentHidden[1, 0], 12);
        Assert.Equal(0.408723508299, engine.Weights.RecurrentHidden[1, 1], 12);

        Assert.Equal(0.515550698433, run.Results[0].Outputs[0], 12);
        Assert.Equal(0.542510847774, run.Results[1].Outputs[0], 12);
        Assert.Equal(0.538225952973, run.Results[2].Outputs[0], 12);
        Assert.Equal(0.523232075003, run.Results[3].Outputs[0], 12);
    }
}
