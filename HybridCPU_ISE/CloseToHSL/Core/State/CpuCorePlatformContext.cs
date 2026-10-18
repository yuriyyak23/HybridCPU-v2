using System;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace YAKSys_Hybrid_CPU
{
    /// <summary>
    /// Immutable construction request for the one already-proven neutral
    /// PROBE_NO_STATE_V1 runtime contour. The recorded subject SHA identifies
    /// the reviewed contour; it is evidence binding, not runtime authority.
    /// </summary>
    public readonly record struct CpuExactProbeRuntimeConstructionProfile
    {
        public const string ProvenContourSubjectCommitSha =
            "bcd2d7f4654d4dab17c7a6705cb885fdd572510d";

        public CpuExactProbeRuntimeConstructionProfile(
            ulong domainTag,
            string provenContourSubjectCommitSha)
        {
            if (domainTag == 0 || domainTag > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(domainTag));
            if (!string.Equals(
                    provenContourSubjectCommitSha,
                    ProvenContourSubjectCommitSha,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Exact probe construction requires the reviewed contour subject SHA.",
                    nameof(provenContourSubjectCommitSha));
            }

            DomainTag = domainTag;
            ProvenContourCommitSha = ProvenContourSubjectCommitSha;
            IsConfigured = true;
        }

        public ulong DomainTag { get; }
        public string? ProvenContourCommitSha { get; }
        public bool IsConfigured { get; }
    }

    /// <summary>
    /// Immutable construction-only binding for the exact released read-only
    /// PinBasedControls VMREAD profile. These identifiers bind reviewed
    /// compatibility decisions; they are evidence, never runtime authority.
    /// </summary>
    public readonly record struct PinBasedControlsVmReadProductionConstructionProfile
    {
        public const string FrozenAbiDecisionId =
            "ABI-HV-VMX8-PIN-BASED-CONTROLS-0001";
        public const string ProjectionDecisionId =
            "D2-HV-VMREAD-PIN-BASED-CONTROLS-0005";
        public const string ScalarDeliveryDecisionId =
            "D2-HV-VMREAD-PIN-BASED-CONTROLS-SCALAR-0006";
        public const string ReviewedImplementationSubjectSha =
            "bcb10489762c4a560813be8efac299408d8f2906";
        public const string ReviewedImplementationSubjectTree =
            "e5db97f9dcee4cab0d6114fcc85a00f305339011";

        public PinBasedControlsVmReadProductionConstructionProfile(
            ulong domainIdentity,
            string frozenAbiDecisionId,
            string projectionDecisionId,
            string scalarDeliveryDecisionId,
            string reviewedImplementationSubjectSha,
            string reviewedImplementationSubjectTree)
        {
            if (domainIdentity == 0)
                throw new ArgumentOutOfRangeException(nameof(domainIdentity));
            RequireExact(frozenAbiDecisionId, FrozenAbiDecisionId, nameof(frozenAbiDecisionId));
            RequireExact(projectionDecisionId, ProjectionDecisionId, nameof(projectionDecisionId));
            RequireExact(scalarDeliveryDecisionId, ScalarDeliveryDecisionId, nameof(scalarDeliveryDecisionId));
            RequireExact(reviewedImplementationSubjectSha,
                ReviewedImplementationSubjectSha, nameof(reviewedImplementationSubjectSha));
            RequireExact(reviewedImplementationSubjectTree,
                ReviewedImplementationSubjectTree, nameof(reviewedImplementationSubjectTree));

            DomainIdentity = domainIdentity;
            IsConfigured = true;
        }

        public ulong DomainIdentity { get; }
        public bool IsConfigured { get; }
        public bool RuntimeAuthorityGranted => false;

        public static PinBasedControlsVmReadProductionConstructionProfile CreateExact(
            ulong domainIdentity) => new(
                domainIdentity,
                FrozenAbiDecisionId,
                ProjectionDecisionId,
                ScalarDeliveryDecisionId,
                ReviewedImplementationSubjectSha,
                ReviewedImplementationSubjectTree);

        private static void RequireExact(string actual, string expected, string parameterName)
        {
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                throw new ArgumentException("Exact PinBasedControls release identity mismatch.", parameterName);
        }
    }

    /// <summary>
    /// Explicit runtime dependencies for a live CPU core instance.
    /// New production core construction should go through this context instead
    /// of implicitly reading mutable global Processor state.
    /// </summary>
    public readonly record struct CpuCorePlatformContext
    {
        private readonly Func<Processor.MainMemoryArea, IAtomicMemoryUnit>? _atomicMemoryUnitFactory;

        public CpuCorePlatformContext(
            Processor.MainMemoryArea mainMemory,
            ProcessorMode initialExecutionMode,
            bool trackGlobalExecutionMode = false,
            Func<Processor.MainMemoryArea, IAtomicMemoryUnit>? atomicMemoryUnitFactory = null,
            CpuInstructionTranslationPolicy? cpuInstructionTranslationPolicy = null,
            bool enableCompletionReasonQualificationVmRead = false,
            bool enableCompletionSecondStageVmRead = false,
            CpuExactProbeRuntimeConstructionProfile? exactProbeRuntimeProfile = null,
            CompatibilityControlPolicyConstructionProfile? compatibilityControlPolicyProfile = null,
            PinBasedControlsVmReadProductionConstructionProfile? pinBasedControlsVmReadProfile = null,
            IHybridCpuIseEcallBridgeV1? ecallBridge = null)
        {
            MainMemory = mainMemory ?? throw new ArgumentNullException(nameof(mainMemory));
            InitialExecutionMode = initialExecutionMode;
            TrackGlobalExecutionMode = trackGlobalExecutionMode;
            _atomicMemoryUnitFactory = atomicMemoryUnitFactory;
            CpuInstructionTranslationPolicy =
                cpuInstructionTranslationPolicy ?? CpuInstructionTranslationPolicy.Identity;
            EnableCompletionReasonQualificationVmRead = enableCompletionReasonQualificationVmRead;
            EnableCompletionSecondStageVmRead = enableCompletionSecondStageVmRead;
            ExactProbeRuntimeProfile = exactProbeRuntimeProfile;
            CompatibilityControlPolicyProfile = compatibilityControlPolicyProfile;
            PinBasedControlsVmReadProfile = pinBasedControlsVmReadProfile;
            EcallBridge = ecallBridge;
            if (pinBasedControlsVmReadProfile is { IsConfigured: true } exactPinBasedProfile)
            {
                if (compatibilityControlPolicyProfile is not { IsConfigured: true } policyProfile ||
                    policyProfile.DomainIdentity != exactPinBasedProfile.DomainIdentity)
                {
                    throw new ArgumentException(
                        "Exact PinBasedControls release construction requires the same canonical compatibility-control policy domain.",
                        nameof(pinBasedControlsVmReadProfile));
                }
                EnablePinBasedControlsVmRead = true;
            }
            IsConfigured = true;
        }

        public Processor.MainMemoryArea MainMemory { get; }

        public ProcessorMode InitialExecutionMode { get; }

        public bool TrackGlobalExecutionMode { get; }

        public bool IsConfigured { get; }

        public CpuInstructionTranslationPolicy CpuInstructionTranslationPolicy { get; }
        public bool EnableCompletionReasonQualificationVmRead { get; }
        public bool EnableCompletionSecondStageVmRead { get; }
        public CpuExactProbeRuntimeConstructionProfile? ExactProbeRuntimeProfile { get; }
        public CompatibilityControlPolicyConstructionProfile? CompatibilityControlPolicyProfile { get; }
        public PinBasedControlsVmReadProductionConstructionProfile? PinBasedControlsVmReadProfile { get; }
        public bool EnablePinBasedControlsVmRead { get; }
        public IHybridCpuIseEcallBridgeV1? EcallBridge { get; }

        public ProcessorMode ResolveExecutionMode() =>
            TrackGlobalExecutionMode ? Processor.CurrentProcessorMode : InitialExecutionMode;

        public IAtomicMemoryUnit CreateAtomicMemoryUnit()
        {
            Func<Processor.MainMemoryArea, IAtomicMemoryUnit>? atomicMemoryUnitFactory = _atomicMemoryUnitFactory;
            if (atomicMemoryUnitFactory != null)
            {
                return atomicMemoryUnitFactory(MainMemory);
            }

            return new MainMemoryAtomicMemoryUnit(MainMemory);
        }

        public static CpuCorePlatformContext CreateFixed(
            Processor.MainMemoryArea mainMemory,
            ProcessorMode executionMode,
            Func<Processor.MainMemoryArea, IAtomicMemoryUnit>? atomicMemoryUnitFactory = null,
            CpuInstructionTranslationPolicy? cpuInstructionTranslationPolicy = null,
            bool enableCompletionReasonQualificationVmRead = false,
            bool enableCompletionSecondStageVmRead = false,
            CpuExactProbeRuntimeConstructionProfile? exactProbeRuntimeProfile = null,
            CompatibilityControlPolicyConstructionProfile? compatibilityControlPolicyProfile = null,
            PinBasedControlsVmReadProductionConstructionProfile? pinBasedControlsVmReadProfile = null,
            IHybridCpuIseEcallBridgeV1? ecallBridge = null) =>
            new(
                mainMemory,
                executionMode,
                trackGlobalExecutionMode: false,
                atomicMemoryUnitFactory: atomicMemoryUnitFactory,
                cpuInstructionTranslationPolicy: cpuInstructionTranslationPolicy,
                enableCompletionReasonQualificationVmRead: enableCompletionReasonQualificationVmRead,
                enableCompletionSecondStageVmRead: enableCompletionSecondStageVmRead,
                exactProbeRuntimeProfile: exactProbeRuntimeProfile,
                compatibilityControlPolicyProfile: compatibilityControlPolicyProfile,
                pinBasedControlsVmReadProfile: pinBasedControlsVmReadProfile,
                ecallBridge: ecallBridge);

        public static CpuCorePlatformContext CreateLegacy(
            Processor.MainMemoryArea? mainMemory = null,
            Func<Processor.MainMemoryArea, IAtomicMemoryUnit>? atomicMemoryUnitFactory = null,
            CpuInstructionTranslationPolicy? cpuInstructionTranslationPolicy = null,
            bool enableCompletionReasonQualificationVmRead = false,
            bool enableCompletionSecondStageVmRead = false,
            CpuExactProbeRuntimeConstructionProfile? exactProbeRuntimeProfile = null,
            CompatibilityControlPolicyConstructionProfile? compatibilityControlPolicyProfile = null,
            PinBasedControlsVmReadProductionConstructionProfile? pinBasedControlsVmReadProfile = null,
            IHybridCpuIseEcallBridgeV1? ecallBridge = null) =>
            new(
                mainMemory ?? Processor.MainMemory,
                Processor.CurrentProcessorMode,
                trackGlobalExecutionMode: true,
                atomicMemoryUnitFactory: atomicMemoryUnitFactory,
                cpuInstructionTranslationPolicy: cpuInstructionTranslationPolicy,
                enableCompletionReasonQualificationVmRead: enableCompletionReasonQualificationVmRead,
                enableCompletionSecondStageVmRead: enableCompletionSecondStageVmRead,
                exactProbeRuntimeProfile: exactProbeRuntimeProfile,
                compatibilityControlPolicyProfile: compatibilityControlPolicyProfile,
                pinBasedControlsVmReadProfile: pinBasedControlsVmReadProfile,
                ecallBridge: ecallBridge);
    }
}
