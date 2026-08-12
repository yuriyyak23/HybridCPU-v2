using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU.Compiler.Core.IR;

public enum CompilerExactProbeEmissionDecisionKind
{
    DeniedDisabled = 0,
    DeniedDecisionReference,
    DeniedNamespace,
    DeniedOperation,
    DeniedLeaf,
    DeniedProfile,
    DeniedOperandAbi,
    DeniedSchedulingContract,
    EmittedExactProbe
}

public enum CompilerVirtualizationCarrierKind : byte
{
    None = 0,
    VmCall = 1
}

/// <summary>
/// Typed compiler scheduling facts required by one emission profile. These are
/// structural facts only; lane selection and bundle placement are not runtime authority.
/// </summary>
public sealed record CompilerVirtualizationSchedulingConstraints(
    byte BundleWidth,
    SlotClass RequiredSlotClass,
    IrSlotBindingKind RequiredBindingKind,
    IrIssueSlotMask StructurallyAllowedSlots,
    byte RequiredLane,
    IrSerializationKind RequiredSerialization,
    InstructionClass RequiredInstructionClass,
    SerializationClass RequiredIsaSerialization,
    bool RequiresExclusiveCycle,
    bool RequiresNonStealable,
    bool RequiresStageAClassAdmission,
    bool RequiresStageBMaterializationValidation);

/// <summary>
/// Explicit negative scope carried by a compiler emission decision. Adding a future
/// operation requires another accepted decision; none of these denials are inferred away.
/// </summary>
public sealed record CompilerVirtualizationAdjacentOperationDenials(
    bool DenyZeroLeaf,
    bool DenyAdjacentLeaves,
    bool DenyUnknownLeaves,
    bool DenyHighBits,
    bool DenyVmRead,
    bool DenyVmWrite,
    bool DenyNestedVirtualization,
    bool DenySecureCompute,
    bool DenyMemoryIoDmaIommuDevice,
    bool DenyLane6Lane7StreamPassthrough);

/// <summary>
/// Compiler-local kill switch and immutable reference to an already accepted runtime D2.
/// It describes which carrier profile the compiler may construct. It is neither a
/// capability, an admission certificate nor execution/completion/retire authority.
/// </summary>
public sealed record CompilerEmissionDecisionV1(
    bool CompilerEmissionEnabled,
    string ReferencedDecisionId,
    string ReferencedSpecDigest,
    string OperationNamespace,
    ushort NumericLeaf,
    string OperationId,
    uint OperandAbiVersion,
    string OperandAbi,
    string EmissionProfileVersion,
    string RequiredCompilerFeatureProfile,
    CompilerVirtualizationSchedulingConstraints RequiredSchedulingBundleConstraints,
    CompilerVirtualizationAdjacentOperationDenials AdjacentOperationDenials)
{
    public static CompilerEmissionDecisionV1 DefaultDisabled { get; } =
        CompilerExactProbeEmissionContract.CreateDecision(enabled: false);

    public static CompilerEmissionDecisionV1 ExactProbeEnabled() =>
        CompilerExactProbeEmissionContract.CreateDecision(enabled: true);
}

/// <summary>
/// Frontend request. The lowerer immediately normalizes this into a
/// <see cref="VirtualizationCompilerIntent"/> and does not use the decision as runtime policy.
/// </summary>
public sealed record CompilerExactProbeEmissionRequest(
    CompilerEmissionDecisionV1 Decision,
    string OperationNamespace,
    string OperationId,
    ulong NumericLeaf,
    byte Rs1Register,
    ulong KnownRs1Value,
    byte Rs2Register,
    byte RdRegister)
{
    public static CompilerExactProbeEmissionRequest Exact(
        CompilerEmissionDecisionV1 decision,
        byte rs1Register) =>
        new(
            decision,
            CompilerExactProbeEmissionContract.OperationNamespace,
            CompilerExactProbeEmissionContract.OperationId,
            CompilerExactProbeEmissionContract.NumericLeaf,
            rs1Register,
            CompilerExactProbeEmissionContract.NumericLeaf,
            Rs2Register: 0,
            RdRegister: 0);
}

/// <summary>
/// Canonical compiler virtualization intent. This is compile-time meaning and operand
/// selection only. Runtime never consumes this object and remains correct when it is absent.
/// </summary>
public sealed record VirtualizationCompilerIntent(
    CompilerVirtualizationCarrierKind CarrierKind,
    string OperationNamespace,
    string OperationId,
    ulong NumericLeaf,
    byte Rs1Register,
    ulong KnownRs1Value,
    byte Rs2Register,
    byte RdRegister,
    CompilerEmissionDecisionV1 ReferencedEmissionDecision);

/// <summary>
/// Positive compile-time legality evidence. Every field is an ISA/ABI/bundle fact;
/// this object intentionally contains no capability, domain or live runtime decision.
/// </summary>
public sealed record CompilerVirtualizationLegalityFacts(
    bool DecisionReferenceMatched,
    bool ExactOperationMatched,
    bool OperandAbiMatched,
    bool SchedulingContractMatched,
    bool ExactCarrierDeterministic,
    string DiagnosticClass)
{
    public bool IsCompileTimeLegal =>
        DecisionReferenceMatched &&
        ExactOperationMatched &&
        OperandAbiMatched &&
        SchedulingContractMatched &&
        ExactCarrierDeterministic;
}

