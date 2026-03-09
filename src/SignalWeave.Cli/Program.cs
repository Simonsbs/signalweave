using SignalWeave.Core;

if (args.Length == 0 || IsHelp(args[0]))
{
    PrintHelp();
    return;
}

try
{
    var command = args[0].ToLowerInvariant();
    var options = ParseOptions(args.Skip(1).ToArray());

    switch (command)
    {
        case "summary":
            PrintSummary(options);
            break;
        case "train":
            Train(options);
            break;
        case "test-all":
            TestAll(options);
            break;
        case "cluster":
            Cluster(options);
            break;
        default:
            throw new InvalidOperationException($"Unknown command '{command}'.");
    }
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    Environment.ExitCode = 1;
}

static void PrintHelp()
{
    Console.WriteLine("""
SignalWeave CLI

Commands:
  summary   --network <file> --patterns <file>
  train     --network <file> --patterns <file> --weights <file> [--seed <int>] [--epochs <int>]
  test-all  --network <file> --patterns <file> --weights <file>
  cluster   --network <file> --patterns <file> --weights <file> [--mode outputs|hidden]
""");
}

static void PrintSummary(Dictionary<string, string> options)
{
    var definition = LoadDefinition(options);
    var patterns = LoadPatterns(options);
    Console.WriteLine(definition.ToSummary());
    Console.WriteLine(patterns.ToSummary());
    Console.WriteLine();
    Console.WriteLine(CompatibilityProfile.ToDisplayText());
}

static void Train(Dictionary<string, string> options)
{
    var definition = LoadDefinition(options);
    var patterns = LoadPatterns(options);
    var seed = options.TryGetValue("seed", out var seedText) ? int.Parse(seedText) : (int?)null;
    var epochs = options.TryGetValue("epochs", out var epochText) ? int.Parse(epochText) : (int?)null;

    var engine = new SignalWeaveEngine(definition, seed: seed);
    var result = engine.Train(patterns, epochs);

    if (options.TryGetValue("weights", out var weightsPath))
    {
        WeightSetSerializer.SaveFile(weightsPath, definition, result.Weights);
    }

    Console.WriteLine($"Epochs: {result.History.Count}");
    Console.WriteLine($"Final error: {result.FinalPoint.AverageError:0.000}");
    Console.WriteLine(result.FinalRun.ToTable());
}

static void TestAll(Dictionary<string, string> options)
{
    var definition = LoadDefinition(options);
    var patterns = LoadPatterns(options);
    var weights = LoadWeights(options);
    var engine = new SignalWeaveEngine(definition, weights);
    Console.WriteLine(engine.TestAll(patterns).ToTable());
}

static void Cluster(Dictionary<string, string> options)
{
    var definition = LoadDefinition(options);
    var patterns = LoadPatterns(options);
    var weights = LoadWeights(options);
    var engine = new SignalWeaveEngine(definition, weights);
    var mode = options.TryGetValue("mode", out var value) ? value.ToLowerInvariant() : "outputs";
    var cluster = mode == "hidden" ? engine.ClusterHiddenStates(patterns) : engine.ClusterOutputs(patterns);
    Console.WriteLine(cluster.ToDisplayText());
}

static NetworkDefinition LoadDefinition(Dictionary<string, string> options)
{
    return BasicPropNetworkConfigParser.ParseFile(GetRequired(options, "network"));
}

static PatternSet LoadPatterns(Dictionary<string, string> options)
{
    return PatternSetParser.ParseFile(GetRequired(options, "patterns"));
}

static WeightSet LoadWeights(Dictionary<string, string> options)
{
    return WeightSetSerializer.LoadFile(GetRequired(options, "weights"));
}

static string GetRequired(Dictionary<string, string> options, string key)
{
    if (!options.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Missing required option --{key}.");
    }

    return value;
}

static Dictionary<string, string> ParseOptions(string[] args)
{
    var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    for (var index = 0; index < args.Length; index++)
    {
        var token = args[index];
        if (!token.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var key = token[2..];
        var value = index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal)
            ? args[++index]
            : "true";
        options[key] = value;
    }

    return options;
}

static bool IsHelp(string token)
{
    return token is "-h" or "--help" or "help";
}
