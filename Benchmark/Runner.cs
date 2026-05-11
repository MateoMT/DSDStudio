using System.Diagnostics;
using ODE;

namespace Benchmark;

public sealed class Runner
{
    private readonly Options options;
    private readonly int baseSeed;

    public Runner(Options options)
    {
        this.options = options;
        baseSeed = options.Seed;
    }

    public IReadOnlyList<Result> Run()
    {
        var results = new List<Result>();
        DebugLog("Benchmark run started.");
        DebugLog($"Base seed: {baseSeed}.");
        Console.WriteLine("Run\tCount\tReactions\tCPU(ms)\tGPU(ms)\tSpeedup");

        for (int times = 0; times < options.Times; times++)
        {
            DebugLog($"Starting run {times + 1}/{options.Times}.");

            for (int count = options.MinCount; count <= options.MaxCount; count *= 2)
            {
                int reactionCount = checked(count * options.ReactionFactor);
                int seed = GetSeed(times, count);

                DebugLog($"Preparing test: run={times + 1}/{options.Times}, count={count}, reactions={reactionCount}, seed={seed}.");
                DebugLog("Generating ODE system with the parallel local-group generator...");
                ODEsys odes = GetOdes(count, reactionCount, seed);
                DebugLog($"ODE system generated. Equations={odes.equations.Count}.");

                DebugLog("Generating initial concentrations...");
                Dictionary<int, double> init = GetInitialValues(count, seed);
                DebugLog($"Initial concentrations ready. FinalTime={options.FinalTime}.");
                if (options.Debug)
                    OdeCheck.Check(odes, init).Print();

                long? cpuTime = null;
                string? cpuError = null;
                if (options.SkipCpu)
                {
                    DebugLog("CPU solver skipped.");
                }
                else
                {
                    try
                    {
                        DebugLog($"CPU solver started. step={options.CpuStep}, rtol={options.CpuRtol:E3}, atol={options.CpuAtol:E3}, minStep={options.CpuMinStep:E3}.");
                        cpuTime = GetTime(() =>
                            Solver.Solver.solve3(
                                odes,
                                init,
                                0,
                                options.FinalTime,
                                options.CpuStep,
                                options.CpuRtol,
                                options.CpuAtol,
                                minStep: options.CpuMinStep,
                                debugOutput: options.Debug,
                                debugEvery: options.DebugEvery,
                                debugPreviewCount: options.DebugPreviewCount));
                        DebugLog($"CPU solver finished in {cpuTime.Value} ms.");
                    }
                    catch (Exception ex)
                    {
                        cpuError = ex.Message;
                        DebugLog($"CPU solver failed: {cpuError}");
                    }
                }

                long? gpuTime = null;
                string? gpuError = null;
                if (options.SkipGpu)
                {
                    DebugLog("GPU solver skipped.");
                }
                else
                {
                    try
                    {
                        DebugLog($"GPU solver started. step={options.GpuStep}, rtol={options.GpuRtol:E3}, atol={options.GpuAtol:E3}, minStep={options.GpuMinStep:E3}.");
                        gpuTime = GetTime(() =>
                            Solver.Solver.solve4(
                                odes,
                                init,
                                0,
                                options.FinalTime,
                                options.GpuStep,
                                options.GpuRtol,
                                options.GpuAtol,
                                minStep: options.GpuMinStep,
                                debugOutput: options.Debug,
                                debugEvery: options.DebugEvery,
                                debugPreviewCount: options.DebugPreviewCount));
                        DebugLog($"GPU solver finished in {gpuTime.Value} ms.");
                    }
                    catch (Exception ex)
                    {
                        gpuError = ex.Message;
                        DebugLog($"GPU solver failed: {gpuError}");
                    }
                }

                var result = new Result(times, count, reactionCount, seed, cpuTime, cpuError, gpuTime, gpuError);
                results.Add(result);
                Print(result);
            }
        }

        DebugLog("Benchmark run finished.");
        return results;
    }

    private static Dictionary<int, double> GetInitialValues(int count, int seed)
    {
        Random random = new Random(seed ^ 0x5F3759DF);
        var init = new Dictionary<int, double>(count);
        for (int i = 0; i < count; i++)
            init[i] = random.NextDouble();

        return init;
    }

    private ODEsys GetOdes(int count, int reactionCount, int seed)
    {
        return ODEsys.GenerateParallelTestSystem(count, reactionCount, seed, maxRate: 1.0, groupSize: options.ParallelGroupSize);
    }

    private int GetSeed(int times, int count)
    {
        unchecked
        {
            int seed = baseSeed;
            seed = seed * 31 + times;
            seed = seed * 31 + count;
            seed = seed * 31 + options.ReactionFactor;
            return seed;
        }
    }

    private static long GetTime(Action action)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        action();
        stopwatch.Stop();
        return stopwatch.ElapsedMilliseconds;
    }

    private static void Print(Result result)
    {
        string cpu = result.CpuTime.HasValue
            ? result.CpuTime.Value.ToString()
            : result.CpuError is null ? "skipped" : "failed";
        string gpu = result.GpuTime.HasValue
            ? result.GpuTime.Value.ToString()
            : "failed";
        if (result.GpuTime is null && result.GpuError is null)
            gpu = "skipped";
        string speedup = result.CpuTime.HasValue && result.GpuTime.HasValue && result.GpuTime.Value > 0
            ? ((double)result.CpuTime.Value / result.GpuTime.Value).ToString("F2")
            : "";

        Console.WriteLine($"{result.Times + 1}\t{result.Count}\t{result.ReactionCount}\t\t{cpu}\t{gpu}\t{speedup}");
    }

    private void DebugLog(string message)
    {
        if (!options.Debug)
            return;

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
    }
}
