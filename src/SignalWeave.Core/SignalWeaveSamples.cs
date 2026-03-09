namespace SignalWeave.Core;

public static class SignalWeaveSamples
{
    public const string DefaultFeedForwardConfig = """
name = Untitled
network = feedforward
inputs = 2
hidden = 2
outputs = 1
inputBias = true
hiddenBias = true
learningRate = 0.3
momentum = 0.8
randomWeightRange = 1.0
sigmoidPrimeOffset = 0.1
maxEpochs = 5000
errorThreshold = 0.02
update = pattern
cost = sse
""";

    public const string EmptyPatterns = "";

    public const string XorConfig = """
name = XOR demo
network = feedforward
inputs = 2
hidden = 4
outputs = 1
inputBias = true
hiddenBias = true
learningRate = 0.3
momentum = 0.8
randomWeightRange = 1.0
sigmoidPrimeOffset = 0.1
maxEpochs = 5000
errorThreshold = 0.02
update = pattern
cost = sse
""";

    public const string XorPatterns = """
zero-zero: 0 0 => 0
zero-one:  0 1 => 1
one-zero:  1 0 => 1
one-one:   1 1 => 0
""";

    public const string EchoSrnConfig = """
name = Echo SRN demo
network = srn
inputs = 1
hidden = 3
outputs = 1
inputBias = true
hiddenBias = true
learningRate = 0.45
momentum = 0.1
randomWeightRange = 0.4
sigmoidPrimeOffset = 0.1
maxEpochs = 3000
errorThreshold = 0.02
update = pattern
cost = sse
""";

    public const string EchoSrnPatterns = """
reset
seq-a-1: 0 => 0
seq-a-2: 1 => 0
seq-a-3: 0 => 1
seq-a-4: 1 => 0
reset
seq-b-1: 1 => 0
seq-b-2: 1 => 1
seq-b-3: 0 => 1
seq-b-4: 0 => 0
""";
}
