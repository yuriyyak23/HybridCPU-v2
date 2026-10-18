namespace HybridCPU_ISE.Tests;

internal enum VmxQuarantineEvidenceRole : byte
{
    Unknown = 0,
    FrontendHandler = 1,
    VmcsStateCarrier = 2,
    ShadowVmcsProjection = 3,
    CsrFallbackProjection = 4,
    MemoryTranslationBackend = 5,
    IoVirtualizationBackend = 6,
    LegacyAdapter = 7,
}

internal readonly record struct VmxQuarantineEvidenceEntry(
    string RelativePath,
    VmxQuarantineEvidenceRole Role,
    string RequiredBeforeReturn,
    bool MustRemainQuarantined,
    string ReturnedCorePath = "",
    bool RemovedWithoutReplacement = false)
{
    public bool IsRecognizedLegacyRisk =>
        !string.IsNullOrWhiteSpace(RelativePath) &&
        Role != VmxQuarantineEvidenceRole.Unknown &&
        !string.IsNullOrWhiteSpace(RequiredBeforeReturn) &&
        (MustRemainQuarantined || IsReturnedToCore || IsRemovedWithoutReplacement);

    public bool IsReturnedToCore =>
        !MustRemainQuarantined &&
        !string.IsNullOrWhiteSpace(ReturnedCorePath);

    public bool IsRemovedWithoutReplacement =>
        !MustRemainQuarantined &&
        string.IsNullOrWhiteSpace(ReturnedCorePath) &&
        RemovedWithoutReplacement;
}

internal readonly record struct VmxQuarantineReturnProof(
    bool OriginatesFromLegacyVmx,
    bool DescriptorOwnerIdentified,
    bool CapabilityPolicyAdded,
    bool EvidencePolicyAdded,
    bool RetireBoundaryDefined,
    bool ProjectionTestsAdded,
    bool ContainsAuthoritativeVmxState)
{
    public static VmxQuarantineReturnProof CompleteProjectionOnly { get; } = new(
        OriginatesFromLegacyVmx: true,
        DescriptorOwnerIdentified: true,
        CapabilityPolicyAdded: true,
        EvidencePolicyAdded: true,
        RetireBoundaryDefined: true,
        ProjectionTestsAdded: true,
        ContainsAuthoritativeVmxState: false);
}

