using YAKSys_Hybrid_CPU.Core.Vmcs.V2;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.Core;

public enum VmxCompatibilityVmReadAdmissionDecision : byte
{
    ScalarProjectionAllowed = 0,
    ReadOnlyProjectionDenied = 1,
    DecodeDenied = 2,
    ProjectionDenied = 3,
    RuntimeAdmissionDenied = 4,
    MissingDescriptor = 5,
    ReadOnlyValueProjected = 6,
}

public readonly record struct VmxCompatibilityVmReadAdmissionRequest(
    DomainRuntimeContext? Context,
    RootAuthorityDescriptor? RootAuthority,
    EvidencePolicyDescriptor? EvidencePolicy,
    VmcsV2Descriptor? Descriptor,
    ushort FieldId,
    byte DestinationRegister,
    byte FieldSelectorRegister,
    byte ReservedRegister,
    bool DescriptorValidated,
    bool CapabilityValidated,
    bool SchedulingValidated,
    bool NoEmissionValidated,
    bool ProjectionEvidenceValidated,
    CompletionRecord? Completion = null,
    PrivilegedExecutionStateDescriptor? PrivilegedExecutionState = null,
    PrivilegedExecutionStateEpoch CurrentPrivilegedExecutionStateEpoch = default,
    bool PrivilegedExecutionStateConformanceProven = false);

public readonly record struct VmxCompatibilityVmReadAdmissionResult(
    VmxCompatibilityVmReadAdmissionDecision Decision,
    VmxCompatDecodeResult Decode,
    VmxCompatProjectionResult Projection,
    RuntimeBoundaryAdmissionResult RuntimeAdmission,
    VmcsV2ValidationResult VmcsValidation,
    VmcsReadOnlyValueProjectionResult ValueProjection,
    long Value,
    string Reason)
{
    public bool RuntimeAdmissionAllowed =>
        (Decision is not VmxCompatibilityVmReadAdmissionDecision.DecodeDenied and
            not VmxCompatibilityVmReadAdmissionDecision.ProjectionDenied) &&
        RuntimeAdmission.IsAllowed;

    public bool IsScalarProjectionAllowed =>
        Decision == VmxCompatibilityVmReadAdmissionDecision.ScalarProjectionAllowed;

    public bool IsReadOnlyValueProjected =>
        Decision == VmxCompatibilityVmReadAdmissionDecision.ReadOnlyValueProjected &&
        ValueProjection.IsProjected;

    public bool IsReadOnlyProjectionDenied =>
        Decision == VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied &&
        RuntimeAdmission.IsAllowed &&
        VmcsValidation.Code == VmcsV2ValidationCode.AccessDenied &&
        !ValueProjection.IsProjected;
}

public sealed partial class VmxCompatibilityAdmissionService
{
    private readonly VmxCompatDecodeBoundary _decodeBoundary;
    private readonly VmxCompatProjectionService _projectionService;
    private readonly RuntimeBoundaryAdmissionService _runtimeAdmission;
    private readonly VmcsReadOnlyValueProjectionService _readOnlyValueProjection;

    public VmxCompatibilityAdmissionService()
        : this(
            new VmxCompatDecodeBoundary(),
            new VmxCompatProjectionService(),
            new RuntimeBoundaryAdmissionService(),
            new VmcsReadOnlyValueProjectionService())
    {
    }

    public VmxCompatibilityAdmissionService(
        VmxCompatDecodeBoundary decodeBoundary,
        VmxCompatProjectionService projectionService,
        RuntimeBoundaryAdmissionService runtimeAdmission,
        VmcsReadOnlyValueProjectionService readOnlyValueProjection)
    {
        _decodeBoundary = decodeBoundary;
        _projectionService = projectionService;
        _runtimeAdmission = runtimeAdmission;
        _readOnlyValueProjection = readOnlyValueProjection;
    }

