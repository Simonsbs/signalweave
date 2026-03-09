using System.Globalization;

namespace SignalWeave.Core;

public static class BasicPropNetworkConfigParser
{
    public static NetworkDefinition ParseFile(string path)
    {
        return Parse(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));
    }

    public static NetworkDefinition Parse(string text, string? fallbackName = null)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = StripComment(rawLine).Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('['))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex < 0)
            {
                separatorIndex = line.IndexOf(':');
            }

            if (separatorIndex < 0)
            {
                continue;
            }

            var key = NormalizeKey(line[..separatorIndex]);
            var value = line[(separatorIndex + 1)..].Trim();
            values[key] = value;
        }

        var definition = new NetworkDefinition
        {
            Name = ReadString(values, ["name", "title"], fallbackName ?? "Untitled"),
            NetworkKind = ReadNetworkKind(values, ["networkkind", "networktype", "network", "type"]),
            InputUnits = ReadInt(values, ["inputunits", "inputs", "input"]),
            HiddenUnits = ReadInt(values, ["hiddenunits", "hidden", "hiddenlayer"]),
            OutputUnits = ReadInt(values, ["outputunits", "outputs", "output"]),
            UseInputBias = ReadBool(values, ["useinputbias", "inputbias", "biasinput"], true),
            UseHiddenBias = ReadBool(values, ["usehiddenbias", "hiddenbias", "biashidden"], true),
            LearningRate = ReadDouble(values, ["learningrate", "eta"], 0.3),
            Momentum = ReadDouble(values, ["momentum", "alpha"], 0.0),
            RandomWeightRange = ReadDouble(values, ["randomweightrange", "randomrange", "weightinitrange"], 0.5),
            SigmoidPrimeOffset = ReadDouble(values, ["sigmoidprimeoffset", "sigmoidoffset", "primeoffset"], 0.1),
            MaxEpochs = ReadInt(values, ["maxepochs", "epochs", "iterations"], 1000),
            ErrorThreshold = ReadDouble(values, ["errorthreshold", "threshold", "stoppingerror"], 0.01),
            UpdateMode = ReadUpdateMode(values, ["updatemode", "update", "learningmode"], UpdateMode.Pattern),
            CostFunction = ReadCostFunction(values, ["costfunction", "cost", "errorfunction"], CostFunction.SumSquaredError)
        };

        definition.Validate();
        return definition;
    }

    private static string NormalizeKey(string value)
    {
        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private static string StripComment(string line)
    {
        foreach (var token in new[] { "#", ";", "//" })
        {
            var index = line.IndexOf(token, StringComparison.Ordinal);
            if (index >= 0)
            {
                return line[..index];
            }
        }

        return line;
    }

    private static string ReadString(Dictionary<string, string> values, string[] keys, string defaultValue)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value))
            {
                return value.Trim();
            }
        }

        return defaultValue;
    }

    private static int ReadInt(Dictionary<string, string> values, string[] keys, int? defaultValue = null)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) &&
                int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        if (defaultValue.HasValue)
        {
            return defaultValue.Value;
        }

        throw new InvalidOperationException($"Missing required integer config key. Expected one of: {string.Join(", ", keys)}");
    }

    private static double ReadDouble(Dictionary<string, string> values, string[] keys, double defaultValue)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) &&
                double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return defaultValue;
    }

    private static bool ReadBool(Dictionary<string, string> values, string[] keys, bool defaultValue)
    {
        foreach (var key in keys)
        {
            if (!values.TryGetValue(key, out var value))
            {
                continue;
            }

            value = value.Trim().ToLowerInvariant();
            return value switch
            {
                "1" or "true" or "yes" or "on" => true,
                "0" or "false" or "no" or "off" => false,
                _ => defaultValue
            };
        }

        return defaultValue;
    }

    private static NetworkKind ReadNetworkKind(Dictionary<string, string> values, string[] keys)
    {
        foreach (var key in keys)
        {
            if (!values.TryGetValue(key, out var value))
            {
                continue;
            }

            value = value.Trim().ToLowerInvariant();
            return value switch
            {
                "feedforward" or "feed-forward" or "ff" => NetworkKind.FeedForward,
                "srn" or "recurrent" or "simple-recurrent" or "simple_recurrent" => NetworkKind.SimpleRecurrent,
                _ => throw new InvalidOperationException($"Unknown network type '{value}'.")
            };
        }

        return NetworkKind.FeedForward;
    }

    private static UpdateMode ReadUpdateMode(Dictionary<string, string> values, string[] keys, UpdateMode defaultValue)
    {
        foreach (var key in keys)
        {
            if (!values.TryGetValue(key, out var value))
            {
                continue;
            }

            value = value.Trim().ToLowerInvariant();
            return value switch
            {
                "pattern" or "online" or "stochastic" => UpdateMode.Pattern,
                "batch" => UpdateMode.Batch,
                _ => defaultValue
            };
        }

        return defaultValue;
    }

    private static CostFunction ReadCostFunction(Dictionary<string, string> values, string[] keys, CostFunction defaultValue)
    {
        foreach (var key in keys)
        {
            if (!values.TryGetValue(key, out var value))
            {
                continue;
            }

            value = value.Trim().ToLowerInvariant();
            return value switch
            {
                "sse" or "sumsquarederror" or "squared" => CostFunction.SumSquaredError,
                "crossentropy" or "cross-entropy" or "ce" => CostFunction.CrossEntropy,
                _ => defaultValue
            };
        }

        return defaultValue;
    }
}

public static class PatternSetParser
{
    public static PatternSet ParseFile(string path)
    {
        return Parse(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));
    }

    public static PatternSet Parse(string text, string? name = null)
    {
        var examples = new List<PatternExample>();
        var startsSequence = true;
        var index = 1;

        foreach (var rawLine in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = StripComment(rawLine).Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (string.Equals(line, "reset", StringComparison.OrdinalIgnoreCase))
            {
                startsSequence = true;
                continue;
            }

            var label = $"{name ?? "pattern"}-{index}";
            var body = line;
            var labelSeparator = line.IndexOf(':');

            if (labelSeparator > -1 && line.IndexOf("=>", StringComparison.Ordinal) > labelSeparator)
            {
                label = line[..labelSeparator].Trim();
                body = line[(labelSeparator + 1)..].Trim();
            }

            var splitToken = body.Contains("=>", StringComparison.Ordinal)
                ? "=>"
                : body.Contains('|', StringComparison.Ordinal)
                    ? "|"
                    : body.Contains("->", StringComparison.Ordinal)
                        ? "->"
                        : string.Empty;

            var inputsText = body;
            string? targetsText = null;

            if (!string.IsNullOrWhiteSpace(splitToken))
            {
                var parts = body.Split(splitToken, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                inputsText = parts[0];
                targetsText = parts.Length > 1 ? parts[1] : null;
            }

            var example = new PatternExample(
                label,
                ParseVector(inputsText),
                targetsText is null ? null : ParseVector(targetsText),
                startsSequence);

            examples.Add(example);
            startsSequence = false;
            index++;
        }

        return new PatternSet(examples);
    }

    private static string StripComment(string line)
    {
        foreach (var token in new[] { "#", ";", "//" })
        {
            var index = line.IndexOf(token, StringComparison.Ordinal);
            if (index >= 0)
            {
                return line[..index];
            }
        }

        return line;
    }

    private static double[] ParseVector(string text)
    {
        return text
            .Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => double.Parse(value, CultureInfo.InvariantCulture))
            .ToArray();
    }
}