internal sealed class VmxQuarantineEvidenceManifest
{
    private static readonly VmxQuarantineEvidenceEntry[] EntryTable =
    {
        new(
            "Legacy/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs",
            VmxQuarantineEvidenceRole.FrontendHandler,
            "Delete the broad frozen opcode shell and constructor ABI after current routing emits typed fail-closed effects only.",
            MustRemainQuarantined: false,
            RemovedWithoutReplacement: true),
        new(
            "Legacy/VMX/Substrate/Runtime/Binding/VmcsManager.cs",
            VmxQuarantineEvidenceRole.VmcsStateCarrier,
            "Delete the VMCS-owned runtime carrier after all Core lane/vector/dirty/Lane7/DMA references are removed or fail closed.",
            MustRemainQuarantined: false,
            RemovedWithoutReplacement: true),
        new(
            "Legacy/VMX/Compatibility/Generated/VmcsProjection/ShadowVmcsBlock.cs",
            VmxQuarantineEvidenceRole.ShadowVmcsProjection,
            "Keep VMCS12/ShadowVMCS vocabulary behind nested projection services only.",
            MustRemainQuarantined: false,
            RemovedWithoutReplacement: true),
        new(
            "Legacy/VMX/Compatibility/Generated/CsrProjection/LegacyCsrBackedVmxCapabilityDescriptorSource.cs",
            VmxQuarantineEvidenceRole.CsrFallbackProjection,
            "Delete the dead fail-closed CSR source after reachability audit; typed grant descriptors remain capability authority.",
            MustRemainQuarantined: false,
            RemovedWithoutReplacement: true),
        new(
            "Legacy/VMX/Substrate/Memory/Translation/LegacyVmcsMemoryTranslationControlProjection.cs",
            VmxQuarantineEvidenceRole.MemoryTranslationBackend,
            "Map translation authority to MemoryDomainDescriptor-owned policy before return.",
            MustRemainQuarantined: false,
            RemovedWithoutReplacement: true),
        new(
            "Legacy/VMX/Substrate/Memory/Iommu/IOMMU.DomainBinding.partial.cs",
            VmxQuarantineEvidenceRole.MemoryTranslationBackend,
            "Replace VMX-shaped IOMMU binding with memory/io domain invalidation services.",
            MustRemainQuarantined: false,
            "Memory/MMU/IOMMU.DomainBinding.cs"),
        new(
            "Legacy/VMX/Substrate/Memory/Invalidation/LegacyVmxTranslationInvalidationBackend.cs",
            VmxQuarantineEvidenceRole.MemoryTranslationBackend,
            "Remove the legacy invalidation backend authority without replacement.",
            MustRemainQuarantined: false,
            RemovedWithoutReplacement: true),
        new(
            "Legacy/VMX/Compatibility/Adapters/MemoryInvalidation/LegacyVmxTranslationInvalidationBackend.cs",
            VmxQuarantineEvidenceRole.MemoryTranslationBackend,
            "Delete the dead denied invalidation shell after reachability audit; neutral memory runtime owns invalidation.",
            MustRemainQuarantined: false,
            RemovedWithoutReplacement: true),
        new(
            "Legacy/VMX/Substrate/IO/LegacyVmxIoVirtualizationBackend.cs",
            VmxQuarantineEvidenceRole.IoVirtualizationBackend,
            "Remove the legacy I/O authority backend without replacement.",
            MustRemainQuarantined: false,
            RemovedWithoutReplacement: true),
        new(
            "Legacy/VMX/Compatibility/Adapters/IO/LegacyVmxIoVirtualizationBackend.cs",
            VmxQuarantineEvidenceRole.IoVirtualizationBackend,
            "Delete the dead denied I/O backend shell after reachability audit; neutral I/O runtime owns DMA/IOTLB authority.",
            MustRemainQuarantined: false,
            RemovedWithoutReplacement: true),
        new(
            "Legacy/VMX/Compatibility/Adapters/LegacyVmxV1/LegacyVmxV1AdapterBoundary.cs",
            VmxQuarantineEvidenceRole.LegacyAdapter,
            "Delete the dead v1 policy shell after reachability audit; current typed fail-closed routing remains in Core.",
            MustRemainQuarantined: false,
            RemovedWithoutReplacement: true),
        new(
            "Legacy/VMX/Compatibility/Adapters/LegacyVmxV1/ExecutionDispatcherV4.Vmx.cs",
            VmxQuarantineEvidenceRole.LegacyAdapter,
            "Return only as current VMX compatibility routing over the configured execution unit and retire-window boundary.",
            MustRemainQuarantined: false,
            "Core/Execution/ExecutionDispatcherV4.VmxCompatibility.cs"),
        new(
            "Legacy/VMX/Compatibility/Adapters/LegacyVmxV1/CPU_Core.PipelineExecution.Vmx.cs",
            VmxQuarantineEvidenceRole.LegacyAdapter,
            "Return only as current VMX retire effect materialization over the pipeline retire boundary.",
            MustRemainQuarantined: false,
            "Core/Pipeline/Core/CPU_Core.PipelineExecution.VmxRetire.cs"),
        new(
            "Legacy/VMX/Compatibility/Adapters/LegacyVmxV2/LegacyVmxV2AdapterBoundary.cs",
            VmxQuarantineEvidenceRole.LegacyAdapter,
            "Delete the dead v2 policy shell after reachability audit; generated/read-only projections remain explicit.",
            MustRemainQuarantined: false,
            RemovedWithoutReplacement: true),
        new(
            "Legacy/VMX/Compatibility/Frontend/Decode/VmxInstructionPayload.cs",
            VmxQuarantineEvidenceRole.FrontendHandler,
            "Return the decode payload carrier only as no-legacy compatibility ABI vocabulary.",
            MustRemainQuarantined: false,
            "Core/VMX/Compatibility/Frontend/Decode/VmxInstructionPayload.cs"),
        new(
            "Legacy/VMX/Compatibility/Frontend/Retire/VmxRetireModel.cs",
            VmxQuarantineEvidenceRole.FrontendHandler,
            "Return the retire carrier only as no-legacy typed fail-closed compatibility vocabulary.",
            MustRemainQuarantined: false,
            "Core/VMX/Compatibility/Frontend/Retire/VmxRetireModel.cs"),
        new(
            "Legacy/VMX/Compatibility/Generated/VmcsProjection/ShadowVmcsNestedProjectionService.cs",
            VmxQuarantineEvidenceRole.ShadowVmcsProjection,
            "Return the denied generated Shadow VMCS bridge only as no-legacy fail-closed projection vocabulary.",
            MustRemainQuarantined: false,
            "Core/VMX/Compatibility/Generated/VmcsProjection/ShadowVmcsNestedProjectionService.cs"),
    };

    public static ReadOnlySpan<VmxQuarantineEvidenceEntry> Entries => EntryTable;

    public bool TryGetEntry(
        string relativePath,
        out VmxQuarantineEvidenceEntry entry)
    {
        foreach (VmxQuarantineEvidenceEntry candidate in Entries)
        {
            if (string.Equals(candidate.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase))
            {
                entry = candidate;
                return true;
            }
        }

        entry = default;
        return false;
    }

    public bool RequiresQuarantine(string relativePath) =>
        TryGetEntry(relativePath, out VmxQuarantineEvidenceEntry entry) &&
        entry.IsRecognizedLegacyRisk &&
        entry.MustRemainQuarantined;

    public bool CanReturnToCore(
        string relativePath,
        VmxQuarantineReturnProof request)
    {
        if (!TryGetEntry(relativePath, out VmxQuarantineEvidenceEntry entry) ||
            !entry.IsRecognizedLegacyRisk ||
            entry.IsRemovedWithoutReplacement)
        {
            return false;
        }

        return IsReturnProofSatisfied(request);
    }

    private static bool IsReturnProofSatisfied(VmxQuarantineReturnProof request) =>
        request.OriginatesFromLegacyVmx &&
        request.DescriptorOwnerIdentified &&
        request.CapabilityPolicyAdded &&
        request.EvidencePolicyAdded &&
        request.RetireBoundaryDefined &&
        request.ProjectionTestsAdded &&
        !request.ContainsAuthoritativeVmxState;
}
