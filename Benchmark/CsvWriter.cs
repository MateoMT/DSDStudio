using System.Globalization;
using System.Text;

namespace Benchmark;

public static class CsvWriter
{
    public static void Write(string path, IReadOnlyList<Result> results)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var text = new StringBuilder();
        text.AppendLine("Times,Count,ReactionCount,Seed,CpuTime,CpuError,GpuTime,GpuError");

        foreach (Result result in results)
        {
            text.Append(result.Times.ToString(CultureInfo.InvariantCulture)).Append(',');
            text.Append(result.Count.ToString(CultureInfo.InvariantCulture)).Append(',');
            text.Append(result.ReactionCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            text.Append(result.Seed.ToString(CultureInfo.InvariantCulture)).Append(',');
            text.Append(result.CpuTime?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            text.Append(Escape(result.CpuError ?? string.Empty)).Append(',');
            text.Append(result.GpuTime?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            text.AppendLine(Escape(result.GpuError ?? string.Empty));
        }

        File.WriteAllText(path, text.ToString(), Encoding.UTF8);
    }

    public static string WriteSummary(string resultPath, IReadOnlyList<Result> results)
    {
        string summaryPath = GetSummaryPath(resultPath);
        string? dir = Path.GetDirectoryName(summaryPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var text = new StringBuilder();
        text.AppendLine("Count,Runs,CpuBest,GpuBest,BestSpeedup,CpuAverage,GpuAverage");

        foreach (Summary.Row row in Summary.GetRows(results))
        {
            double? speedup = row.CpuBest.HasValue && row.GpuBest.HasValue && row.GpuBest.Value > 0
                ? (double)row.CpuBest.Value / row.GpuBest.Value
                : null;

            text.Append(row.Count.ToString(CultureInfo.InvariantCulture)).Append(',');
            text.Append(row.Runs.ToString(CultureInfo.InvariantCulture)).Append(',');
            text.Append(row.CpuBest?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            text.Append(row.GpuBest?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            text.Append(speedup?.ToString("F6", CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            text.Append(row.CpuAverage?.ToString("F6", CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            text.AppendLine(row.GpuAverage?.ToString("F6", CultureInfo.InvariantCulture) ?? string.Empty);
        }

        File.WriteAllText(summaryPath, text.ToString(), Encoding.UTF8);
        return summaryPath;
    }

    private static string GetSummaryPath(string resultPath)
    {
        string? dir = Path.GetDirectoryName(resultPath);
        string fileName = Path.GetFileNameWithoutExtension(resultPath);
        string extension = Path.GetExtension(resultPath);
        if (string.IsNullOrEmpty(extension))
            extension = ".csv";

        return Path.Combine(dir ?? string.Empty, $"{fileName}-summary{extension}");
    }

    private static string Escape(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
            return value;

        return '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    }
}
