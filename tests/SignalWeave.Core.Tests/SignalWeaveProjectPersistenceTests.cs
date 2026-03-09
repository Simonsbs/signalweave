using SignalWeave.Core;

namespace SignalWeave.Core.Tests;

public sealed class SignalWeaveProjectPersistenceTests
{
    [Fact]
    public void ProjectRoundTripsDefinitionPatternsAndWeights()
    {
        var definition = BasicPropNetworkConfigParser.Parse("""
            name = Project roundtrip
            network = feedforward
            inputs = 2
            hidden = 3
            outputs = 1
            inputBias = true
            hiddenBias = true
            learningRate = 0.3
            momentum = 0.8
            update = pattern
            cost = sse
            """);
        var patterns = PatternSetParser.Parse("""
            a: 0 0 => 0
            b: 0 1 => 1
            reset
            c: 1 0 => 1
            """);
        var weights = new WeightSet(
            new double[,]
            {
                { 0.1, 0.2, 0.3 },
                { 0.4, 0.5, 0.6 },
                { 0.7, 0.8, 0.9 }
            },
            new double[,]
            {
                { 1.0 },
                { 1.1 },
                { 1.2 },
                { 1.3 }
            });

        var path = Path.Combine(Path.GetTempPath(), $"signalweave-project-{Guid.NewGuid():N}.swproj.json");

        try
        {
            SignalWeaveProjectSerializer.SaveFile(path, definition, patterns, weights);
            var project = SignalWeaveProjectSerializer.LoadFile(path);

            Assert.Equal(definition.Name, project.Definition.Name);
            Assert.Equal(definition.NetworkKind, project.Definition.NetworkKind);
            Assert.Equal(patterns.Examples.Count, project.Patterns.Examples.Count);
            Assert.Equal(patterns.Examples[1].Label, project.Patterns.Examples[1].Label);
            Assert.True(project.Patterns.Examples[1].ResetsContextAfter);
            Assert.NotNull(project.Weights);
            Assert.Equal(0.8, project.Weights!.InputHidden[2, 1], 12);
            Assert.Equal(1.3, project.Weights.HiddenOutput[3, 0], 12);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void CheckpointRoundTripsWeightsPatternsAndCycles()
    {
        var definition = BasicPropNetworkConfigParser.Parse("""
            name = Checkpoint roundtrip
            network = srn
            inputs = 1
            hidden = 2
            outputs = 1
            inputBias = true
            hiddenBias = true
            learningRate = 0.3
            momentum = 0.8
            update = batch
            cost = sse
            """);
        var patterns = PatternSetParser.Parse("""
            a: 0 => 0
            b: 1 => 1
            reset
            c: 0 => 1
            """);
        var weights = new WeightSet(
            new double[,]
            {
                { 0.1, 0.2 },
                { 0.3, 0.4 }
            },
            new double[,]
            {
                { 0.5 },
                { 0.6 },
                { 0.7 }
            },
            new double[,]
            {
                { 0.8, 0.9 },
                { 1.0, 1.1 }
            });

        var savedAt = new DateTimeOffset(2026, 3, 9, 12, 0, 0, TimeSpan.Zero);
        var path = Path.Combine(Path.GetTempPath(), $"signalweave-checkpoint-{Guid.NewGuid():N}.swcheckpoint.json");

        try
        {
            SignalWeaveCheckpointSerializer.SaveFile(path, definition, patterns, weights, 42, savedAt);
            var checkpoint = SignalWeaveCheckpointSerializer.LoadFile(path);

            Assert.Equal(42, checkpoint.CompletedCycles);
            Assert.Equal(savedAt, checkpoint.SavedAtUtc);
            Assert.Equal(definition.Name, checkpoint.Definition.Name);
            Assert.Equal(patterns.Examples.Count, checkpoint.Patterns.Examples.Count);
            Assert.True(checkpoint.Patterns.Examples[1].ResetsContextAfter);
            Assert.Equal(0.4, checkpoint.Weights.InputHidden[1, 1], 12);
            Assert.Equal(1.1, checkpoint.Weights.RecurrentHidden![1, 1], 12);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
