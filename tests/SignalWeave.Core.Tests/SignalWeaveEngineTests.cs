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

        Assert.True(result.FinalPoint.AverageError < 0.03);

        var run = engine.TestAll(patterns);
        Assert.True(run.Results[0].Outputs[0] < 0.2);
        Assert.True(run.Results[1].Outputs[0] < 0.2);
        Assert.True(run.Results[2].Outputs[0] < 0.2);
        Assert.True(run.Results[3].Outputs[0] > 0.8);
    }
}