public sealed record CompilerExactProbeEmissionMetadata(
    string ReferencedDecisionId,
    string ReferencedSpecDigest,
    string OperationNamespace,
    ushort NumericLeaf,
    string OperationId,
    uint OperandAbiVersion,
    string OperandAbi,
    string EmissionProfileVersion,
    CompilerVirtualizationSchedulingConstraints SchedulingBundleConstraints,
    CompilerVirtualizationAdjacentOperationDenials AdjacentOperationDenials)
{
    public const string AuthorityClassification = "CompilerEvidenceOnly";

    public bool IsRuntimeAuthority => false;

    public bool RuntimeCorrectnessDependsOnMetadata => false;
}

public sealed record CompilerExactProbeEmissionPlan(
    CompilerExactProbeEmissionRequest Request,
    VirtualizationCompilerIntent Intent,
    CompilerVirtualizationLegalityFacts LegalityFacts,
    CompilerExactProbeEmissionMetadata Metadata,
    VLIW_Instruction EncodedInstruction)
{
    public bool RuntimeAdmissionRemainsRequired => true;
}

public sealed record CompilerExactProbeEmissionResult(
    CompilerExactProbeEmissionDecisionKind DecisionKind,
    CompilerExactProbeEmissionPlan? Plan,
    string Reason)
{
    public bool Emitted => DecisionKind == CompilerExactProbeEmissionDecisionKind.EmittedExactProbe;
}

/// <summary>
/// The only accepted compiler emission profile in this repository state. This registry is
/// intentionally exact and non-generic: a new runtime D2 does not become compiler-emittable
/// until another compiler decision/profile is added explicitly.
/// </summary>
public static class CompilerExactProbeEmissionContract
{
    public const string DecisionId = "D2-HV-VMCALL-RUNTIME-V1-PROBE-0001";
    public const string SpecDigest = "33076e430fcbc05cf0774d08baadc6d7840f88029fcfb28a458558af82f93ca8";
    public const string OperationNamespace = "HybridCPU.VMCALL.Runtime.v1";
    public const ushort NumericLeaf = 0x0001;
    public const string OperationId = "PROBE_NO_STATE_V1";
    public const uint OperandAbiVersion = 1;
    public const string OperandAbi = "Rs1=ArchitecturalRegisterFullNumericLeafValue;Rs2=x0;Rd=x0";
    public const string EmissionProfileVersion = "CompilerEmissionDecisionV1/exact-probe/v1";
    public const string RequiredCompilerFeatureProfile = "HybridCPU.Compiler.Virtualization.ExactProbe.v1";

    public static CompilerVirtualizationSchedulingConstraints SchedulingConstraints { get; } = new(
        BundleWidth: BundleMetadata.BundleSlotCount,
        RequiredSlotClass: SlotClass.SystemSingleton,
        RequiredBindingKind: IrSlotBindingKind.HardPinned,
        StructurallyAllowedSlots: IrIssueSlotMask.Slot7,
        RequiredLane: 7,
        RequiredSerialization: IrSerializationKind.SystemBoundary | IrSerializationKind.ExclusiveCycle,
        RequiredInstructionClass: InstructionClass.Vmx,
        RequiredIsaSerialization: SerializationClass.VmxSerial,
        RequiresExclusiveCycle: true,
        RequiresNonStealable: true,
        RequiresStageAClassAdmission: true,
        RequiresStageBMaterializationValidation: true);

    public static CompilerVirtualizationAdjacentOperationDenials AdjacentOperationDenials { get; } = new(
        DenyZeroLeaf: true,
        DenyAdjacentLeaves: true,
        DenyUnknownLeaves: true,
        DenyHighBits: true,
        DenyVmRead: true,
        DenyVmWrite: true,
        DenyNestedVirtualization: true,
        DenySecureCompute: true,
        DenyMemoryIoDmaIommuDevice: true,
        DenyLane6Lane7StreamPassthrough: true);

    internal static CompilerEmissionDecisionV1 CreateDecision(bool enabled) =>
        new(
            enabled,
            DecisionId,
            SpecDigest,
            OperationNamespace,
            NumericLeaf,
            OperationId,
            OperandAbiVersion,
            OperandAbi,
            EmissionProfileVersion,
            RequiredCompilerFeatureProfile,
            SchedulingConstraints,
            AdjacentOperationDenials);

    internal static bool MatchesAcceptedProfile(CompilerEmissionDecisionV1 decision) =>
        decision.ReferencedDecisionId == DecisionId &&
        decision.ReferencedSpecDigest == SpecDigest &&
        decision.OperationNamespace == OperationNamespace &&
        decision.NumericLeaf == NumericLeaf &&
        decision.OperationId == OperationId &&
        decision.OperandAbiVersion == OperandAbiVersion &&
        decision.OperandAbi == OperandAbi &&
        decision.EmissionProfileVersion == EmissionProfileVersion &&
        decision.RequiredCompilerFeatureProfile == RequiredCompilerFeatureProfile &&
        decision.RequiredSchedulingBundleConstraints == SchedulingConstraints &&
        decision.AdjacentOperationDenials == AdjacentOperationDenials;
}
