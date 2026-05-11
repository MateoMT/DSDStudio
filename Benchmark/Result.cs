namespace Benchmark;

public sealed record Result(
    int Times,
    int Count,
    int ReactionCount,
    int Seed,
    long? CpuTime,
    string? CpuError,
    long? GpuTime,
    string? GpuError);
