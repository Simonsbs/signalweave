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
        Assert.Equal(2, definition.OutputUnits);
        Assert.Equal(UpdateMode.Batch, definition.UpdateMode);
        Assert.Equal(CostFunction.CrossEntropy, definition.CostFunction);
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
        Assert.True(patterns.Examples[0].StartsSequence);
        Assert.False(patterns.Examples[1].StartsSequence);
        Assert.True(patterns.Examples[2].StartsSequence);
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
}
