import basicProp.Controller;
import basicProp.FeedForwardNetwork;
import basicProp.FeedForwardSpec;
import basicProp.NetSpec;
import basicProp.Network;
import basicProp.PatternConfig;
import basicProp.SimControl;
import basicProp.SpecStub;
import basicProp.SrnNetwork;
import basicProp.SrnSpec;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.Locale;
import java.util.Objects;

public final class BasicPropProbe {
    private BasicPropProbe() {
    }

    public static void main(String[] args) throws Exception {
        System.setProperty("java.awt.headless", "true");

        if (args.length == 0 || "help".equalsIgnoreCase(args[0]) || "--help".equalsIgnoreCase(args[0])) {
            printHelp();
            return;
        }

        switch (args[0].toLowerCase(Locale.ROOT)) {
            case "defaults" -> printDefaults();
            case "run" -> {
                if (args.length < 2) {
                    throw new IllegalArgumentException("Missing probe file path for run command.");
                }

                runExperiment(Path.of(args[1]));
            }
            default -> throw new IllegalArgumentException("Unknown command: " + args[0]);
        }
    }

    private static void printHelp() {
        System.out.println("""
                BasicPropProbe

                Commands:
                  defaults
                  run <probe-file>
                """);
    }

    private static void printDefaults() throws Exception {
        new Controller();
        SimControl control = Controller.ui.control;

        StringBuilder builder = new StringBuilder();
        builder.append("{\n");
        writeJsonField(builder, "learningRateOptions", comboValues(control.lRateComboBox), 1, true);
        writeJsonField(builder, "learningRateDefault", control.lRateComboBox.getSelectedItem().toString(), 1, true);
        writeJsonField(builder, "momentumOptions", comboValues(control.momentumComboBox), 1, true);
        writeJsonField(builder, "momentumDefault", control.momentumComboBox.getSelectedItem().toString(), 1, true);
        writeJsonField(builder, "learningStepOptions", comboValues(control.cyclesComboBox), 1, true);
        writeJsonField(builder, "learningStepsDefault", control.cyclesComboBox.getSelectedItem().toString(), 1, true);
        writeJsonField(builder, "weightRangeOptions", comboValues(control.wtRangeComboBox), 1, true);
        writeJsonField(builder, "weightRangeDefault", control.wtRangeComboBox.getSelectedItem().toString(), 1, true);
        writeJsonField(builder, "defaultNetworkSpec", Controller.spec.getSpecStub().toString(), 1, false);
        builder.append("}\n");
        System.out.print(builder);
    }

    private static void runExperiment(Path probeFile) throws Exception {
        Experiment experiment = Experiment.parse(probeFile);
        new Controller();

        NetSpec spec = createSpec(experiment);
        Controller.spec = spec;
        Controller.net = createNetwork(spec, experiment.type);
        Controller.spec.batchUpdate = experiment.batchUpdate;
        Controller.spec.crossEntropy = experiment.crossEntropy;

        if (spec instanceof SrnSpec srnSpec) {
            srnSpec.sequentialUpdate = experiment.sequentialUpdate;
        }

        PatternConfig patternConfig = buildPatternConfig(experiment);
        Controller.net.setPatterns(patternConfig);

        if (!experiment.weights.isEmpty()) {
            applyFeedForwardWeights(spec, experiment);
        }

        if (spec instanceof SrnSpec srnSpec && experiment.recurrentWeights != null) {
            applyRecurrentWeights(srnSpec, experiment.recurrentWeights);
        }

        if (experiment.steps > 0) {
            Controller.net.train(spec, experiment.steps, experiment.learningRate, experiment.momentum, false);
        }

        double testAllError = Controller.net.testAll(spec);
        double[][] outputs = Controller.net.getOutputsAsList();
        double[][] hiddenActivations;

        try {
            hiddenActivations = Controller.net.getHiddenActs();
        } catch (RuntimeException exception) {
            hiddenActivations = null;
        }

        StringBuilder builder = new StringBuilder();
        builder.append("{\n");
        writeJsonField(builder, "probeFile", probeFile.toAbsolutePath().toString(), 1, true);
        writeJsonField(builder, "type", experiment.type, 1, true);
        writeJsonField(builder, "layers", experiment.layers, 1, true);
        writeJsonField(builder, "biases", experiment.biases, 1, true);
        writeJsonField(builder, "steps", experiment.steps, 1, true);
        writeJsonField(builder, "learningRate", experiment.learningRate, 1, true);
        writeJsonField(builder, "momentum", experiment.momentum, 1, true);
        writeJsonField(builder, "batchUpdate", experiment.batchUpdate, 1, true);
        writeJsonField(builder, "crossEntropy", experiment.crossEntropy, 1, true);
        writeJsonField(builder, "cyclesCompleted", spec.getCycles(), 1, true);
        writeJsonField(builder, "testAllError", testAllError, 1, true);
        writeJsonField(builder, "outputs", outputs, 1, true);
        writeJsonField(builder, "hiddenActivations", hiddenActivations, 1, true);
        writeJsonField(builder, "weights", spec.getWeights(), 1, spec instanceof SrnSpec);

        if (spec instanceof SrnSpec srnSpec) {
            writeJsonField(builder, "recurrentWeights", srnSpec.getRecWeights(), 1, false);
        }

        builder.append("}\n");
        System.out.print(builder);
    }

