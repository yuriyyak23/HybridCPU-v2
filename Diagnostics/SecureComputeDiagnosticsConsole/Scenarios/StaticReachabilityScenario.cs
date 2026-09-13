namespace YAKSys_Hybrid_CPU.SecureComputeDiagnostics;

internal sealed class StaticReachabilityScenario : ISecureComputeScenario
{
    public string Id => "static-reachability";
    public string Description => "Runs scoped source inventory for named SecureCompute owners and known authority shortcuts.";
    public DiagnosticSurfaceKind SurfaceKind => DiagnosticSurfaceKind.StaticInspection;
    public string AuthorityCeiling => "Scoped source inventory only; source strings never prove runtime reachability or authority.";

    public Task ExecuteAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        string root = RepositoryLocator.FindRoot();
        string productRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] files = Directory.EnumerateFiles(productRoot, "*.cs", SearchOption.AllDirectories).ToArray();
        string source = string.Join('\n', files.Select(File.ReadAllText));

        string[] expectedOwnerTypes =
        [
            "SecureAdmissionCertificate",
            "SecureDescriptorRegistry",
            "SecureGrantLedger",
            "SecureBackendResultOwner",
            "SecureCompletionOwner",
            "SecureRetireOwner",
            "SecureEvidencePublisher",
        ];
        int missingOwners = expectedOwnerTypes.Count(type => !source.Contains($"class {type}", StringComparison.Ordinal));
        bool callerDescriptorFallback = source.Contains("context.SecureCompute ?? request.SecureDescriptor", StringComparison.Ordinal);
        bool migrationDefaultAllow = source.Contains("_ => SecureCheckpointPayloadDecision.Allowed", StringComparison.Ordinal);

        context.Count("production_cs_files_scanned", files.Length);
        context.Count("named_owner_types_not_found", missingOwners);
        if (callerDescriptorFallback) context.Count("caller_descriptor_fallback_patterns");
        if (migrationDefaultAllow) context.Count("migration_default_allow_patterns");

        for (int iteration = 0; iteration < context.Profile.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Check(files.Length > 0, "production source inventory is non-empty");
            context.Trace("source-inventory", ("files", files.Length), ("missingOwners", missingOwners));
            context.CompleteIteration("Scoped source inventory completed.");
        }

        if (missingOwners != 0)
            context.Finding("C0-OWNER-CHAIN-INVENTORY", DiagnosticSeverity.Blocker,
                "Named production owner chain is incomplete",
                $"{missingOwners} of {expectedOwnerTypes.Length} exact owner type definitions were not found in the scoped production root. This is an inventory signal, not proof of nonexistence under every possible name.");
        if (callerDescriptorFallback)
            context.Finding("STATIC-DESCRIPTOR-FALLBACK", DiagnosticSeverity.Warning,
                "Context/request descriptor fallback is present",
                "The runtime service contains a two-carrier full-descriptor selection expression. Runtime behavior is checked separately by descriptor-carriers.");
        if (migrationDefaultAllow)
            context.Finding("STATIC-MIGRATION-DEFAULT", DiagnosticSeverity.Warning,
                "Checkpoint classifier contains a default-Allow arm",
                "Runtime behavior is checked separately by checkpoint-payload; this string observation is not used as authority evidence.");
        return Task.CompletedTask;
    }
}
