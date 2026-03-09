using System.Text.Json;
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
    public void ParsesTwoLayerFeedForwardConfig()
    {
        var definition = BasicPropNetworkConfigParser.Parse("""
            name = Two layer parser test
            network = feedforward
            inputs = 2
            hidden = 0
            outputs = 1
            inputBias = true
            """);

        Assert.Equal("Two layer parser test", definition.Name);
        Assert.Equal(NetworkKind.FeedForward, definition.NetworkKind);
        Assert.True(definition.IsDirectFeedForward);
        Assert.False(definition.HasHiddenLayer);
        Assert.Equal(2, definition.TotalLayerCount);
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
    public void SupportsFixedWeightForwardPassForTwoLayerFeedForward()
    {
        var definition = BasicPropNetworkConfigParser.Parse("""
            name = Two layer forward
            network = feedforward
            inputs = 2
            hidden = 0
            outputs = 1
            inputBias = true
            hiddenBias = false
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
                { 0.2 },
                { -0.4 },
                { 0.1 }
            },
            new double[0, 0]);

        var engine = new SignalWeaveEngine(definition, weights);
        var run = engine.TestAll(patterns);

        Assert.Equal(0.574442516812, run.Results[0].Outputs[0], 12);
        Assert.Empty(run.Results[0].HiddenActivations);
    }

    [Fact]
    public void MatchesBasicPropProbeForSingleStepTwoLayerFeedForwardTraining()
    {
        var definition = BasicPropNetworkConfigParser.Parse("""
            name = FF 2-layer train parity
            network = feedforward
            inputs = 2
            hidden = 0
            outputs = 1
            inputBias = true
            hiddenBias = false
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
                { 0.2 },
                { -0.4 },
                { 0.1 }
            },
            new double[0, 0]);

        var engine = new SignalWeaveEngine(definition, weights);
        engine.Train(patterns, 1);
        var run = engine.TestAll(patterns);
        var golden = LoadGoldenFixture("ff2-train-single.json");

        AssertRunMatchesGolden(run, golden);
        AssertMatrixMatches(engine.Weights.InputHidden, golden.InputHidden);
        Assert.Empty(engine.Weights.HiddenOutput);
    }

    [Fact]
    public void MatchesBasicPropProbeForSingleStepThreeLayerFeedForwardTraining()
    {
        var definition = BasicPropNetworkConfigParser.Parse("""
            name = FF 3-layer train parity
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
            a: 1 0 => 1
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
        engine.Train(patterns, 1);
        var run = engine.TestAll(patterns);
        var golden = LoadGoldenFixture("ff3-train-single.json");

        AssertRunMatchesGolden(run, golden);
        AssertMatrixMatches(engine.Weights.InputHidden, golden.InputHidden);
        AssertMatrixMatches(engine.Weights.HiddenOutput, golden.HiddenOutput!);
    }

    [Fact]
    public void MatchesBasicPropProbeForBatchThreeLayerFeedForwardTraining()
    {
        var definition = BasicPropNetworkConfigParser.Parse("""
            name = FF 3-layer batch parity
            network = feedforward
            inputs = 2
            hidden = 2
            outputs = 1
            inputBias = true
            hiddenBias = true
            learningRate = 0.3
            momentum = 0.8
            randomWeightRange = 1.0
            maxEpochs = 2
            errorThreshold = 0.0
            update = batch
            cost = sse
            """);

        var patterns = PatternSetParser.Parse("""
            a: 0 0 => 0
            b: 1 0 => 1
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
        engine.Train(patterns, 2);
        var run = engine.TestAll(patterns);
        var golden = LoadGoldenFixture("ff3-train-batch.json");

        AssertRunMatchesGolden(run, golden);
        AssertMatrixMatches(engine.Weights.InputHidden, golden.InputHidden);
        AssertMatrixMatches(engine.Weights.HiddenOutput, golden.HiddenOutput!);
    }

    [Fact]
    public void MatchesBasicPropProbeForSingleStepFourLayerFeedForwardTraining()
    {
        var definition = BasicPropNetworkConfigParser.Parse("""
            name = FF 4-layer train parity
            network = feedforward
            inputs = 2
            hidden = 2
            hidden2 = 2
            outputs = 1
            inputBias = true
            hiddenBias = true
            hidden2Bias = true
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
                { 0.3, 0.4 },
                { 0.05, -0.05 }
            },
            new double[,]
            {
                { 0.9 },
                { -1.0 },
                { 0.2 }
            },
            hiddenHidden: new double[,]
            {
                { 0.5, -0.6 },
                { 0.7, 0.8 },
                { 0.1, -0.2 }
            });

        var engine = new SignalWeaveEngine(definition, weights);
        engine.Train(patterns, 1);
        var run = engine.TestAll(patterns);
        var golden = LoadGoldenFixture("ff4-train-single.json");

        AssertRunMatchesGolden(run, golden);
        AssertMatrixMatches(engine.Weights.InputHidden, golden.InputHidden);
        AssertMatrixMatches(engine.Weights.HiddenHidden!, golden.HiddenHidden!);
        AssertMatrixMatches(engine.Weights.HiddenOutput, golden.HiddenOutput!);
    }

    [Fact]
    public void MatchesBasicPropProbeForCrossEntropyThreeLayerFeedForwardTraining()
    {
        var definition = BasicPropNetworkConfigParser.Parse("""
            name = FF 3-layer cross-entropy parity
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
            cost = crossEntropy
            """);

        var patterns = PatternSetParser.Parse("""
            a: 1,0 => 1
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
        var result = engine.Train(patterns, 1);
        var run = engine.TestAll(patterns);
        var golden = LoadGoldenFixture("ff3-train-cross-entropy.json");

        Assert.Single(result.History);
        AssertRunMatchesGolden(run, golden);
        AssertMatrixMatches(engine.Weights.InputHidden, golden.InputHidden);
        AssertMatrixMatches(engine.Weights.HiddenOutput, golden.HiddenOutput!);
    }

    [Fact]
    public void MatchesBasicPropProbeForMomentumThreeLayerFeedForwardTraining()
    {
        var definition = BasicPropNetworkConfigParser.Parse("""
            name = FF 3-layer momentum parity
            network = feedforward
            inputs = 2
            hidden = 2
            outputs = 1
            inputBias = true
            hiddenBias = true
            learningRate = 0.3
            momentum = 0.8
            randomWeightRange = 1.0
            maxEpochs = 2
            errorThreshold = 0.0
            update = pattern
            cost = sse
            """);

        var patterns = PatternSetParser.Parse("""
            a: 1,0 => 1
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
        var result = engine.Train(patterns, 2);
        var run = engine.TestAll(patterns);
        var golden = LoadGoldenFixture("ff3-train-momentum.json");

        Assert.Equal(2, result.History.Count);
        AssertRunMatchesGolden(run, golden);
        AssertMatrixMatches(engine.Weights.InputHidden, golden.InputHidden);
        AssertMatrixMatches(engine.Weights.HiddenOutput, golden.HiddenOutput!);
    }

    [Fact]
    public void MatchesBasicPropProbeForFeedForwardStopRule()
    {
        var definition = BasicPropNetworkConfigParser.Parse("""
            name = FF stop-rule parity
            network = feedforward
            inputs = 1
            hidden = 1
            outputs = 1
            inputBias = true
            hiddenBias = true
            learningRate = 0.3
            momentum = 0.8
            randomWeightRange = 1.0
            maxEpochs = 50
            errorThreshold = 0.0
            update = pattern
            cost = sse
            """);

        var patterns = PatternSetParser.Parse("""
            a: 1 => 1
            """);

        var weights = new WeightSet(
            new double[,]
            {
                { 5.0 },
                { 5.0 }
            },
            new double[,]
            {
                { 5.0 },
                { 5.0 }
            });

        var engine = new SignalWeaveEngine(definition, weights);
        var result = engine.Train(patterns, 50);
        var run = engine.TestAll(patterns);
        var golden = LoadGoldenFixture("ff3-stop-rule.json");

        Assert.Equal(golden.CyclesCompleted, result.History.Count);
        AssertRunMatchesGolden(run, golden);
        AssertMatrixMatches(engine.Weights.InputHidden, golden.InputHidden);
        AssertMatrixMatches(engine.Weights.HiddenOutput, golden.HiddenOutput!);
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
        var golden = LoadGoldenFixture("srn-forward.json");

        AssertRunMatchesGolden(run, golden);
        AssertMatrixMatches(engine.Weights.InputHidden, golden.InputHidden);
        AssertMatrixMatches(engine.Weights.HiddenOutput, golden.HiddenOutput!);
        AssertMatrixMatches(engine.Weights.RecurrentHidden!, golden.RecurrentHidden!);
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
        var golden = LoadGoldenFixture("srn-train-sequential.json");

        Assert.Equal(4, result.History.Count);
        AssertRunMatchesGolden(run, golden);
        AssertMatrixMatches(engine.Weights.InputHidden, golden.InputHidden);
        AssertMatrixMatches(engine.Weights.HiddenOutput, golden.HiddenOutput!);
        AssertMatrixMatches(engine.Weights.RecurrentHidden!, golden.RecurrentHidden!);
    }

    [Fact]
    public void MatchesBasicPropProbeForBatchSrnTraining()
    {
        var definition = BasicPropNetworkConfigParser.Parse("""
            name = SRN train batch parity
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
            update = batch
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
        var golden = LoadGoldenFixture("srn-train-batch.json");

        Assert.Equal(4, result.History.Count);
        AssertRunMatchesGolden(run, golden);
        AssertMatrixMatches(engine.Weights.InputHidden, golden.InputHidden);
        AssertMatrixMatches(engine.Weights.HiddenOutput, golden.HiddenOutput!);
        AssertMatrixMatches(engine.Weights.RecurrentHidden!, golden.RecurrentHidden!);
    }

    private static BasicPropGoldenFixture LoadGoldenFixture(string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "Fixtures",
            "BasicProp",
            fileName));

        return JsonSerializer.Deserialize<BasicPropGoldenFixture>(
            File.ReadAllText(path),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })
            ?? throw new InvalidOperationException($"Failed to load golden fixture '{fileName}'.");
    }

    private static void AssertRunMatchesGolden(RunResult run, BasicPropGoldenFixture golden)
    {
        Assert.Equal(golden.AverageError, run.AverageError, 12);
        Assert.Equal(golden.Outputs.Length, run.Results.Count);

        for (var index = 0; index < golden.Outputs.Length; index++)
        {
            for (var output = 0; output < golden.Outputs[index].Length; output++)
            {
                Assert.Equal(golden.Outputs[index][output], run.Results[index].Outputs[output], 12);
            }

            if (golden.HiddenActivations is null)
            {
                continue;
            }

            Assert.Equal(golden.HiddenActivations[index].Length, run.Results[index].HiddenActivations.Length);
            for (var hidden = 0; hidden < golden.HiddenActivations[index].Length; hidden++)
            {
                Assert.Equal(golden.HiddenActivations[index][hidden], run.Results[index].HiddenActivations[hidden], 12);
            }
        }
    }

    private static void AssertMatrixMatches(double[,] actual, double[][] expected)
    {
        Assert.Equal(expected.Length, actual.GetLength(0));
        Assert.Equal(expected.Length == 0 ? 0 : expected[0].Length, actual.GetLength(1));

        for (var row = 0; row < expected.Length; row++)
        {
            for (var column = 0; column < expected[row].Length; column++)
            {
                Assert.Equal(expected[row][column], actual[row, column], 12);
            }
        }
    }

    private sealed class BasicPropGoldenFixture
    {
        public int? CyclesCompleted { get; set; }
        public double AverageError { get; set; }
        public double[][] Outputs { get; set; } = [];
        public double[][]? HiddenActivations { get; set; }
        public double[][] InputHidden { get; set; } = [];
        public double[][]? HiddenHidden { get; set; }
        public double[][]? HiddenOutput { get; set; }
        public double[][]? RecurrentHidden { get; set; }
    }
}