    private static NetSpec createSpec(Experiment experiment) {
        SpecStub stub = new SpecStub();
        stub.setType(experiment.type);
        stub.setNumLayers(experiment.layers.length);
        stub.setUnitCounts(experiment.layers.clone());
        stub.setBiases(experiment.biases.clone());

        NetSpec spec = "SRN".equalsIgnoreCase(experiment.type) ? new SrnSpec() : new FeedForwardSpec();
        spec.init(stub);
        return spec;
    }

    private static Network createNetwork(NetSpec spec, String type) {
        return "SRN".equalsIgnoreCase(type) ? new SrnNetwork(spec) : new FeedForwardNetwork(spec);
    }

    private static PatternConfig buildPatternConfig(Experiment experiment) {
        PatternConfig patternConfig = new PatternConfig(experiment.layers[0], experiment.layers[experiment.layers.length - 1]);
        patternConfig.setNumberOfPatterns(experiment.patterns.size());
        patternConfig.setPatternFileName(experiment.name != null ? experiment.name : "probe");

        for (int index = 0; index < experiment.patterns.size(); index++) {
            PatternLine pattern = experiment.patterns.get(index);
            double[] combined = new double[pattern.inputs.length + pattern.targets.length];
            System.arraycopy(pattern.inputs, 0, combined, 0, pattern.inputs.length);
            System.arraycopy(pattern.targets, 0, combined, pattern.inputs.length, pattern.targets.length);
            patternConfig.addPattern(index, combined);

            if (pattern.startsSequence) {
                patternConfig.addReset(index);
            }
        }

        return patternConfig;
    }

    private static void applyFeedForwardWeights(NetSpec spec, Experiment experiment) {
        for (int layer = 0; layer < experiment.weights.size(); layer++) {
            double[][] layerWeights = experiment.weights.get(layer);
            if (layerWeights == null) {
                continue;
            }

            for (int toUnit = 0; toUnit < layerWeights.length; toUnit++) {
                for (int fromUnit = 0; fromUnit < layerWeights[toUnit].length; fromUnit++) {
                    spec.setWeight(layer, toUnit + 1, fromUnit, layerWeights[toUnit][fromUnit]);
                }
            }
        }
    }

    private static void applyRecurrentWeights(SrnSpec spec, double[][] recurrentWeights) {
        for (int hidden = 0; hidden < recurrentWeights.length; hidden++) {
            for (int previous = 0; previous < recurrentWeights[hidden].length; previous++) {
                spec.setRecWeight(hidden, previous, recurrentWeights[hidden][previous]);
            }
        }
    }

    private static List<String> comboValues(javax.swing.JComboBox<?> comboBox) {
        List<String> values = new ArrayList<>();
        for (int index = 0; index < comboBox.getItemCount(); index++) {
            values.add(Objects.toString(comboBox.getItemAt(index)));
        }

        return values;
    }

    private static void writeJsonField(StringBuilder builder, String name, Object value, int indent, boolean trailingComma) {
        indent(builder, indent);
        builder.append('"').append(escape(name)).append('"').append(": ");
        appendJsonValue(builder, value, indent);
        if (trailingComma) {
            builder.append(',');
        }
        builder.append('\n');
    }

