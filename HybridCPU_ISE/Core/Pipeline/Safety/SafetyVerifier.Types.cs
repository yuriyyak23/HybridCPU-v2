using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Combined reject classification for typed-slot world.
    /// Provides a complete picture: what kind of reject, where it happened, why.
    /// <para>
    /// HLS design note: pure combinational decode of existing reject codes.
    /// 3 comparators, no flip-flops, no timing impact.
    /// </para>
    /// </summary>
    public readonly struct TypedSlotRejectClassification
    {
        /// <summary>Admission-stage reject reason from the two-stage pipeline.</summary>
        public TypedSlotRejectReason AdmissionReject { get; }

        /// <summary>Certificate-level detail (shared vs register group).</summary>
        public CertificateRejectDetail CertificateDetail { get; }

        /// <summary>Slot class of the rejected candidate.</summary>
        public SlotClass CandidateClass { get; }

        /// <summary>Pinning kind of the rejected candidate.</summary>
        public SlotPinningKind PinningKind { get; }

        /// <summary>True if reject was due to hard-pinned lane already occupied.</summary>
        public bool IsPinnedConflict { get; }

        /// <summary>True if reject was due to all class lanes being occupied.</summary>
        public bool IsClassCapacityIssue { get; }

        /// <summary>True if reject was due to static overcommit (compiler fault).</summary>
        public bool IsStaticOvercommit { get; }

        /// <summary>True if reject was due to dynamic typed-slot densification exhaustion during normal runtime class-capacity pressure.</summary>
        public bool IsDynamicExhaustion { get; }

        /// <summary>True if reject was due to dynamic runtime state (scoreboard, bank, budget).</summary>
        public bool IsDynamicStateIssue { get; }

        public TypedSlotRejectClassification(
            TypedSlotRejectReason admissionReject,
            CertificateRejectDetail certificateDetail,
            SlotClass candidateClass,
            SlotPinningKind pinningKind,
            bool isPinnedConflict,
            bool isClassCapacityIssue,
            bool isStaticOvercommit,
            bool isDynamicExhaustion,
            bool isDynamicStateIssue)
        {
            AdmissionReject = admissionReject;
            CertificateDetail = certificateDetail;
            CandidateClass = candidateClass;
            PinningKind = pinningKind;
            IsPinnedConflict = isPinnedConflict;
            IsClassCapacityIssue = isClassCapacityIssue;
            IsStaticOvercommit = isStaticOvercommit;
            IsDynamicExhaustion = isDynamicExhaustion;
            IsDynamicStateIssue = isDynamicStateIssue;
        }
    }

    /// <summary>
    /// Explicit classification for domain-isolation stress probes.
    /// </summary>
    public readonly struct DomainIsolationProbeResult
    {
        public DomainIsolationProbeResult(bool isAllowed, bool isCrossDomainBlock, bool isKernelToUserBlock)
        {
            IsAllowed = isAllowed;
            IsCrossDomainBlock = isCrossDomainBlock;
            IsKernelToUserBlock = isKernelToUserBlock;
        }

        public bool IsAllowed { get; }

        public bool IsCrossDomainBlock { get; }

        public bool IsKernelToUserBlock { get; }
    }

    /// <summary>
    /// Explicit checker-owned guard decision for inter-core domain filtering.
    /// Keeps telemetry classification available without letting scheduler-side callers
    /// treat the raw probe result as legality authority.
    /// </summary>
    public readonly struct InterCoreDomainGuardDecision
    {
        public InterCoreDomainGuardDecision(
            LegalityDecision legalityDecision,
            DomainIsolationProbeResult probeResult)
        {
            LegalityDecision = legalityDecision;
            ProbeResult = probeResult;
        }

        public LegalityDecision LegalityDecision { get; }

        public DomainIsolationProbeResult ProbeResult { get; }

        public bool IsAllowed => LegalityDecision.IsAllowed;
    }

    /// <summary>
    /// Which structural authority produced the current legality decision.
    /// </summary>
    public enum LegalityAuthoritySource : byte
    {
        StructuralCertificate = 0,
        ReplayPhaseCertificate = 1,
        GuardPlane = 2,
        DetailedCompatibilityCheck = 3,
        AdmissionMetadataStructuralCheck = 4
    }

    /// <summary>
    /// Explicit legality reject classification for runtime checker decisions.
    /// </summary>
    public enum RejectKind : byte
    {
        None = 0,
        Boundary,
        OwnerMismatch,
        DomainMismatch,
        EpochMismatch,
        ClassCapacity,
        LaneUnavailable,
        CrossLaneConflict,
        RareHazard,
        Ordering,
        CertificateStale
    }

    /// <summary>
    /// Explicit checker-owned legality decision for scheduler hot-path consumers.
    /// Keeps structural certificate detail local while exposing a stable authority seam.
    /// </summary>
    public readonly struct LegalityDecision
    {
        public LegalityDecision(
            bool isAllowed,
            RejectKind rejectKind,
            CertificateRejectDetail certificateDetail,
            LegalityAuthoritySource authoritySource,
            bool attemptedReplayCertificateReuse)
        {
            IsAllowed = isAllowed;
            RejectKind = rejectKind;
            CertificateDetail = certificateDetail;
            AuthoritySource = authoritySource;
            AttemptedReplayCertificateReuse = attemptedReplayCertificateReuse;
        }

        public bool IsAllowed { get; }

        public RejectKind RejectKind { get; }

        public CertificateRejectDetail CertificateDetail { get; }

        public LegalityAuthoritySource AuthoritySource { get; }

        public bool AttemptedReplayCertificateReuse { get; }

        public bool ReusedReplayCertificate => AuthoritySource == LegalityAuthoritySource.ReplayPhaseCertificate;

        public static LegalityDecision Allow(
            LegalityAuthoritySource authoritySource,
            bool attemptedReplayCertificateReuse)
        {
            return new LegalityDecision(
                isAllowed: true,
                rejectKind: RejectKind.None,
                certificateDetail: CertificateRejectDetail.None,
                authoritySource: authoritySource,
                attemptedReplayCertificateReuse: attemptedReplayCertificateReuse);
        }

        public static LegalityDecision Reject(
            RejectKind rejectKind,
            CertificateRejectDetail certificateDetail,
            LegalityAuthoritySource authoritySource,
            bool attemptedReplayCertificateReuse)
        {
            return new LegalityDecision(
                isAllowed: false,
                rejectKind: rejectKind,
                certificateDetail: certificateDetail,
                authoritySource: authoritySource,
                attemptedReplayCertificateReuse: attemptedReplayCertificateReuse);
        }
    }

    /// <summary>
    /// Staged normative status for compiler-emitted typed-slot facts.
    /// This is an architectural vocabulary surface; only
    /// <see cref="ValidationOnly"/> is active in the current mainline.
    /// </summary>
    public enum TypedSlotFactMode : byte
    {
        /// <summary>
        /// Facts are validated and compared when present, but canonical runtime
        /// execution remains correct and fail-closed without them.
        /// </summary>
        ValidationOnly = 0,

        /// <summary>
        /// Future mode: missing facts would surface an explicit warning or
        /// diagnostic, but admission would still remain runtime-owned.
        /// </summary>
        WarnOnMissing = 1,

        /// <summary>
        /// Future stricter mode: missing or invalid facts would fail before the
        /// typed-slot admission path is allowed to proceed.
        /// </summary>
        RequiredForAdmission = 2
    }

    /// <summary>
    /// Canonical repository-facing staging surface for typed-slot facts.
    /// Current code remains in <see cref="TypedSlotFactMode.ValidationOnly"/>;
    /// the stronger modes are declared here so docs and tests can describe the
    /// staged path without pretending it already landed.
    /// </summary>
    public static class TypedSlotFactStaging
    {
        public static TypedSlotFactMode CurrentMode => TypedSlotFactMode.ValidationOnly;

        public static bool AllowsCanonicalExecutionWithoutFacts =>
            CurrentMode != TypedSlotFactMode.RequiredForAdmission;
    }

    /// <summary>
    /// Runtime-local legality checker seam for scheduler and replay/certificate hot paths.
    /// This is intentionally separate from <see cref="YAKSys_Hybrid_CPU.Core.Legality.ILegalityChecker"/>,
    /// which serves the older decoder/instruction legality layer.
    /// </summary>
    internal interface ILegalityChecker
    {
        bool IsKernelDomainIsolationEnabled { get; }

        InterCoreDomainGuardDecision EvaluateInterCoreDomainGuard(
            MicroOp candidate,
            ulong requestedDomainTag);

        TypedSlotRejectClassification ClassifyReject(
            TypedSlotRejectReason admissionReject,
            CertificateRejectDetail certDetail,
            SlotClass candidateClass,
            SlotPinningKind pinningKind);

        LegalityDecision EvaluateInterCoreLegality(
            IReadOnlyList<MicroOp?> bundle,
            BundleResourceCertificate bundleCertificate,
            PhaseCertificateTemplateKey liveTemplateKey,
            PhaseCertificateTemplate phaseTemplate,
            int bundleOwnerThreadId,
            ulong requestedDomainTag,
            MicroOp candidate,
            SafetyMask128 globalHardwareMask = default);

        LegalityDecision EvaluateSmtBoundaryGuard(PhaseCertificateTemplateKey4Way liveTemplateKey);

        LegalityDecision EvaluateSmtLegality(
            BundleResourceCertificate4Way bundleCertificate,
            PhaseCertificateTemplateKey4Way liveTemplateKey,
            PhaseCertificateTemplate4Way phaseTemplate,
            MicroOp candidate);
    }

    /// <summary>
    /// Internal compatibility-only runtime checker provisioning.
    /// Keeps checker instantiation behind runtime legality service wiring instead of
    /// exposing it as a public scheduler/runtime seam.
    /// </summary>
    internal static class RuntimeLegalityCheckerFactory
    {
        internal static ILegalityChecker CreateCompatibilityDefault()
        {
            return new SafetyVerifier();
        }
    }

    /// <summary>
    /// Verification condition type for formal proofs (Phase 5)
    /// </summary>
    public enum VerificationConditionType
    {
        RegisterIsolation,
        MemoryDomainSeparation,
        FSPNonInterference,
        NoDataRaces,
        PredicateSafety
    }

    /// <summary>
    /// Verification condition for formal proofs (Phase 5)
    /// </summary>
    public struct VerificationCondition
    {
        public VerificationConditionType Type;
        public string Description;
        public bool IsValid;
        public string CounterExample;  // If !IsValid
        public string FormalSpec;      // Z3/SMT formula (optional)

        public VerificationCondition()
        {
            Type = VerificationConditionType.RegisterIsolation;
            Description = "";
            IsValid = true;
            CounterExample = "";
            FormalSpec = "";
        }
    }

    /// <summary>
    /// Formal verification context (Phase 5)
    /// </summary>
    public struct VerificationContext
    {
        public Dictionary<int, MemoryDomain> ThreadMemoryDomains;
        public List<VerificationCondition> GeneratedConditions;
        public bool EnableFormalChecks;

        public VerificationContext()
        {
            ThreadMemoryDomains = new Dictionary<int, MemoryDomain>();
            GeneratedConditions = new List<VerificationCondition>();
            EnableFormalChecks = false;
        }
    }

    /// <summary>
    /// Security context for bundle verification
    /// Defines resource boundaries and access policies for a thread/process
    /// (Phase 2: Formal Resource Proofs)
    /// </summary>
    public struct SecurityContext
    {
        /// <summary>
        /// Minimum accessible memory address
        /// </summary>
        public ulong MinAddr;

        /// <summary>
        /// Maximum accessible memory address
        /// </summary>
        public ulong MaxAddr;

        /// <summary>
        /// Bitmask of active hardware threads
        /// </summary>
        public uint ActiveThreads;

        /// <summary>
        /// Thread ID that owns this security context
        /// </summary>
        public int OwnerThreadId;

        public SecurityContext()
        {
            MinAddr = 0;
            MaxAddr = 0;
            ActiveThreads = 0;
            OwnerThreadId = 0;
        }
    }

    /// <summary>
    /// Safety Verifier for Formally Safe Packing (FSP).
    ///
    /// Purpose: Verify that injecting a micro-operation from one thread into another thread's
    /// VLIW bundle does not violate non-interference constraints.
    ///
    /// Non-interference contract:
    /// 1. State isolation: Registers/CSR/PC are logically separate per thread
    /// 2. Memory domain separation: IOMMU enforces memory access boundaries
    /// 3. Per-thread in-order commit: Injection doesn't break commit order within a thread
    /// 4. No cross-thread hazards: No RAW/WAW/WAR conflicts between threads in same bundle
    /// 5. Control/system ops non-stealable: Privileged operations stay in original thread
    ///
    /// Verification algorithm:
    /// - Check register dependencies (RAW/WAW/WAR)
    /// - Check memory range conflicts (overlap detection)
    /// - Check operation type restrictions (control flow, CSR, etc.)
    /// - Check capability domains (IOMMU/protection domains)
    ///
    /// Phase 5 additions:
    /// - Formal verification condition generation
    /// - SMT-LIB2 proof export
    /// - Memory domain verification
    ///
    /// Phase 2 additions:
    /// - Hardware Root of Trust (HRoT) proof generation
    /// - Bundle resource proof certificates
    /// - Singularity/SIP-style isolation verification
}