    public VmxCompatibilityVmReadAdmissionResult AdmitVmReadProjection(
        VmxCompatibilityVmReadAdmissionRequest request)
    {
        VmxCompatDecodeResult decode = _decodeBoundary.Decode(new VmxCompatDecodeRequest(
            Opcode: IsaOpcodeValues.VMREAD,
            Rd: request.DestinationRegister,
            Rs1: request.FieldSelectorRegister,
            Rs2: request.ReservedRegister,
            DescriptorValidated: request.DescriptorValidated,
            CapabilityValidated: request.CapabilityValidated,
            SchedulingValidated: request.SchedulingValidated,
            NoEmissionValidated: request.NoEmissionValidated));

        if (!decode.IsAllowed)
        {
            return CreateResult(
                VmxCompatibilityVmReadAdmissionDecision.DecodeDenied,
                decode,
                default,
                default,
                default,
                default,
                value: 0,
                decode.Reason);
        }

        VmxCompatProjectionResult projection = _projectionService.ValidateProjection(
            new VmxCompatProjectionRequest(
                CompatAliasSourceKind.Opcode,
                "VMREAD",
                DescriptorValidated: request.DescriptorValidated,
                EvidenceValidated: request.ProjectionEvidenceValidated,
                AttemptsAuthoritativeMutation: false));

        if (!projection.IsAllowed)
        {
            return CreateResult(
                VmxCompatibilityVmReadAdmissionDecision.ProjectionDenied,
                decode,
                projection,
                default,
                default,
                default,
                value: 0,
                projection.Reason);
        }

        RuntimeBoundaryAdmissionResult admission = _runtimeAdmission.Validate(
            new RuntimeBoundaryAdmissionRequest(
                Context: request.Context,
                RootAuthority: request.RootAuthority,
                EvidencePolicy: request.EvidencePolicy,
                Operation: DomainRuntimeOperation.FromCompatibilityFrontend(
                    DomainRuntimeOperationKind.ReadCompatibilityProjection,
                    requiresCapabilityGrant: false,
                    isProjectionOnly: true),
                DomainBoundary: DomainBoundaryDescriptor.FullDomainRuntime,
                CapabilityRequirement: CapabilityBoundaryRequirement.None,
                EvidenceRequirement: EvidenceBoundaryRequirement.GuestVisible(
                    EvidenceVisibilityClass.CompatibilityAlias)));

        if (!admission.IsAllowed)
        {
            return CreateResult(
                VmxCompatibilityVmReadAdmissionDecision.RuntimeAdmissionDenied,
                decode,
                projection,
                admission,
                default,
                default,
                value: 0,
                admission.Message);
        }

        VmcsReadOnlyValueProjectionResult valueProjection = _readOnlyValueProjection.Project(
            new VmcsReadOnlyValueProjectionRequest(
                FieldId: request.FieldId,
                RuntimeAdmission: admission,
                EvidencePolicy: request.EvidencePolicy,
                DescriptorValidated: request.DescriptorValidated,
                Execution: request.Context?.Execution,
                Memory: request.Context?.Memory,
                Completion: request.Completion,
                PrivilegedExecutionState: request.PrivilegedExecutionState,
                RuntimeDomainTag: request.Context?.DomainTag ?? 0,
                RuntimeAddressSpaceTag: request.Context?.AddressSpaceTag ?? 0,
                CurrentPrivilegedExecutionStateEpoch:
                    request.CurrentPrivilegedExecutionStateEpoch,
                PrivilegedExecutionStateConformanceProven:
                    request.PrivilegedExecutionStateConformanceProven));

        return CreateResult(
            valueProjection.IsProjected
                ? VmxCompatibilityVmReadAdmissionDecision.ReadOnlyValueProjected
                : VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied,
            decode,
            projection,
            admission,
            valueProjection.Validation,
            valueProjection,
            valueProjection.Value,
            valueProjection.Reason);
    }

    private static VmxCompatibilityVmReadAdmissionResult CreateResult(
        VmxCompatibilityVmReadAdmissionDecision decision,
        VmxCompatDecodeResult decode,
        VmxCompatProjectionResult projection,
        RuntimeBoundaryAdmissionResult admission,
        VmcsV2ValidationResult validation,
        VmcsReadOnlyValueProjectionResult valueProjection,
        long value,
        string reason) =>
        new(
            decision,
            decode,
            projection,
            admission,
            validation,
            valueProjection,
            value,
            reason);
}
