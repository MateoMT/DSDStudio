namespace Benchmark;

public static class Summary
{
    public sealed record Row(
        int Count,
        int Runs,
        long? CpuBest,
        long? GpuBest,
        double? CpuAverage,
        double? GpuAverage);

    public static void Print(IReadOnlyList<Result> results)
    {
        if (results.Count == 0)
            return;

        Console.WriteLine();
        Console.WriteLine("Best-of-N summary:");
        Console.WriteLine("Count\tRuns\tCPU best(ms)\tGPU best(ms)\tBest speedup\tCPU avg(ms)\tGPU avg(ms)");

        foreach (Row row in GetRows(results))
        {
            string speedup = row.CpuBest.HasValue && row.GpuBest.HasValue && row.GpuBest.Value > 0
                ? ((double)row.CpuBest.Value / row.GpuBest.Value).ToString("F2") + "x"
                : "";

            Console.WriteLine(
                $"{row.Count}\t{row.Runs}\t{Format(row.CpuBest)}\t\t{Format(row.GpuBest)}\t\t{speedup}\t\t{Format(row.CpuAverage)}\t\t{Format(row.GpuAverage)}");
        }
    }

    public static IReadOnlyList<Row> GetRows(IReadOnlyList<Result> results)
    {
        return results
            .GroupBy(result => result.Count)
            .OrderBy(group => group.Key)
            .Select(group => new Row(
                group.Key,
                group.Count(),
                Best(group.Select(result => result.CpuTime)),
                Best(group.Select(result => result.GpuTime)),
                Average(group.Select(result => result.CpuTime)),
                Average(group.Select(result => result.GpuTime))))
            .ToArray();
    }

    private static long? Best(IEnumerable<long?> values)
    {
        long[] validValues = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        if (validValues.Length == 0)
            return null;

        return validValues.Min();
    }

    private static double? Average(IEnumerable<long?> values)
    {
        long[] validValues = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        if (validValues.Length == 0)
            return null;

        return validValues.Average();
    }

    private static string Format(long? value)
    {
        return value.HasValue ? value.Value.ToString() : "";
    }

    private static string Format(double? value)
    {
        return value.HasValue ? value.Value.ToString("F1") : "";
    }
}
