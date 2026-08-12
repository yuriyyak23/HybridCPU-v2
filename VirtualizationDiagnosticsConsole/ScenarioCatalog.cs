namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal static class ScenarioCatalog
{
    public static IReadOnlyList<IVirtualizationScenario> All { get; } =
    [
        new E1FaultTransportScenario(),
        new PraD2GovernanceNegativeScenario(),
        new PrbD2AttributableMaterializationScenario(),
        new PrcO1OperandFaultOnlyScenario(),
        new PrdE2AdmissionFaultOnlyScenario(),
        new PreExactProbeExecutorNoPublicationScenario(),
        new PrfCanonicalHypercallCompositionScenario(),
        new PrgAtomicCompletionE5Scenario(),
        new PrhCanonicalRetireE6Scenario(),
        new PriDrainRestoreDeterminismScenario(),
        new PrjExactReleaseActivationRollbackScenario(),
        new GuestControlProjectionScenario(),
        new VmCallDeniedScenario(),
        new ResearchRuntimeProbeScenario(),
        new ResearchCanonicalCompositionScenario(),
        new AuthorityStateModelScenario(),
    ];

    public static IVirtualizationScenario Resolve(string id) =>
        All.FirstOrDefault(scenario => string.Equals(scenario.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown scenario '{id}'. Use 'list' to inspect available scenarios.");
}