    private static void appendJsonValue(StringBuilder builder, Object value, int indent) {
        if (value == null) {
            builder.append("null");
            return;
        }

        if (value instanceof String stringValue) {
            builder.append('"').append(escape(stringValue)).append('"');
            return;
        }

        if (value instanceof Boolean || value instanceof Integer || value instanceof Long) {
            builder.append(value);
            return;
        }

        if (value instanceof Double doubleValue) {
            builder.append(formatNumber(doubleValue));
            return;
        }

        if (value instanceof int[] ints) {
            builder.append('[');
            for (int index = 0; index < ints.length; index++) {
                if (index > 0) {
                    builder.append(", ");
                }
                builder.append(ints[index]);
            }
            builder.append(']');
            return;
        }

        if (value instanceof boolean[] booleans) {
            builder.append('[');
            for (int index = 0; index < booleans.length; index++) {
                if (index > 0) {
                    builder.append(", ");
                }
                builder.append(booleans[index]);
            }
            builder.append(']');
            return;
        }

        if (value instanceof double[] doubles) {
            builder.append('[');
            for (int index = 0; index < doubles.length; index++) {
                if (index > 0) {
                    builder.append(", ");
                }
                builder.append(formatNumber(doubles[index]));
            }
            builder.append(']');
            return;
        }

        if (value instanceof double[][] matrix) {
            builder.append("[\n");
            for (int row = 0; row < matrix.length; row++) {
                indent(builder, indent + 1);
                appendJsonValue(builder, matrix[row], indent + 1);
                if (row < matrix.length - 1) {
                    builder.append(',');
                }
                builder.append('\n');
            }
            indent(builder, indent);
            builder.append(']');
            return;
        }

        if (value instanceof double[][][] cube) {
            builder.append("[\n");
            for (int layer = 0; layer < cube.length; layer++) {
                indent(builder, indent + 1);
                appendJsonValue(builder, cube[layer], indent + 1);
                if (layer < cube.length - 1) {
                    builder.append(',');
                }
                builder.append('\n');
            }
            indent(builder, indent);
            builder.append(']');
            return;
        }

        if (value instanceof List<?> list) {
            builder.append('[');
            for (int index = 0; index < list.size(); index++) {
                if (index > 0) {
                    builder.append(", ");
                }
                appendJsonValue(builder, list.get(index), indent + 1);
            }
            builder.append(']');
            return;
        }

        builder.append('"').append(escape(String.valueOf(value))).append('"');
    }

    private static String formatNumber(double value) {
        return String.format(Locale.ROOT, "%.12f", value).replaceAll("0+$", "").replaceAll("\\.$", ".0");
    }

    private static void indent(StringBuilder builder, int indent) {
        builder.append("  ".repeat(Math.max(0, indent)));
    }

    private static String escape(String value) {
        return value.replace("\\", "\\\\").replace("\"", "\\\"").replace("\n", "\\n");
    }

    private static final class PatternLine {
        private final double[] inputs;
        private final double[] targets;
        private final boolean startsSequence;

        private PatternLine(double[] inputs, double[] targets, boolean startsSequence) {
            this.inputs = inputs;
            this.targets = targets;
            this.startsSequence = startsSequence;
        }
    }

    private static final class Experiment {
        private String name;
        private String type = "FeedForward";
        private int[] layers = new int[0];
        private boolean[] biases = new boolean[0];
        private double learningRate = 0.3;
        private double momentum = 0.8;
        private int steps;
        private boolean batchUpdate;
        private boolean crossEntropy;
        private boolean sequentialUpdate;
        private final List<PatternLine> patterns = new ArrayList<>();
        private final List<double[][]> weights = new ArrayList<>();
        private double[][] recurrentWeights;

        private static Experiment parse(Path path) throws IOException {
            Experiment experiment = new Experiment();
            List<String> lines = Files.readAllLines(path);
            Section section = Section.HEADER;
            List<String> matrixRows = new ArrayList<>();
            Integer activeWeightLayer = null;
            boolean nextPatternStartsSequence = false;

            for (String rawLine : lines) {
                String line = stripComment(rawLine).trim();
                if (line.isEmpty()) {
                    continue;
                }

                String lower = line.toLowerCase(Locale.ROOT);
                if (lower.equals("patterns:")) {
                    flushMatrix(experiment, matrixRows, activeWeightLayer, section);
                    section = Section.PATTERNS;
                    activeWeightLayer = null;
                    continue;
                }

                if (lower.startsWith("weights ") && lower.endsWith(":")) {
                    flushMatrix(experiment, matrixRows, activeWeightLayer, section);
                    section = Section.WEIGHTS;
                    activeWeightLayer = Integer.parseInt(lower.substring("weights ".length(), lower.length() - 1).trim());
                    continue;
                }

                if (lower.equals("recweights:")) {
                    flushMatrix(experiment, matrixRows, activeWeightLayer, section);
                    section = Section.RECURRENT;
                    activeWeightLayer = null;
                    continue;
                }

                switch (section) {
                    case HEADER -> parseHeaderLine(experiment, line);
                    case PATTERNS -> {
                        if (lower.equals("reset")) {
                            nextPatternStartsSequence = true;
                        } else {
                            experiment.patterns.add(parsePattern(line, nextPatternStartsSequence));
                            nextPatternStartsSequence = false;
                        }
                    }
                    case WEIGHTS, RECURRENT -> matrixRows.add(line);
                }
            }

            flushMatrix(experiment, matrixRows, activeWeightLayer, section);

            if (experiment.layers.length == 0) {
                throw new IllegalArgumentException("Probe file is missing layers.");
            }

            if (experiment.biases.length == 0) {
                experiment.biases = new boolean[experiment.layers.length];
            }

            if (experiment.biases.length != experiment.layers.length) {
                throw new IllegalArgumentException("Bias count must match layer count.");
            }

            return experiment;
        }

