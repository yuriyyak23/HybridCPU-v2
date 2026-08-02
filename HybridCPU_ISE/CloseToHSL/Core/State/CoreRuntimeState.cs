namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Stable reference identity and containment root for one constructed CPU core.
/// Containment does not transfer execution, commit, rollback or publication
/// authority from the existing state owners.
/// </summary>
internal sealed class CoreRuntimeState
{
    internal CoreRuntimeState()
    {
        Telemetry = new TelemetryState();
        Assist = new AssistState();
        Scratch = new ScratchState();
        Cache = new CacheState();
        Resources = new ResourceState();
        VirtualThreadControl = new VirtualThreadControlState();
        LegacyCompatibility = new LegacyCompatibilityState();
        Binding = new CoreBindingState();
        Frontend = new FrontendState();
        Decode = new DecodeState();
        Admission = new AdmissionState();
        Replay = new ReplayState();
        Retire = new RetireState();
        Architectural = new ArchitecturalState();
        Scheduling = new SchedulingState();
        Execution = new ExecutionState();
        MemoryPipeline = new MemoryPipelineState();
        Backend = new BackendState();
        Extensions = new ExtensionState();
    }

    internal TelemetryState Telemetry { get; }

    internal AssistState Assist { get; }

    internal ScratchState Scratch { get; }

    internal CacheState Cache { get; }

    internal ResourceState Resources { get; }

    internal VirtualThreadControlState VirtualThreadControl { get; }

    internal LegacyCompatibilityState LegacyCompatibility { get; }

    internal CoreBindingState Binding { get; }

    internal FrontendState Frontend { get; }

    internal DecodeState Decode { get; }

    internal AdmissionState Admission { get; }

    internal ReplayState Replay { get; }

    internal RetireState Retire { get; }

    internal ArchitecturalState Architectural { get; }

    internal SchedulingState Scheduling { get; }

    internal ExecutionState Execution { get; }

    internal MemoryPipelineState MemoryPipeline { get; }

    internal BackendState Backend { get; }

    internal ExtensionState Extensions { get; }
}
