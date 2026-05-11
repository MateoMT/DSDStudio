namespace Benchmark;

public sealed class Options
{
    public const int DefaultSeed = 288825296;

    public int MinCount { get; init; } = 4;
    public int MaxCount { get; init; } = 16_384;
    public int Times { get; init; } = 3;
    public int ReactionFactor { get; init; } = 4;
    public int ParallelGroupSize { get; init; } = 8;
    public double FinalTime { get; init; } = 5.0;
    public double CpuStep { get; init; } = 1.0;
    public double GpuStep { get; init; } = 1.0;
    public double CpuRtol { get; init; } = 1e-3;
    public double CpuAtol { get; init; } = 1e-5;
    public double CpuMinStep { get; init; } = 1e-8;
    public double GpuRtol { get; init; } = 1e-3;
    public double GpuAtol { get; init; } = 1e-5;
    public double GpuMinStep { get; init; } = 1e-8;
    public bool Debug { get; init; }
    public int DebugEvery { get; init; } = 100;
    public int DebugPreviewCount { get; init; } = 3;
    public bool SkipCpu { get; init; }
    public bool SkipGpu { get; init; }
    public int Seed { get; init; } = DefaultSeed;
    public string OutputPath { get; init; } = GetDefaultOutputPath();
    public string LogPath { get; init; } = GetDefaultLogPath();
    public bool NoLog { get; init; }

    public static Options Parse(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var knownOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "min",
            "max",
            "times",
            "iterations",
            "debug",
            "debug-every",
            "debug-preview",
            "skip-cpu",
            "skip-gpu",
            "seed",
            "output",
            "log",
            "no-log"
        };

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
                continue;

