using System.Text.Json;

namespace SignalWeave.Core;

public sealed class WeightSet
{
    public WeightSet(double[,] inputHidden, double[,] hiddenOutput, double[,]? recurrentHidden = null, double[,]? hiddenHidden = null)
    {
        InputHidden = inputHidden;
        HiddenOutput = hiddenOutput;
        RecurrentHidden = recurrentHidden;
        HiddenHidden = hiddenHidden;
    }

    public double[,] InputHidden { get; }
    public double[,] HiddenOutput { get; }
    public double[,]? RecurrentHidden { get; }
    public double[,]? HiddenHidden { get; }

    public WeightSet Clone()
    {
        return new WeightSet(
            (double[,])InputHidden.Clone(),
            (double[,])HiddenOutput.Clone(),
            RecurrentHidden is null ? null : (double[,])RecurrentHidden.Clone(),
            HiddenHidden is null ? null : (double[,])HiddenHidden.Clone());
    }

    public static WeightSet CreateRandom(NetworkDefinition definition, int? seed = null)
    {
        var random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        var inputHidden = CreateMatrix(
            definition.InputUnits + (definition.UseInputBias ? 1 : 0),
            definition.IsDirectFeedForward ? definition.OutputUnits : definition.HiddenUnits,
            definition.RandomWeightRange,
            random);
        var hiddenHidden = definition.HasSecondHiddenLayer
            ? CreateMatrix(definition.HiddenUnits + (definition.UseHiddenBias ? 1 : 0), definition.SecondHiddenUnits, definition.RandomWeightRange, random)
            : null;
        var hiddenOutput = CreateMatrix(
            definition.IsDirectFeedForward
                ? 0
                : definition.HasSecondHiddenLayer
                ? definition.SecondHiddenUnits + (definition.UseSecondHiddenBias ? 1 : 0)
                : definition.HiddenUnits + (definition.UseHiddenBias ? 1 : 0),
            definition.IsDirectFeedForward ? 0 : definition.OutputUnits,
            definition.RandomWeightRange,
            random);
        var recurrent = definition.NetworkKind == NetworkKind.SimpleRecurrent
            ? CreateMatrix(definition.HiddenUnits, definition.HiddenUnits, definition.RandomWeightRange, random)
            : null;

        return new WeightSet(inputHidden, hiddenOutput, recurrent, hiddenHidden);
    }

    private static double[,] CreateMatrix(int rows, int columns, double range, Random random)
    {
        var matrix = new double[rows, columns];

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                matrix[row, column] = (random.NextDouble() * 2.0 - 1.0) * range;
            }
        }

        return matrix;
    }
}

public static class WeightSetSerializer
{
    public static void SaveFile(string path, NetworkDefinition definition, WeightSet weights)
    {
        var mappedWeights = WeightSetDocumentMapper.ToDocument(weights);
        var document = new WeightDocument
        {
            Name = definition.Name,
            NetworkKind = definition.NetworkKind.ToString(),
            InputHidden = mappedWeights.InputHidden,
            HiddenOutput = mappedWeights.HiddenOutput,
            RecurrentHidden = mappedWeights.RecurrentHidden,
            HiddenHidden = mappedWeights.HiddenHidden
        };

        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public static WeightSet LoadFile(string path)
    {
        var json = File.ReadAllText(path);
        var document = JsonSerializer.Deserialize<WeightDocument>(json)
            ?? throw new InvalidOperationException("Weight file is empty or invalid.");

        return WeightSetDocumentMapper.FromDocument(document);
    }
}