        private static void parseHeaderLine(Experiment experiment, String line) {
            int separator = line.indexOf('=');
            if (separator < 0) {
                throw new IllegalArgumentException("Invalid header line: " + line);
            }

            String key = line.substring(0, separator).trim().toLowerCase(Locale.ROOT);
            String value = line.substring(separator + 1).trim();

            switch (key) {
                case "name" -> experiment.name = value;
                case "type" -> experiment.type = value;
                case "layers" -> experiment.layers = parseIntVector(value);
                case "biases" -> experiment.biases = parseBooleanVector(value);
                case "learningrate" -> experiment.learningRate = Double.parseDouble(value);
                case "momentum" -> experiment.momentum = Double.parseDouble(value);
                case "steps" -> experiment.steps = Integer.parseInt(value);
                case "batchupdate" -> experiment.batchUpdate = Boolean.parseBoolean(value);
                case "crossentropy" -> experiment.crossEntropy = Boolean.parseBoolean(value);
                case "sequentialupdate" -> experiment.sequentialUpdate = Boolean.parseBoolean(value);
                default -> throw new IllegalArgumentException("Unknown header key: " + key);
            }
        }

        private static PatternLine parsePattern(String line, boolean startsSequence) {
            String[] parts = line.split("=>");
            if (parts.length != 2) {
                throw new IllegalArgumentException("Invalid pattern line: " + line);
            }

            return new PatternLine(parseDoubleVector(parts[0]), parseDoubleVector(parts[1]), startsSequence);
        }

        private static void flushMatrix(Experiment experiment, List<String> rows, Integer activeWeightLayer, Section section) {
            if (rows.isEmpty()) {
                return;
            }

            double[][] matrix = new double[rows.size()][];
            for (int index = 0; index < rows.size(); index++) {
                matrix[index] = parseDoubleVector(rows.get(index));
            }

            if (section == Section.WEIGHTS) {
                while (experiment.weights.size() <= activeWeightLayer) {
                    experiment.weights.add(null);
                }
                experiment.weights.set(activeWeightLayer, matrix);
            } else if (section == Section.RECURRENT) {
                experiment.recurrentWeights = matrix;
            }

            rows.clear();
        }

        private static String stripComment(String line) {
            int hash = line.indexOf('#');
            return hash >= 0 ? line.substring(0, hash) : line;
        }

        private static int[] parseIntVector(String text) {
            return Arrays.stream(text.split(","))
                    .map(String::trim)
                    .filter(token -> !token.isEmpty())
                    .mapToInt(Integer::parseInt)
                    .toArray();
        }

        private static boolean[] parseBooleanVector(String text) {
            String[] tokens = Arrays.stream(text.split(","))
                    .map(String::trim)
                    .filter(token -> !token.isEmpty())
                    .toArray(String[]::new);

            boolean[] values = new boolean[tokens.length];
            for (int index = 0; index < tokens.length; index++) {
                values[index] = Boolean.parseBoolean(tokens[index]);
            }

            return values;
        }

        private static double[] parseDoubleVector(String text) {
            return Arrays.stream(text.split(","))
                    .map(String::trim)
                    .filter(token -> !token.isEmpty())
                    .mapToDouble(Double::parseDouble)
                    .toArray();
        }
    }

    private enum Section {
        HEADER,
        PATTERNS,
        WEIGHTS,
        RECURRENT
    }
}
