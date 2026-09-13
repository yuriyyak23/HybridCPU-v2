using System.Diagnostics;
using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal sealed class PrbD2AttributableMaterializationScenario : IVirtualizationScenario
{
    public string Id => "prb-d2-attributable-materialization";
    public string Description =>
        "PR-B attributable machine-D2 policy materialization; governance evidence only and no runtime authority.";
    public DiagnosticSurfaceKind SurfaceKind => DiagnosticSurfaceKind.ModelContract;

    public Task ExecuteAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        string repositoryRoot = FindRepositoryRoot();
        string containingCommitSha = Git(repositoryRoot, "rev-parse", "HEAD");
        string codeOwnersBlobSha = Git(
            repositoryRoot,
            "rev-parse",
            $"{Phase38VirtualizationDecisionAcceptanceV2.SpecCommitSha}:.github/CODEOWNERS");

        for (int iteration = 0; iteration < context.Profile.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VirtualizationDecisionValidationResultV2 validation =
                Phase38VirtualizationDecisionAcceptanceV2.ValidateRepositoryArtifact(
                    containingCommitSha);

            context.Check(
                codeOwnersBlobSha == Phase38VirtualizationDecisionAcceptanceV2.CodeOwnersBlobSha,
                "resolved CODEOWNERS blob must match the reviewed commit-A blob");
            context.Check(validation.IsAcceptedPolicyObject,
                "the exact attributable spec/acceptance pair must validate as policy metadata");
            context.Check(!validation.RuntimeCapabilityGranted,
                "accepted D2 policy must not grant a runtime capability");
            context.Check(!validation.BackendExecutionAuthorized,
                "accepted D2 policy must not authorize backend execution");
            context.Check(!validation.CompletionPublicationAuthorized,
                "accepted D2 policy must not authorize completion publication");
            context.Check(!validation.RetirePublicationAuthorized,
                "accepted D2 policy must not authorize retire publication");
            context.Check(Phase38AcceptedVirtualizationDecisionRegistry.TryResolvePolicy(
                    VirtualizationDecisionValidatorV2.ExpectedOperationNamespace,
                    1,
                    out AcceptedVirtualizationDecisionRegistryEntry entry),
                "generated registry must resolve the exact namespace/leaf only");
            context.Check(!Phase38AcceptedVirtualizationDecisionRegistry.TryResolvePolicy(
                    VirtualizationDecisionValidatorV2.ExpectedOperationNamespace,
                    2,
                    out _),
                "adjacent leaf must remain absent from the generated lookup");
            context.Check(!entry.RuntimeCapabilityGranted && !entry.BackendExecutionAuthorized,
                "generated policy entry must carry no runtime authority");

            context.Count("validated_policy_objects");
            context.Count("exact_lookup_hits");
            context.Count("adjacent_lookup_denials");
            context.Count("runtime_capability_grants", 0);
            context.Count("backend_authorizations", 0);
            context.Count("completion_publications", 0);
            context.Count("retire_publications", 0);
            context.Trace("prb-d2-attributable-materialization",
                ("evidenceClass", "attributable-machine-d2-policy-only"),
                ("specCommitSha", Phase38VirtualizationDecisionAcceptanceV2.SpecCommitSha),
                ("specDigest", Phase38VirtualizationDecisionAcceptanceV2.ExpectedSpecDigest),
                ("acceptanceDigest", Phase38VirtualizationDecisionAcceptanceV2.ExpectedAcceptanceDigest),
                ("acceptanceContainingCommitSha", containingCommitSha),
                ("codeOwnersBlobSha", codeOwnersBlobSha),
                ("principal", Phase38VirtualizationDecisionAcceptanceV2.RepositoryPrincipal),
                ("decision", validation.Decision),
                ("runtimeAuthority", false));
            context.CompleteIteration("Attributable D2 policy resolved exactly while all runtime stages remained denied.");
        }

        return Task.CompletedTask;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to resolve repository root for PR-B evidence.");
    }

    private static string Git(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using Process process = Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(error);
        return output.Trim();
    }
}
