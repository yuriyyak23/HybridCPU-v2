using System.Text.Json;
using Xunit;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase43CurrentStatusGuardTests
{
    [Fact]
    public void CurrentStatus_RecordsClosedExactProductionWithoutOpeningNextCandidate()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string path = Path.Combine(root, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan", "VirtualizationActivationStatusV1.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement status = document.RootElement;
        JsonElement phase43 = status.GetProperty("VmReadGuestPcSpFlagsScalarDeliveryD2");

        Assert.Equal("ClosedExactProductionCompositionDefaultDisabled",
            phase43.GetProperty("State").GetString());
        Assert.Equal("D2-HV-VMREAD-SCALAR-DELIVERY-V1-GUEST-PC-SP-FLAGS-0002",
            phase43.GetProperty("DecisionId").GetString());
        Assert.Equal("ExecutionDomainDescriptor", phase43.GetProperty("SourceOwner").GetString());
        Assert.Equal("MaterializedExecutionDomainReadOnlyStateView",
            phase43.GetProperty("ValueSource").GetString());
        Assert.Equal("ExactGuestPcGuestSpGuestFlagsOnly",
            phase43.GetProperty("ProductionImplementation").GetString());
        Assert.Equal("ClosedRuntimeIssuedNonZeroAtomicCapture",
            phase43.GetProperty("SourceEpochProductionGate").GetString());
        Assert.Equal("Disabled", phase43.GetProperty("ActivationDefault").GetString());
        Assert.Equal("ExistingExecutionDomainAuthorityOnly",
            phase43.GetProperty("RuntimeAuthority").GetString());

        Assert.Equal("NoAutomaticActivationExpansion",
            status.GetProperty("NextOpenPool").GetString());
        Assert.Equal("NoneUntilSeparateNeutralCpuTranslationFaultCompletionProducerAuthorization",
            status.GetProperty("NextCandidatePool").GetString());
    }
}
