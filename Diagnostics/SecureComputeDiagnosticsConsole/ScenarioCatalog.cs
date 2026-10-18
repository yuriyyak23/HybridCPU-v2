namespace YAKSys_Hybrid_CPU.SecureComputeDiagnostics;

internal static class ScenarioCatalog
{
    public static IReadOnlyList<ISecureComputeScenario> All { get; } =
    [
        new AdmissionBoundaryScenario(),
        new DescriptorCarrierScenario(),
        new GrantAuthorityScenario(),
        new DescriptorMapScenario(),
        new CheckpointPayloadScenario(),
        new FailClosedBoundaryScenario(),
        new StaticReachabilityScenario(),
    ];

    public static ISecureComputeScenario Resolve(string id) =>
        All.FirstOrDefault(scenario => string.Equals(scenario.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown SecureCompute diagnostic scenario '{id}'. Use 'list' to inspect valid ids.");
}