            string key = arg[2..];
            string value = "true";
            int equal = key.IndexOf('=');
            if (equal >= 0)
            {
                value = key[(equal + 1)..];
                key = key[..equal];
            }
            else if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[++i];
            }

            map[key] = value;
        }

        foreach (string key in map.Keys)
        {
            if (!knownOptions.Contains(key))
                throw new ArgumentException($"Unknown option --{key}. Use --help to list supported options.");
        }

        return new Options
        {
            MinCount = GetInt(map, "min", 4),
            MaxCount = GetInt(map, "max", 16_384),
            Times = GetInt(map, "times", GetInt(map, "iterations", 3)),
            Debug = GetBool(map, "debug", false),
            DebugEvery = GetInt(map, "debug-every", 100),
            DebugPreviewCount = GetInt(map, "debug-preview", 3),
            SkipCpu = GetBool(map, "skip-cpu", false),
            SkipGpu = GetBool(map, "skip-gpu", false),
            Seed = map.TryGetValue("seed", out string? seed) && int.TryParse(seed, out int seedValue) ? seedValue : DefaultSeed,
            OutputPath = map.TryGetValue("output", out string? output) && !string.IsNullOrWhiteSpace(output)
                ? output
                : GetDefaultOutputPath(),
            LogPath = map.TryGetValue("log", out string? log) && !string.IsNullOrWhiteSpace(log)
                ? log
                : GetDefaultLogPath(),
            NoLog = GetBool(map, "no-log", false)
        }.Check();
    }

    public static void PrintUsage()
    {
        Console.WriteLine("CPU/GPU ODE benchmark");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project Benchmark/Benchmark.csproj -- [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --min <n>              Minimum substance count. Default: 4");
        Console.WriteLine("  --max <n>              Maximum substance count. Default: 16384");
        Console.WriteLine("  --times <n>            Number of benchmark passes. Default: 3");
        Console.WriteLine($"  --seed <n>             Base seed. Default: {DefaultSeed}");
        Console.WriteLine("  --output <path>        CSV output path.");
        Console.WriteLine("  --log <path>           Log file path. Default: benchmark-results/...");
        Console.WriteLine("  --no-log               Disable log file writing.");
        Console.WriteLine("  --debug                Print RK45 trial/accepted step diagnostics.");
        Console.WriteLine("  --debug-every <n>      Print accepted-step diagnostics every n steps. Default: 100");
        Console.WriteLine("  --debug-preview <n>    Number of state values shown in debug output. Default: 3");
        Console.WriteLine("  --skip-cpu             Do not run the CPU solver.");
        Console.WriteLine("  --skip-gpu             Do not run the GPU solver.");
        Console.WriteLine("  --help                 Show this help.");
    }

    private Options Check()
    {
        if (MinCount < 1)
            throw new ArgumentOutOfRangeException(nameof(MinCount), "MinCount must be at least 1.");
        if (MaxCount < MinCount)
            throw new ArgumentOutOfRangeException(nameof(MaxCount), "MaxCount must be greater than or equal to MinCount.");
        if (Times < 1)
            throw new ArgumentOutOfRangeException(nameof(Times), "Times must be at least 1.");
        if (ReactionFactor < 1)
            throw new ArgumentOutOfRangeException(nameof(ReactionFactor), "ReactionFactor must be at least 1.");
        if (ParallelGroupSize < 1)
            throw new ArgumentOutOfRangeException(nameof(ParallelGroupSize), "ParallelGroupSize must be at least 1.");
        if (FinalTime <= 0)
            throw new ArgumentOutOfRangeException(nameof(FinalTime), "FinalTime must be greater than 0.");
        if (CpuStep <= 0)
            throw new ArgumentOutOfRangeException(nameof(CpuStep), "CpuStep must be greater than 0.");
        if (GpuStep <= 0)
            throw new ArgumentOutOfRangeException(nameof(GpuStep), "GpuStep must be greater than 0.");
        if (CpuRtol <= 0)
            throw new ArgumentOutOfRangeException(nameof(CpuRtol), "CpuRtol must be greater than 0.");
        if (CpuAtol <= 0)
            throw new ArgumentOutOfRangeException(nameof(CpuAtol), "CpuAtol must be greater than 0.");
        if (CpuMinStep <= 0)
            throw new ArgumentOutOfRangeException(nameof(CpuMinStep), "CpuMinStep must be greater than 0.");
        if (GpuRtol <= 0)
            throw new ArgumentOutOfRangeException(nameof(GpuRtol), "GpuRtol must be greater than 0.");
        if (GpuAtol <= 0)
            throw new ArgumentOutOfRangeException(nameof(GpuAtol), "GpuAtol must be greater than 0.");
        if (GpuMinStep <= 0)
            throw new ArgumentOutOfRangeException(nameof(GpuMinStep), "GpuMinStep must be greater than 0.");
        if (DebugEvery < 1)
            throw new ArgumentOutOfRangeException(nameof(DebugEvery), "DebugEvery must be at least 1.");
        if (DebugPreviewCount < 1)
            throw new ArgumentOutOfRangeException(nameof(DebugPreviewCount), "DebugPreviewCount must be at least 1.");
        if (SkipCpu && SkipGpu)
            throw new ArgumentException("At least one solver must run. Do not use --skip-cpu and --skip-gpu together.");

        return this;
    }

    private static int GetInt(IReadOnlyDictionary<string, string> map, string name, int defaultValue)
    {
        return map.TryGetValue(name, out string? value) && int.TryParse(value, out int parsed)
            ? parsed
            : defaultValue;
    }

    private static bool GetBool(IReadOnlyDictionary<string, string> map, string name, bool defaultValue)
    {
        if (!map.TryGetValue(name, out string? value))
            return defaultValue;
        if (bool.TryParse(value, out bool parsed))
            return parsed;

        return value is "1" or "yes" or "on";
    }

    private static string GetDefaultOutputPath()
    {
        string fileName = $"cpu-gpu-ode-benchmark-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
        return Path.Combine("benchmark-results", fileName);
    }

    private static string GetDefaultLogPath()
    {
        string fileName = $"cpu-gpu-ode-benchmark-{DateTime.Now:yyyyMMdd-HHmmss}.log";
        return Path.Combine("benchmark-results", fileName);
    }
}
