using Benchmark;

if (args.Any(arg => arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("-h", StringComparison.OrdinalIgnoreCase)))
{
    Options.PrintUsage();
    return;
}

Logger? logger = null;
try
{
    Options options = Options.Parse(args);
    logger = Logger.Start(options);

    Console.WriteLine("CPU/GPU ODE benchmark");
    Console.WriteLine($"CPU: {Hardware.GetCpuName()}");
    Console.WriteLine($"CPU logical processors: {Environment.ProcessorCount}");
    foreach (string gpu in Hardware.GetGpuNames())
        Console.WriteLine($"GPU: {gpu}");
    Console.WriteLine($"Count: {options.MinCount}..{options.MaxCount}");
    Console.WriteLine($"Runs: {options.Times}");
    Console.WriteLine($"Base seed: {options.Seed}");
    Console.WriteLine($"Output: {options.OutputPath}");
    Console.WriteLine($"Log: {(options.NoLog ? "disabled" : options.LogPath)}");
    if (options.Debug)
    {
        Console.WriteLine($"Generator: parallel local groups, reactionFactor={options.ReactionFactor}, groupSize={options.ParallelGroupSize}");
        Console.WriteLine($"Final time: {options.FinalTime}");
        Console.WriteLine($"CPU RK45: step={options.CpuStep}, rtol={options.CpuRtol:E3}, atol={options.CpuAtol:E3}, minStep={options.CpuMinStep:E3}");
        Console.WriteLine($"GPU RK45: step={options.GpuStep}, rtol={options.GpuRtol:E3}, atol={options.GpuAtol:E3}, minStep={options.GpuMinStep:E3}");
    }
    Console.WriteLine();

    var runner = new Runner(options);
    IReadOnlyList<Result> results = runner.Run();
    Summary.Print(results);

    CsvWriter.Write(options.OutputPath, results);
    string summaryPath = CsvWriter.WriteSummary(options.OutputPath, results);
    Console.WriteLine();
    Console.WriteLine($"Results saved to {options.OutputPath}");
    Console.WriteLine($"Summary saved to {summaryPath}");
    if (!options.NoLog)
        Console.WriteLine($"Log saved to {options.LogPath}");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Benchmark failed: {ex.Message}");
    Environment.ExitCode = 1;
}
finally
{
    logger?.Dispose();
}
