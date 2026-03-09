using System.Globalization;
using System.Text;

namespace SignalWeave.Core;

public static class BasicPropDisplayFormatter
{
    public static string FormatPatternSelector(int zeroBasedIndex, IReadOnlyList<double> inputs, IReadOnlyList<double>? targets)
    {
        return $"[{zeroBasedIndex}]: {FormatPatternHolder(inputs, targets)}";
    }

    public static string FormatPatternHolder(IReadOnlyList<double> inputs, IReadOnlyList<double>? targets)
    {
        var builder = new StringBuilder();
        builder.Append(FormatPattern(inputs));
        builder.Append("    >>>");
        builder.Append(FormatPattern(targets));
        return builder.ToString();
    }

    public static string FormatPattern(IReadOnlyList<double>? values)
    {
        if (values is null || values.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var index = 0; index < values.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append(FormatPatternValue(values[index]));
        }

        return builder.ToString();
    }

    public static string FormatHiddenActivationExport(IReadOnlyList<IReadOnlyList<double>> rows)
    {
        var builder = new StringBuilder();

        foreach (var row in rows)
        {
            foreach (var value in row)
            {
                builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
            }

            builder.AppendLine();
        }

        if (builder.Length == 0)
        {
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string FormatPatternValue(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
