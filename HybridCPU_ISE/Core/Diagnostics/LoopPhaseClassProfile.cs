namespace YAKSys_Hybrid_CPU.Core.Diagnostics;

/// <summary>
/// Loop-local replay/class-capacity telemetry exported for compiler-side steady-state shaping.
/// </summary>
/// <param name="LoopPcAddress">Replay-phase PC used as the loop header identity.</param>
/// <param name="IterationsSampled">Number of loop iterations sampled for this profile.</param>
/// <param name="AluFreeVariance">Variance of free ALU-class capacity across sampled iterations.</param>
/// <param name="LsuFreeVariance">Variance of free LSU-class capacity across sampled iterations.</param>
/// <param name="DmaStreamFreeVariance">Variance of free DMA/stream class capacity across sampled iterations.</param>
/// <param name="BranchControlFreeVariance">Variance of free branch/control capacity across sampled iterations.</param>
/// <param name="SystemSingletonFreeVariance">Variance of free system-singleton capacity across sampled iterations.</param>
/// <param name="OverallClassVariance">Mean class-capacity variance across tracked slot classes.</param>
/// <param name="TemplateReuseRate">Loop-local replay template reuse rate in the range 0.0-1.0.</param>
public sealed record LoopPhaseClassProfile(
    ulong LoopPcAddress,
    int IterationsSampled,
    double AluFreeVariance,
    double LsuFreeVariance,
    double DmaStreamFreeVariance,
    double BranchControlFreeVariance,
    double SystemSingletonFreeVariance,
    double OverallClassVariance,
    double TemplateReuseRate);
