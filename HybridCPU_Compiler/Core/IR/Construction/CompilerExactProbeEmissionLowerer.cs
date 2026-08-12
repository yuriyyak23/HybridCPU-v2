using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU.Compiler.Core.IR;

internal static class CompilerExactProbeEmissionLowerer
{
    internal static CompilerExactProbeEmissionResult Lower(
        CompilerExactProbeEmissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Decision);

        VirtualizationCompilerIntent intent = NormalizeIntent(request);
        CompilerEmissionDecisionV1 decision = intent.ReferencedEmissionDecision;

        if (!decision.CompilerEmissionEnabled)
        {
            return Deny(
                CompilerExactProbeEmissionDecisionKind.DeniedDisabled,
                "Exact probe compiler emission is disabled by default.");
        }

        if (!Exact(decision.ReferencedDecisionId, CompilerExactProbeEmissionContract.DecisionId) ||
            !Exact(decision.ReferencedSpecDigest, CompilerExactProbeEmissionContract.SpecDigest))
        {
            return Deny(
                CompilerExactProbeEmissionDecisionKind.DeniedDecisionReference,
                "Compiler emission requires the exact accepted DecisionId and SpecDigest reference.");
        }

        if (!Exact(decision.OperationNamespace, CompilerExactProbeEmissionContract.OperationNamespace) ||
            !Exact(intent.OperationNamespace, CompilerExactProbeEmissionContract.OperationNamespace))
        {
            return Deny(
                CompilerExactProbeEmissionDecisionKind.DeniedNamespace,
                "Compiler emission requires the exact accepted operation namespace in both decision and intent.");
        }

        if (!Exact(decision.OperationId, CompilerExactProbeEmissionContract.OperationId) ||
            !Exact(intent.OperationId, CompilerExactProbeEmissionContract.OperationId) ||
            intent.CarrierKind != CompilerVirtualizationCarrierKind.VmCall)
        {
            return Deny(
                CompilerExactProbeEmissionDecisionKind.DeniedOperation,
                "Compiler emission permits only the exact PROBE_NO_STATE_V1 VMCALL carrier.");
        }

        if (decision.NumericLeaf != CompilerExactProbeEmissionContract.NumericLeaf ||
            intent.NumericLeaf != CompilerExactProbeEmissionContract.NumericLeaf ||
            intent.KnownRs1Value != CompilerExactProbeEmissionContract.NumericLeaf ||
            intent.NumericLeaf > ushort.MaxValue ||
            intent.KnownRs1Value > ushort.MaxValue)
        {
            return Deny(
                CompilerExactProbeEmissionDecisionKind.DeniedLeaf,
                "Compiler emission permits full-value leaf 0x0001 only; zero, adjacent, high-bit and unknown leaves are denied.");
        }

        if (!Exact(decision.EmissionProfileVersion, CompilerExactProbeEmissionContract.EmissionProfileVersion) ||
            !Exact(decision.RequiredCompilerFeatureProfile, CompilerExactProbeEmissionContract.RequiredCompilerFeatureProfile) ||
            decision.AdjacentOperationDenials != CompilerExactProbeEmissionContract.AdjacentOperationDenials)
        {
            return Deny(
                CompilerExactProbeEmissionDecisionKind.DeniedProfile,
                "Compiler emission requires the exact feature/profile and adjacent-operation denial set.");
        }

        if (decision.OperandAbiVersion != CompilerExactProbeEmissionContract.OperandAbiVersion ||
            !Exact(decision.OperandAbi, CompilerExactProbeEmissionContract.OperandAbi) ||
            intent.Rs1Register == 0 ||
            intent.Rs1Register > ArchRegId.MaxValue ||
            intent.Rs2Register != 0 ||
            intent.RdRegister != 0)
        {
            return Deny(
                CompilerExactProbeEmissionDecisionKind.DeniedOperandAbi,
                "Exact probe requires Rs1=architectural non-x0 full-value leaf register, Rs2=x0 and Rd=x0.");
        }

        if (!CompilerExactProbeEmissionContract.MatchesAcceptedProfile(decision) ||
            !MatchesCanonicalSchedulingProfile(decision.RequiredSchedulingBundleConstraints))
        {
            return Deny(
                CompilerExactProbeEmissionDecisionKind.DeniedSchedulingContract,
                "Exact probe requires the canonical W=8 SystemSingleton/lane-7 exclusive-cycle scheduling contract.");
        }

        VLIW_Instruction instruction = new()
        {
            OpCode = (uint)InstructionsEnum.VMCALL,
            Word1 = VLIW_Instruction.PackArchRegs(
                intent.RdRegister,
                intent.Rs1Register,
                intent.Rs2Register)
        };

        CompilerVirtualizationLegalityFacts legalityFacts = new(
            DecisionReferenceMatched: true,
            ExactOperationMatched: true,
            OperandAbiMatched: true,
            SchedulingContractMatched: true,
            ExactCarrierDeterministic: true,
            DiagnosticClass: "CompileTimeIsaOperandBundleLegalityOnly");

        CompilerExactProbeEmissionMetadata metadata = new(
            decision.ReferencedDecisionId,
            decision.ReferencedSpecDigest,
            intent.OperationNamespace,
            checked((ushort)intent.NumericLeaf),
            intent.OperationId,
            decision.OperandAbiVersion,
            decision.OperandAbi,
            decision.EmissionProfileVersion,
            decision.RequiredSchedulingBundleConstraints,
            decision.AdjacentOperationDenials);

        return new(
            CompilerExactProbeEmissionDecisionKind.EmittedExactProbe,
            new(request, intent, legalityFacts, metadata, instruction),
            "Exact accepted carrier emitted; runtime capability/domain, E1/E2, execution, completion and retire checks remain independent and mandatory.");
    }

    private static VirtualizationCompilerIntent NormalizeIntent(
        CompilerExactProbeEmissionRequest request) =>
        new(
            CompilerVirtualizationCarrierKind.VmCall,
            request.OperationNamespace,
            request.OperationId,
            request.NumericLeaf,
            request.Rs1Register,
            request.KnownRs1Value,
            request.Rs2Register,
            request.RdRegister,
            request.Decision);

    private static bool MatchesCanonicalSchedulingProfile(
        CompilerVirtualizationSchedulingConstraints required)
    {
        IrOpcodeExecutionProfile actual =
            HybridCpuHazardModel.GetExecutionProfile(InstructionsEnum.VMCALL);
        var (instructionClass, serializationClass) =
            InstructionClassifier.Classify(InstructionsEnum.VMCALL);

        return required == CompilerExactProbeEmissionContract.SchedulingConstraints &&
               actual.DerivedSlotClass == required.RequiredSlotClass &&
               actual.DerivedBindingKind == required.RequiredBindingKind &&
               actual.StructurallyAllowedSlots == required.StructurallyAllowedSlots &&
               actual.Serialization == required.RequiredSerialization &&
               actual.RequiresExclusiveCycle == required.RequiresExclusiveCycle &&
               instructionClass == required.RequiredInstructionClass &&
               serializationClass == required.RequiredIsaSerialization;
    }

    private static bool Exact(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.Ordinal);

    private static CompilerExactProbeEmissionResult Deny(
        CompilerExactProbeEmissionDecisionKind kind,
        string reason) =>
        new(kind, Plan: null, reason);
}
