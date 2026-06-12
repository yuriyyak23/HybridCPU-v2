using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecureComputePhase10ReleaseGateTests
{
    private static readonly Regex[] ForbiddenAffirmativeDocClaims =
    {
        new(@"\bVmxCaps\s+grants\s+SecureCompute\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bVMCS\s+owns\s+secure\s+state\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bVMX\s+activates\s+SecureCompute\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bVmxCaps\.SecureCompute\s*=\s*true\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bVMCS\.SecureState\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bVMCS\s+stores\s+secure\s+state\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bPhase\s+[789]\b.*\bproduction-ready\s+SecureCompute\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bPhase\s+[789]\b.*\bfeature-complete\s+SecureCompute\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bPhase\s+[789]\b.*\bpositive\s+secure\s+backend\s+execution\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
    };

    private static readonly Regex[] ForbiddenRouteFencePublicationClaims =
    {
        new(@"\bRuntimeOwnedPublication\b.*\b(authorizes|allows|permits|enables|grants|unlocks)\b.*\b(publication|completion|retire|backend\s+success)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(TrapCompletionRouteDescriptor|TrapCompletionRouteService|TrapCompletionPublicationFence|route/fence\s+classes|route\s+classes|publication\s+classes)\b.*\b(authorizes|allows|permits|enables|grants|unlocks)\b.*\b(publication|completion|retire|backend\s+success)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(class\s+presence|class\s+existence|descriptor\s+existence|fence\s+existence|route\s+existence)\b.*\b(authorizes|allows|permits|enables|grants|unlocks)\b.*\b(publication|completion|retire|backend\s+success)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(route/fence\s+classes|route\s+classes|publication\s+classes)\b.*\b(equal|are)\b.*\b(publication\s+permission|publication\s+authority|retire\s+permission|completion\s+authority)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
    };

    private static readonly Regex[] ForbiddenPublicationMatrixCollapseClaims =
    {
        new(@"\b(completion\s+publication|completion\s+fence|completion\s+record|CanPublishCompletion)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be|means|equals|substitutes\s+for|replaces)\b.*\b(retire\s+publication|retire\s+authority|production\s+activation|activation\s+evidence|backend\s+success)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(retire\s+publication|retire\s+fence|CanPublishRetire)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be|means|equals|substitutes\s+for|replaces)\b.*\b(production\s+activation|activation\s+evidence|backend\s+execution|backend\s+success)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(internal\s+backend\s+result|backend\s+result|route\s+authorization|completion\s+record)\b.*\b(publishes|retires|completes|authorizes|allows|permits|enables|grants|opens|unlocks|activates|means|equals)\b.*\b(completion\s+publication|retire\s+publication|production\s+activation|activation\s+evidence)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(proof-only\s+owner|proof-only\s+owner\s+accepted|admitted-denied\s+hypercall|VMCALL\s+decode/projection|VMCALL\s+projection)\b.*\b(publishes|retires|completes|authorizes|allows|permits|enables|grants|opens|unlocks|activates|means|equals)\b.*\b(completion\s+publication|retire\s+publication|backend\s+success|production\s+activation)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
    };

    private static readonly Regex[] ForbiddenVmreadSchemaReadableClaims =
    {
        new(@"\b(VmcsFieldProjectionSchema|generated\s+schema|schema\s+entry|ReadOnly\s+entry|ReadOnly)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|means|equals)\b.*\b(current\s+readable|readable\s+now|readable\s+value|VMREAD\s+value|value\s+projection|current\s+value)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(VmcsReadOnlyValueProjectionService|CompatibilityControlDescriptor)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|means|equals)\b.*\b(current\s+readable|readable\s+now|frozen\s+VMX\s+control|VMREAD\s+value|value\s+projection|current\s+value)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(GuestCr0|GuestCr4)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|is|are|becomes|become)\b.*\b(VMREAD|readable|current\s+value|value\s+projection)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(neutral\s+privileged\s+execution-state\s+owner\s+RFC|neutral\s+privileged\s+execution-state\s+owner)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks)\b.*\b(GuestCr0|GuestCr4|CR0|CR4|VMREAD)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(VmcsReadOnlyValueProjectionService|value-source\s+evaluator)\b.*\b(projects|publishes|opens|allows|authorizes|enables|grants|means|equals)\b.*\b(every\s+schema\s+entry|all\s+schema\s+entries|every\s+ReadOnly|all\s+ReadOnly|GuestCr0|GuestCr4|compatibility-control\s+values?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(neutral\s+privileged\s+execution-state\s+owner\s+RFC|owner\s+RFC)\b.*\b(is|are|becomes|become|counts\s+as|satisfies|bypasses|opens|allows|authorizes|enables|grants)\b.*\b(VMREAD\s+opening|GuestCr0\s+opening|GuestCr4\s+opening|CR0\s+value|CR4\s+value|current\s+readable\s+value|value\s+projection)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(CompatibilityControlDescriptor|compatibility-control\s+schema|compatibility-control\s+fields?)\b.*\b(projects|publishes|opens|allows|authorizes|enables|grants|means|equals)\b.*\b(frozen\s+control-bit\s+values?|current\s+readable\s+values?|VMREAD\s+values?|value\s+projection)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
    };

    private static readonly Regex[] ForbiddenStreamLaneAuthorityClaims =
    {
        new(@"\b(Stream/Lane6/L7|Stream|Lane6|Lane7|DmaStreamComputeDescriptorParser|ExecutionEnabled)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become)\b.*\b(SecureCompute\s+authority|virtualization\s+authority|VMX\s+authority|secure\s+backend|backend\s+execution|SecureCompute\s+activation|virtualization\s+backend)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(DmaStreamComputeDescriptorParser\.ExecutionEnabled|ExecutionEnabled)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|means|equals)\b.*\b(SecureCompute|VMX|virtualization|secure\s+backend|backend\s+execution)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(bounded\s+execution|bounded\s+contours|compiler\s+contours|runtime/compiler\s+contours)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|means|equals)\b.*\b(SecureCompute|VMX|virtualization|secure\s+backend|backend\s+execution)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
    };

    private static readonly Regex[] ForbiddenSecureIoLaneAuthorityClaims =
    {
        new(@"\b(secure\s+I/O|secure\s+IO|SecureIoDomainDescriptor|SecureSharedBufferDescriptor|shared-buffer\s+descriptors?|shared\s+buffer\s+descriptors?|ExplicitSharedBuffer|hypercall\s+shared-buffer\s+arguments?|SecureIoHypercallAdmissionPolicy|AdmitIoDma)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be|means|equals)\b.*\b(raw\s+pointer\s+admission|raw\s+guest\s+pointers?|raw\s+private\s+pointers?|host/device\s+pointer\s+authority|host\s+pointer\s+authority|device\s+pointer\s+authority|VMX\s+authority|VMCALL\s+authority|secure\s+backend|backend\s+execution|backend\s+success|device-side-effect\s+success|production\s+activation\s+evidence|activation\s+evidence)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(materialized\s+shared-buffer|materialized\s+shared\s+buffer|shared-buffer\s+ID|SharedBufferId|buffer\s+ID|buffer\s+id)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be|means|equals)\b.*\b(raw\s+pointer\s+admission|secure\s+I/O\s+authority|secure\s+IO\s+authority|SecureCompute\s+authority|backend\s+execution|backend\s+success|VMX\s+authority|VMCALL\s+authority)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(completion\s+fence|retire\s+fence|completion/retire\s+fences?|SecureCompletionPublicationFence)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be|means|equals)\b.*\b(device-side-effect\s+success|I/O\s+side-effect\s+success|backend\s+success|execution\s+success|secure\s+I/O\s+execution|secure\s+IO\s+execution|backend\s+execution)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
    };

    private static readonly Regex[] ForbiddenHypercallTrapRecognitionAuthorityClaims =
    {
        new(@"\b(AllowedAdmittedDenied|admitted-denied\s+secure\s+hypercall|admitted-denied\s+hypercall|secure\s+hypercall\s+recognition|hypercall\s+recognition|VMCALL\s+decode|VMCALL\s+projection|VMCALL\s+compatibility\s+trap\s+projection|VMX\s+trap\s+projection|TrapDecision|VmExitReason\.VmCall|VmExitReason\.VMCALL|route\s+descriptors?|publication\s+fences?)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be|means|equals|publishes|retires|completes)\b.*\b(backend\s+execution|secure\s+backend|backend\s+success|runtime\s+execution|completion\s+publication|retire\s+publication|completion\s+authority|retire\s+authority|production\s+activation|activation\s+evidence|production\s+evidence)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(CompletionPublicationAuthorized|RetirePublicationAuthorized|completion\s+publication|retire\s+publication)\b.*\b(is|are|becomes|become|counts\s+as|may\s+be|can\s+be|means|equals|publishes|retires|completes)\b.*\b(AllowedAdmittedDenied|admitted-denied\s+secure\s+hypercall|admitted-denied\s+hypercall|VMCALL\s+projection|VMCALL\s+compatibility\s+trap\s+projection|TrapDecision|VmExitReason\.VmCall)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(RuntimeOwnedPublication|TrapCompletionRouteDescriptor|TrapCompletionRouteService|TrapCompletionPublicationFence)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be|means|equals)\b.*\b(VMCALL\s+backend|secure\s+hypercall\s+backend|backend\s+execution|backend\s+success|completion\s+publication|retire\s+publication|production\s+activation)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
    };

    private static readonly Regex[] ForbiddenMigrationCheckpointAuthorityClaims =
    {
        new(@"\b(host-owned\s+evidence|host\s+owned\s+evidence|VMCS\s+projection\s+metadata|compatibility\s+projection\s+metadata|raw\s+measurement\s+secrets?|raw\s+sealing\s+keys?|active\s+host\s+pointers?)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|is|are|becomes|become|counts\s+as|may\s+be|can\s+be)\b.*\b(SecureCompute\s+migration\s+authority|migration\s+authority|checkpoint\s+authority|guest\s+state|migration\s+payload|checkpoint\s+payload|restore\s+payload|serializ(?:e|ed|able|ation))\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(SecureCheckpointPayloadPolicy|SecureMigrationAdmissionPolicy|SecureMigrationDescriptor)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|serializ(?:es|ed|able))\b.*\b(host-owned\s+evidence|VMCS\s+projection\s+metadata|compatibility\s+projection\s+metadata|raw\s+measurement\s+secrets?|raw\s+sealing\s+keys?|active\s+host\s+pointers?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(migration|checkpoint|restore)\b.*\b(serializ(?:es|ed|able)|carries|restores|publishes|trusts)\b.*\b(host-owned\s+evidence|VMCS\s+projection\s+metadata|compatibility\s+projection\s+metadata|raw\s+measurement\s+secrets?|raw\s+sealing\s+keys?|active\s+host\s+pointers?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(restore\s+validation|AdmitRestore|SecureRestore|migration-serializable\s+evidence|recomputed-after-restore\s+evidence|host-owned\s+evidence)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be|means|equals|publishes|retires|completes|satisfies|bypasses)\b.*\b(guest/runtime\s+authority|guest\s+authority|runtime\s+authority|guest-visible\s+publication|completion\s+publication|retire\s+publication|production\s+activation|activation\s+evidence|backend\s+success)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(completion\s+records?|compatibility\s+projection\s+values?|recomputed\s+completion\s+values?)\b.*\b(serializ(?:es|ed|able)|imports?|restores?|publishes|trusts|authorizes|allows|permits|enables|grants|opens|unlocks|becomes|become|counts\s+as|means|equals)\b.*\b(checkpoint\s+payload|restore\s+payload|migration\s+authority|guest/runtime\s+authority|guest\s+authority|runtime\s+authority|production\s+activation)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
    };

    private static readonly Regex[] ForbiddenNestedDesignFenceAuthorityClaims =
    {
        new(@"\b(nested\s+child\s+intent|child\s+intent|SecureChildDomainIntentDescriptor)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be)\b.*\b(nested\s+backend\s+execution|backend\s+execution|secure\s+backend|mutable\s+nested\s+secure\s+state|migration\s+authority|checkpoint\s+authority|VMCS12|VMCS02|Shadow\s+VMCS\s+authority|shadow-VMCS\s+authority)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(parent/child\s+monotonicity|parent-child\s+monotonicity|parent\s+child\s+monotonicity|parent-child\s+proof|monotonic\s+derivation)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be)\b.*\b(nested\s+backend\s+execution|backend\s+execution|mutable\s+nested\s+secure\s+state|nested\s+execution|migration\s+authority|checkpoint\s+authority)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(nested\s+checkpoint|nested\s+checkpoint\s+payload|NestedDomainProjectionCheckpointService)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|serializ(?:es|ed|able)|is|are|becomes|become|counts\s+as|may\s+be|can\s+be)\b.*\b(nested\s+backend\s+execution|backend\s+execution|mutable\s+nested\s+secure\s+state|VMCS12|VMCS02|Shadow\s+VMCS|shadow-VMCS|migration\s+authority|checkpoint\s+authority)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(VMCS12|VMCS02|Shadow\s+VMCS|shadow-VMCS|mutable\s+Shadow\s+VMCS)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be)\b.*\b(nested\s+secure\s+authority|nested\s+authority|SecureCompute\s+authority|migration\s+authority|checkpoint\s+authority|mutable\s+nested\s+secure\s+state|nested\s+backend\s+execution|backend\s+execution)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(SecureNestedDomainAdmissionPolicy|AllowedDesignFence|AllowedNoEffect)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be)\b.*\b(nested\s+backend\s+execution|backend\s+execution|secure\s+backend|mutable\s+nested\s+secure\s+state|migration\s+authority|checkpoint\s+authority)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(general\s+activation|SecureCompute\s+activation|activation)\b.*\b(implies|opens|activates|authorizes|allows|permits|enables|grants)\b.*\b(nested\s+readiness|nested\s+execution|nested\s+SecureCompute)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(SecureNestedDomainAdmissionPolicy|AllowedDesignFence|AllowedNoEffect|child\s+intent\s+descriptors?|nested\s+projection/checkpoint\s+services?|NestedProjectionService|NestedDomainProjectionCheckpointService)\b.*\b(implements|implementation\s+of|authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be|means|equals)\b.*\b(nested\s+SecureCompute\s+execution|nested\s+backend\s+execution|backend\s+execution|secure\s+backend\s+execution|mutable\s+nested\s+secure\s+state|production\s+backend\s+execution)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(nested\s+evidence|nested\s+telemetry|nested\s+checkpoint\s+facts?|nested\s+checkpoint\s+evidence|nested\s+projection\s+facts?|nested\s+compatibility\s+projection\s+facts?)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be|means|equals|imports?|publishes|serializ(?:es|ed|able))\b.*\b(guest/runtime\s+authority|guest\s+authority|runtime\s+authority|nested\s+backend\s+execution|backend\s+execution|mutable\s+nested\s+secure\s+state|production\s+activation|activation\s+evidence|production\s+evidence)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
    };

    private static readonly Regex[] ForbiddenNestedProductionAuthorityShortcuts =
    {
        new(@"\bBackendExecutionAuthorized:\s*true\b", RegexOptions.CultureInvariant),
        new(@"\bNestedBackendExecution\b", RegexOptions.CultureInvariant),
        new(@"\bMutableNestedStateAuthorized:\s*true\b", RegexOptions.CultureInvariant),
        new(@"\bVMCS12 authority\b", RegexOptions.CultureInvariant),
        new(@"\bVMCS02 authority\b", RegexOptions.CultureInvariant),
        new(@"\bShadowVmcsAuthority\b", RegexOptions.CultureInvariant),
        new(@"\bRuntimeOwnedPublication\b", RegexOptions.CultureInvariant),
        new(@"\bCompletionPublicationAuthorized:\s*true\b", RegexOptions.CultureInvariant),
        new(@"\bRetirePublicationAuthorized:\s*true\b", RegexOptions.CultureInvariant),
    };

    private static readonly Regex[] ForbiddenPositiveRuntimeExecutionAuthorityClaims =
    {
        new(@"\b(SecureBackendOwnerRfcGate|SecureBackendOwnerAdmissionPolicy|SecureBackendOwnerDescriptor|AllowedProofOnlyNoExecution|proof-only|proof\s+only|no-effect\s+evidence|no\s+effect\s+evidence|backend-owner\s+wording|backend\s+owner\s+wording|backend\s+owner\s+proof|neutral\s+backend\s+owner\s+proof)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be|means|equals)\b.*\b(secure\s+backend\s+execution|positive\s+secure\s+backend|backend\s+execution|backend\s+success|runtime\s+execution|completion\s+publication|retire\s+publication|production\s+activation|production-ready|feature-complete|activation\s+claim)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(SecureBackendOwnerRfcGate|SecureBackendOwnerAdmissionPolicy|AllowedProofOnlyNoExecution|proof-only|proof\s+only|backend\s+owner\s+proof|neutral\s+backend\s+owner\s+proof)\b.*\b(publishes|published|retires|retired|completes|completed|emits|emitted)\b.*\b(backend\s+success|completion|retire|publication|runtime\s+result|secure\s+result)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(RFC/ADR\s+gate|backend\s+owner\s+RFC|owner\s+gate|Phase\s+20\s+RFC)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be|means|equals)\b.*\b(secure\s+backend\s+execution|backend\s+execution|completion\s+publication|retire\s+publication|production\s+activation|activation\s+claim)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(ProofChainAccepted|proof\s+chain\s+accepted|approved\s+RFC/ADR|approved\s+RFC|approved\s+ADR)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be|means|equals)\b.*\b(secure\s+backend\s+execution|backend\s+execution|completion\s+publication|retire\s+publication|production\s+activation|activation\s+claim)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(20A|20B|20C|20D|20E|Phase\s+20\s+subphase\s+labels?|subphase\s+labels?|owner\s+descriptor\s+materialization|owner\s+materialization|SecureBackendOwnerDescriptor)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be|means|equals|satisfies|bypasses)\b.*\b(typed\s+execution|typed\s+execution\s+request/result|SecureBackendExecutionRequest|SecureBackendExecutionDecision|backend\s+execution|completion\s+record|completion\s+publication|retire\s+publication|nested\s+execution|production\s+activation|activation\s+evidence)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(SecureBackendExecutionRequest|SecureBackendExecutionDecision|AllowedInternalExecutionNoPublication|AllowedCompletionRecordNoPublication|SecureCompletionRecord|NoSideEffectProbe|typed\s+execution\s+vocabulary|completion-record\s+vocabulary|retire-publication\s+vocabulary)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be|means|equals|satisfies|bypasses)\b.*\b(backend\s+execution|completion\s+publication|retire\s+publication|nested\s+execution|production\s+activation|activation\s+evidence)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
    };

    private static readonly Regex[] ForbiddenEvidenceDebugAttestationAuthorityClaims =
    {
        new(@"\b(guest-visible\s+evidence|guest\s+visible\s+evidence|debug\s+traces?|debug-only\s+evidence|attestation\s+facts?|attestation\s+output|attestation\s+evidence|host-owned\s+evidence|host\s+owned\s+evidence|recomputed(?:-after-restore)?\s+evidence|recomputed\s+evidence|test\s+telemetry)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be|means|equals)\b.*\b(runtime\s+authority|SecureCompute\s+authority|VMREAD\s+authority|VMREAD\s+value|VMCS\s+state|VmxCaps\s+grant|migration\s+authority|checkpoint\s+authority|completion\s+publication|backend\s+success|runtime\s+execution|production\s+activation|activation\s+evidence|production\s+evidence)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(DomainMeasurementDescriptor|SecureEvidencePolicy|SecureEvidencePublicationPolicy|SecureEvidenceVisibilityClass|GuestVisible|DebugOnly|HostOwnedQuarantined|RecomputedAfterRestore)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be|means|equals)\b.*\b(runtime\s+authority|VMREAD\s+authority|migration\s+authority|completion\s+publication|production\s+activation|activation\s+evidence|backend\s+success|runtime\s+execution)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(debug\s+traces?|attestation\s+facts?|attestation\s+output|host-owned\s+evidence|recomputed(?:-after-restore)?\s+evidence|test\s+telemetry)\b.*\b(publishes|published|retires|retired|completes|completed|serializ(?:es|ed|able)|migrates|migrated|VMREADs?|projects)\b.*\b(completion|retire|migration\s+payload|checkpoint\s+payload|VMREAD|VMCS|VmxCaps|runtime\s+authority|activation)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(guest-visible\s+evidence|GuestVisible|debug-only\s+evidence|DebugOnly|attestation\s+evidence)\b.*\b(bypasses|satisfies|replaces)\b.*\b(completion\s+fence|retire\s+fence|migration\s+classification|neutral\s+backend\s+owner|runtime\s+admission)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
    };

    private static readonly Regex[] ForbiddenMemoryPrivateDomainAuthorityClaims =
    {
        new(@"\b(private\s+memory\s+descriptors?|shared\s+memory\s+descriptors?|measured\s+memory\s+descriptors?|SecureMemoryDomainDescriptor|SecureMemoryAdmissionPolicy|host-inspection\s+metadata|host\s+inspection\s+metadata|SecureHostInspectionPolicy|runtime-dirty\s+classes?|runtime\s+dirty\s+classes?|runtime-migration\s+classes?|runtime\s+migration\s+classes?|SecureRuntimeMutableMigrationClass|SecureRuntimeMutableDirtyPolicy)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be|means|equals)\b.*\b(hardware\s+tags?|hardware\s+memory\s+tags?|tagged-memory\s+semantics|tagged\s+memory|raw\s+pointer\s+admission|raw\s+pointers?|CHERI-like\s+ISA|CHERI\s+ISA|CHERI-style\s+memory|CHERI/capability-aware\s+semantics|capability-aware\s+LOAD|capability-aware\s+STORE|capability-aware\s+FETCH|capability-bearing\s+operands?|capability\s+registers?|per-pointer\s+provenance|pointer-level\s+authority|VMX\s+EPT|VMX\s+VPID|VMX\s+NPT|EPT/VPID(?:/NPT)?\s+authority|NPT\s+authority|VMREAD\s+authority|VMWRITE\s+authority|migration\s+authority|production\s+activation\s+evidence|activation\s+evidence)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(sealed\s+payload\s+contract|policy-sealed\s+checkpoint\s+payload\s+contract|migration-sealed\s+payload\s+contract|SecurePrivateMemorySealedPayloadContract)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be|means|equals)\b.*\b(CHERI\s+sealing|CHERI\s+sealed\s+capability|sealed\s+capabilit(?:y|ies)|architectural\s+sealed\s+capability|sealed\s+capability\s+object|pointer-level\s+authority|capability\s+tags?|tag/provenance\s+payload|hardware\s+tags?|migration\s+authority|production\s+activation\s+evidence|activation\s+evidence)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(memory\s+descriptors?|memory\s+policy|private/shared/measured|runtime-mutable\s+memory|host-inspection\s+metadata|sealed\s+payload|runtime-dirty|runtime\s+dirty|migration\s+classes?)\b.*\b(implements|adds|introduces|creates|emits|changes)\b.*\b(CHERI|tagged\s+memory|hardware\s+tags?|capability-aware\s+LOAD|capability-aware\s+STORE|capability-aware\s+FETCH|capability\s+operands?|capability\s+registers?|new\s+ISA\s+encodings?|new\s+operand\s+formats?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(migration\s+payload|checkpoint\s+payload|restore\s+payload)\b.*\b(serializ(?:es|ed|able)|contains|carries|restores|publishes|trusts)\b.*\b(hardware\s+tags?|tag/provenance|tagged-memory|capability\s+tags?|CHERI\s+capabilit(?:y|ies)|host-owned\s+evidence|raw\s+host\s+evidence|raw\s+sealing\s+keys?|raw\s+measurement\s+secrets?|raw\s+secrets?|active\s+host\s+pointers?|VMCS\s+projection\s+metadata|compatibility\s+projection\s+metadata|migration\s+authority|guest/runtime\s+authority|guest\s+authority|runtime\s+authority)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(EPT|VPID|NPT|EPT/VPID|EPT/VPID/NPT|VMX\s+artifacts?|VMX\s+EPT|VMX\s+VPID|VMX\s+NPT|VMREAD\s+schema|VMREAD\s+value\s+projection|VMREAD\s+schema/value\s+projection)\b.*\b(authorizes|allows|permits|enables|grants|opens|unlocks|activates|is|are|becomes|become|counts\s+as|may\s+be|can\s+be|means|equals)\b.*\b(SecureCompute\s+memory\s+authority|secure\s+memory\s+authority|SecureCompute\s+authority|memory\s+authority)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
    };

    private static readonly string[] ForbiddenLayerOneTwoSourceTokens =
    {
        "VmxCaps.Secure",
        "VmcsManager",
        "IVmcsManager",
        "VmxExecutionUnit",
        "ReadFieldValue(",
        "WriteFieldValue(",
        "BackendExecutionAuthorized: true",
        "MutableNestedStateAuthorized: true",
        "InstructionEncoding",
        "Decoder",
        "Encoder",
        "OperandForm",
        "AddressingMode",
        "CapabilityAware",
        "CapabilityRegister",
        "CapabilityRegisters",
        "CapabilityOperand",
        "CapabilityMetadata",
        "TaggedMemory",
        "TagProvenance",
        "ProvisionalTag",
        "TagCheckpoint",
        "ProvenanceCheckpoint",
    };

    [Fact]
    public void Phase10Plans_RecordReleaseGateStatusAndHardeningInvariants()
    {
        string plan10 = ReadPlan("10-nested-secure-domain-plan.md");
        string plan11 = ReadPlan("11-test-and-conformance-master-plan.md");
        string plan12 = ReadPlan("12-phasing-and-pr-breakdown.md");
        string plan13 = ReadPlan("13-future-capability-aware-isa-memory-layer.md");

        Assert.Contains("Design fence only: this plan does not implement nested secure backend entry", plan10);
        Assert.Contains("does not authorize backend success", plan10);
        Assert.Contains("Phase 10 release gate closed on 2026-05-31", plan10);

        Assert.Contains("Positive policy-admission tests:", plan11);
        Assert.Contains("Positive runtime-execution tests:", plan11);
        Assert.Contains("not opened for secure backend execution", plan11);
        Assert.Contains("Phase 10 conformance hardening and stale-doc cleanup closed on 2026-05-31", plan11);

        Assert.Contains("Files 11-13 are meta/future documents, not implementation phases 11-13", plan12);
        Assert.Contains("Phase 10 - conformance hardening and stale-doc cleanup release gate closed on 2026-05-31", plan12);
        Assert.Contains("Mandatory gates:", plan12);
        Assert.Contains("status-label audit", plan12);
        Assert.Contains("production-claim audit", plan12);
        Assert.Contains("Phase 7: runtime descriptor/grant discipline + negative conformance; not CHERI ISA.", plan12);
        Assert.Contains("Phase 8: VMX deny/projection matrix + negative conformance; not secure VMCS.", plan12);
        Assert.Contains("Phase 9: design fence + negative conformance; not nested secure execution.", plan12);

        Assert.Contains("Import ban: current Layer 1/Layer 2 product code must not reference future capability-aware types", plan13);
        Assert.Contains("Lane6/Lane7 paths may carry only neutral descriptor/evidence envelopes", plan13);
        Assert.Contains("Any capability-aware ISA or memory change requires a new RFC / architecture decision record", plan13);
        Assert.Contains("current SecureCompute migration descriptors must not add provisional tag/provenance checkpoint fields", plan13);
        Assert.Contains("Phase 10 release gate closed on 2026-05-31", plan13);
    }

    [Fact]
    public void SecureComputeDocs_DoNotMakeAffirmativeVmxOrProductionClaims()
    {
        List<string> failures = new();
        foreach (string path in EnumerateSecureComputeMarkdownSources())
        {
            string[] lines = File.ReadAllLines(path);
            for (int index = 0; index < lines.Length; index++)
            {
                foreach (Regex pattern in ForbiddenAffirmativeDocClaims)
                {
                    if (!pattern.IsMatch(lines[index]) ||
                        IsForbiddenOrNegativeContext(lines, index))
                    {
                        continue;
                    }

                    failures.Add($"{Relative(path)}:{index + 1}: {lines[index].Trim()}");
                }
            }
        }

        Assert.True(
            failures.Count == 0,
            "Forbidden affirmative SecureCompute documentation claims:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void SecureComputeRefactoringNewDocs_DoNotTreatRouteFenceInfrastructureAsPublicationPermission()
    {
        List<string> failures = new();
        foreach (string path in EnumerateSecureComputeRefactoringNewMarkdownSources())
        {
            string[] lines = File.ReadAllLines(path);
            for (int index = 0; index < lines.Length; index++)
            {
                foreach (Regex pattern in ForbiddenRouteFencePublicationClaims)
                {
                    if (!pattern.IsMatch(lines[index]) ||
                        IsForbiddenOrNegativeContext(lines, index))
                    {
                        continue;
                    }

                    failures.Add($"{Relative(path)}:{index + 1}: {lines[index].Trim()}");
                }
            }
        }

        Assert.True(
            failures.Count == 0,
            "Forbidden route/fence publication-permission wording in SecureComputerefactoringNew:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));

        string phase16 = ReadSecureComputeRefactoringNew("16_completion_and_retire_publication.md");
        string phase21 = ReadSecureComputeRefactoringNew("21_release_gate_and_activation_checklist.md");

        Assert.Contains("does not mean publication is permitted", phase16);
        Assert.Contains("permission is a decision produced only after runtime admission", phase16);
        Assert.Contains("No route descriptor or publication fence class as permission by existence", phase16);
        Assert.Contains("route/fence classes do not equal publication permission", phase21);
        Assert.Contains("`RuntimeOwnedPublication` is blocked before real neutral backend owner", phase21);
    }

    [Fact]
    public void SecureComputeRefactoringNewDocs_DoNotCollapseCompletionRetirePublicationMatrix()
    {
        List<string> failures = FindForbiddenSecureComputeRefactoringNewWording(
            ForbiddenPublicationMatrixCollapseClaims);

        Assert.True(
            failures.Count == 0,
            "Forbidden completion/retire publication matrix wording in SecureComputerefactoringNew:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));

        string phase16 = ReadSecureComputeRefactoringNew("16_completion_and_retire_publication.md");
        string phase21 = ReadSecureComputeRefactoringNew("21_release_gate_and_activation_checklist.md");

        Assert.Contains("| Proof-only owner accepted | no | no | no | proof evidence only |", phase16);
        Assert.Contains("| Admitted-denied hypercall recognized | no | no | no | recognition only |", phase16);
        Assert.Contains("| VMCALL decode/projection or `TrapDecision` | no | no | no | compatibility projection only |", phase16);
        Assert.Contains("| `RuntimeOwnedPublication` descriptor present without backend execution | no | no | no | infrastructure only |", phase16);
        Assert.Contains("| Internal backend result before completion fence | internal only | no | no | not guest-visible success |", phase16);
        Assert.Contains("| Completion fence only | requires backend execution | yes | no | completion is not retire |", phase16);
        Assert.Contains("| Explicit retire fence after completion | requires backend execution | yes | yes | still not production activation by itself |", phase16);
        Assert.Contains("Completion publication, retire publication and production activation are not interchangeable states", phase16);
        Assert.Contains("No completion fence as retire publication authority", phase16);
        Assert.Contains("completion publication, retire publication, route authorization, completion records and production activation remain separate states and do not substitute for each other", phase21);
    }

    [Fact]
    public void SecureComputeRefactoringNewDocs_DoNotTreatVmreadSchemaAsCurrentReadableValues()
    {
        List<string> failures = FindForbiddenSecureComputeRefactoringNewWording(
            ForbiddenVmreadSchemaReadableClaims);

        Assert.True(
            failures.Count == 0,
            "Forbidden VMREAD schema/readable-value wording in SecureComputerefactoringNew:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));

        string index = ReadSecureComputeRefactoringNew("00_securecompute_refactoring_plan_index.md");
        string phase18 = ReadSecureComputeRefactoringNew("18_vmx_compatibility_deny_projection.md");
        string phase20 = ReadSecureComputeRefactoringNew("20_positive_runtime_execution_rfc_gate.md");
        string phase21 = ReadSecureComputeRefactoringNew("21_release_gate_and_activation_checklist.md");

        Assert.Contains("not current readable values by themselves", index);
        Assert.Contains("an entry in `VmcsFieldProjectionSchema` is not a current readable value", phase18);
        Assert.Contains("VMREAD projection state matrix:", phase18);
        Assert.Contains("| `VmcsFieldProjectionSchema` entry present | generated compatibility alias metadata exists | current readable value, VMREAD authority or SecureCompute authority |", phase18);
        Assert.Contains("| Schema `ReadOnly` / `CanRead` | alias may be considered by the projection evaluator | value projection, runtime admission or write authority |", phase18);
        Assert.Contains("| `VmcsReadOnlyValueProjectionService` present | fail-closed value-source evaluator exists | broad VMREAD opening or authority for every schema entry |", phase18);
        Assert.Contains("| Runtime admission allowed | projection-only runtime boundary allowed this compatibility read attempt | field value publication without neutral owner/value source |", phase18);
        Assert.Contains("| Read-only value projected | only admitted for a field with neutral owner, value source, evidence policy, migration classification and conformance proof | backend success, migration authority, completion/retire publication or production activation |", phase18);
        Assert.Contains("`GuestCr0`, `GuestCr4`", phase18);
        Assert.Contains("denied until neutral privileged execution-state owner RFC", phase18);
        Assert.Contains("No `GuestCr0`/`GuestCr4` opening without neutral privileged execution-state owner semantics", phase18);
        Assert.Contains("`VmcsReadOnlyValueProjectionService` existence does not project `GuestCr0`, `GuestCr4` or compatibility-control values", phase18);
        Assert.Contains("No `VmcsReadOnlyValueProjectionService` presence as authority to project every generated read-only schema entry", phase18);
        Assert.Contains("Even the `GuestCr0`/`GuestCr4` owner RFC must keep values denied", phase20);
        Assert.Contains("schema entry does not equal current readable VMREAD value", phase21);
        Assert.Contains("`GuestCr0`/`GuestCr4` remain denied until neutral privileged execution-state owner RFC and implementation", phase21);
        Assert.Contains("`VmcsFieldProjectionSchema` entry presence, schema `ReadOnly`/`CanRead`, generated aliases and `VmcsReadOnlyValueProjectionService` presence do not equal current readable VMREAD values or SecureCompute authority", phase21);
        Assert.Contains("a neutral privileged execution-state owner RFC does not open `GuestCr0`/`GuestCr4` until owner semantics, value source, visibility policy, migration classification and conformance tests are implemented", phase21);
    }

    [Fact]
    public void SecureComputeRefactoringNewDocs_DoNotTreatStreamLaneContoursAsSecureComputeOrVirtualizationAuthority()
    {
        List<string> failures = FindForbiddenSecureComputeRefactoringNewWording(
            ForbiddenStreamLaneAuthorityClaims);

        Assert.True(
            failures.Count == 0,
            "Forbidden Stream/Lane6/Lane7 authority wording in SecureComputerefactoringNew:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));

        string index = ReadSecureComputeRefactoringNew("00_securecompute_refactoring_plan_index.md");
        string phase06 = ReadSecureComputeRefactoringNew("06_compiler_isa_vliw_no_emission_boundary.md");
        string phase14 = ReadSecureComputeRefactoringNew("14_io_lane_boundary.md");
        string phase21 = ReadSecureComputeRefactoringNew("21_release_gate_and_activation_checklist.md");

        Assert.Contains("Lane6/Stream/L7 bounded execution contours may exist outside SecureCompute", index);
        Assert.Contains("not SecureCompute, VMX or virtualization authority", index);
        Assert.Contains("Stream/Lane6/L7 compiler contours as hidden SecureCompute preparation", phase06);
        Assert.Contains("No Stream/Lane6/L7 expansion as SecureCompute authority preparation", phase06);
        Assert.Contains("DmaStreamComputeDescriptorParser.ExecutionEnabled", phase14);
        Assert.Contains("not SecureCompute, VMX or virtualization authority", phase14);
        Assert.Contains("No Stream/Lane6/L7 contour as SecureCompute authority by implication", phase14);
        Assert.Contains("Stream/Lane6/L7 bounded contours do not become SecureCompute or virtualization authority", phase21);
    }

    [Fact]
    public void SecureComputeRefactoringNewDocs_DoNotTreatSecureIoLaneAsRawPointerBackendOrVmxAuthority()
    {
        List<string> failures = FindForbiddenSecureComputeRefactoringNewWording(
            ForbiddenSecureIoLaneAuthorityClaims);

        Assert.True(
            failures.Count == 0,
            "Forbidden secure I/O lane authority wording in SecureComputerefactoringNew:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));

        string phase14 = ReadSecureComputeRefactoringNew("14_io_lane_boundary.md");
        string phase21 = ReadSecureComputeRefactoringNew("21_release_gate_and_activation_checklist.md");

        Assert.Contains("Shared-buffer descriptors and hypercall `ExplicitSharedBuffer` arguments are descriptor IDs/ranges with current grants and evidence; they are not raw guest pointers, host/device pointer authority, backend execution proof or production activation evidence", phase14);
        Assert.Contains("A materialized shared-buffer descriptor under denied I/O policy remains denied; hypercall shared-buffer arguments require `SecureIoDmaPolicy.ExplicitSharedBuffersOnly`", phase14);
        Assert.Contains("No materialized shared-buffer ID as admission when I/O policy is denied", phase14);
        Assert.Contains("No hypercall shared-buffer argument as raw pointer, host pointer or device pointer authority", phase14);
        Assert.Contains("secure I/O shared-buffer descriptors and hypercall shared-buffer arguments do not become raw pointer admission, host/device pointer authority, VMX/VMCALL authority, backend execution proof or production activation evidence", phase21);
    }

    [Fact]
    public void SecureComputeRefactoringNewDocs_DoNotTreatHypercallTrapRecognitionAsBackendExecutionOrPublication()
    {
        List<string> failures = FindForbiddenSecureComputeRefactoringNewWording(
            ForbiddenHypercallTrapRecognitionAuthorityClaims);

        Assert.True(
            failures.Count == 0,
            "Forbidden hypercall/trap recognition authority wording in SecureComputerefactoringNew:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));

        string phase15 = ReadSecureComputeRefactoringNew("15_hypercall_and_trap_policy.md");
        string phase21 = ReadSecureComputeRefactoringNew("21_release_gate_and_activation_checklist.md");

        Assert.Contains("`AllowedAdmittedDenied`, `TrapDecision`, `VmExitReason.VmCall`, VMCALL decode/projection and route/fence class presence are recognition/projection facts only", phase15);
        Assert.Contains("they do not authorize backend execution, completion publication, retire publication or production activation", phase15);
        Assert.Contains("`CompletionPublicationAuthorized` and `RetirePublicationAuthorized` must remain false on admitted-denied secure hypercall recognition", phase15);
        Assert.Contains("No completion publication or retire publication through admitted-denied secure hypercall", phase15);
        Assert.Contains("No VMCALL decode/projection, `TrapDecision`, route descriptor or publication fence as completion/retire publication authority", phase15);
        Assert.Contains("`AllowedAdmittedDenied`, VMCALL decode/projection, `TrapDecision`, `VmExitReason.VmCall`, route descriptors and publication fences do not become backend execution, completion publication, retire publication or production activation evidence", phase21);
    }

    [Fact]
    public void SecureComputeIoLaneSources_DoNotCreateRawPointerVmxOrBackendAuthority()
    {
        string source = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Io/SecureIoDomainDescriptor.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Hypercalls/SecureHypercallDescriptor.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Io/SecureIoHypercallAdmissionPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Memory/SecureMemoryAdmissionPolicy.cs");

        Assert.Contains("DmaPolicy != SecureIoDmaPolicy.ExplicitSharedBuffersOnly", source);
        Assert.Contains("DeniedRawPrivatePointer", source);
        Assert.Contains("DeniedPrivateMemoryAccess", source);
        Assert.Contains("DeniedSharedBufferBinding", source);
        Assert.Contains("DeniedMissingTypedGrant", source);
        Assert.Contains("DeniedBackendSuccessClosed", source);
        Assert.Contains("BackendExecutionAuthorized: false", source);
        Assert.DoesNotContain("BackendExecutionAuthorized: true", source);
        Assert.DoesNotContain("VmcsField", source);
        Assert.DoesNotContain("VmxCaps", source);
        Assert.DoesNotContain("VMREAD", source);
        Assert.DoesNotContain("VMWRITE", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);
        Assert.DoesNotContain("ReadFieldValue(", source);
        Assert.DoesNotContain("WriteFieldValue(", source);
        Assert.DoesNotContain("IntPtr", source);
        Assert.DoesNotContain("UIntPtr", source);
        Assert.DoesNotContain("HostPointer", source);
        Assert.DoesNotContain("DevicePointer", source);
        Assert.DoesNotContain("RawPointerAllowed", source);
        Assert.DoesNotContain("RawPrivatePointerAllowed", source);
        Assert.DoesNotContain("DangerousGetHandle", source);
    }

    [Fact]
    public void HypercallTrapRecognitionSources_DoNotPublishBackendCompletionOrRetire()
    {
        string secureHypercallSource = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Hypercalls/SecureHypercallDescriptor.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Io/SecureIoHypercallAdmissionPolicy.cs");
        string neutralBackendSource = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Events/Hypercalls/HypercallBackendAdmissionPolicy.cs");
        string vmcallTrapSource = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs");

        Assert.Contains("AllowedAdmittedDenied", secureHypercallSource);
        Assert.Contains("DeniedBackendSuccessClosed", secureHypercallSource);
        Assert.Contains("BackendExecutionAuthorized: false", secureHypercallSource);
        Assert.Contains("CompletionPublicationAuthorized: false", secureHypercallSource);
        Assert.Contains("RetirePublicationAuthorized: false", secureHypercallSource);
        Assert.DoesNotContain("BackendExecutionAuthorized: true", secureHypercallSource);
        Assert.DoesNotContain("CompletionPublicationAuthorized: true", secureHypercallSource);
        Assert.DoesNotContain("RetirePublicationAuthorized: true", secureHypercallSource);

        Assert.Contains("HypercallBackendAdmissionService", neutralBackendSource);
        Assert.Contains("DeniedNeutralBackendOwnerMissing", neutralBackendSource);
        Assert.Contains("BackendExecutionAuthorized: false", neutralBackendSource);
        Assert.DoesNotContain("BackendExecutionAuthorized: true", neutralBackendSource);
        Assert.DoesNotContain("HypercallBackendAdmissionDecision.Allowed", neutralBackendSource);
        Assert.DoesNotContain("VmExitReason", neutralBackendSource);
        Assert.DoesNotContain("TrapDecision", neutralBackendSource);
        Assert.DoesNotContain("VmxCaps", neutralBackendSource);
        Assert.DoesNotContain("VmcsManager", neutralBackendSource);

        Assert.Contains("VmxCompatibilityTrapAdmissionDecision.TrapProjectionDeniedBackend", vmcallTrapSource);
        Assert.Contains("HypercallBackendAdmissionRequest.MissingNeutralOwner", vmcallTrapSource);
        Assert.Contains("TrapCompletionRouteRequest.ProjectionOnlyDenied", vmcallTrapSource);
        Assert.Contains("TrapCompletionRouteService.Default.Authorize", vmcallTrapSource);
        Assert.Contains("TrapCompletionRouteService.Default.EvaluateFence", vmcallTrapSource);
        Assert.DoesNotContain("TrapCompletionRouteDescriptor.RuntimeOwnedPublication", vmcallTrapSource);
        Assert.DoesNotContain("BackendExecutionAuthorized: true", vmcallTrapSource);
        Assert.DoesNotContain("CompletionPublicationAuthorized: true", vmcallTrapSource);
        Assert.DoesNotContain("RetirePublicationAuthorized: true", vmcallTrapSource);
        Assert.DoesNotContain("VmxExecutionUnit", vmcallTrapSource);
        Assert.DoesNotContain("VmcsManager", vmcallTrapSource);
    }

    [Fact]
    public void SecureComputeRefactoringNewDocs_DoNotTreatForbiddenCheckpointPayloadsAsMigrationAuthority()
    {
        List<string> failures = FindForbiddenSecureComputeRefactoringNewWording(
            ForbiddenMigrationCheckpointAuthorityClaims);

        Assert.True(
            failures.Count == 0,
            "Forbidden migration/checkpoint authority wording in SecureComputerefactoringNew:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));

        string index = ReadSecureComputeRefactoringNew("00_securecompute_refactoring_plan_index.md");
        string phase17 = ReadSecureComputeRefactoringNew("17_migration_checkpoint_restore.md");
        string phase21 = ReadSecureComputeRefactoringNew("21_release_gate_and_activation_checklist.md");

        Assert.Contains("Do not serialize host-owned evidence, VMCS metadata, compatibility projection metadata, raw keys, raw measurement secrets or active host pointers as SecureCompute migration authority", index);
        Assert.Contains("must reject host-owned evidence, VMCS metadata, compatibility projection metadata, raw keys, raw measurement secrets and active host pointers", phase17);
        Assert.Contains("VMCS projection metadata is not SecureCompute checkpoint authority", phase17);
        Assert.Contains("Compatibility projection metadata is not migration authority", phase17);
        Assert.Contains("Payload-class policy matrix:", phase17);
        Assert.Contains("| `HostOwnedEvidence`, `SchedulerEvidence`, `BackendBindingEvidence`, `NativeTokenEvidence` | denied host-owned/recomputed evidence | rebuild after restore; never deserialize or publish as guest/runtime authority |", phase17);
        Assert.Contains("| `VmcsProjectionMetadata` | denied VMCS projection metadata | not SecureCompute checkpoint, restore or VMREAD/VMWRITE authority |", phase17);
        Assert.Contains("| `CompatibilityProjectionMetadata` | denied compatibility projection metadata | not migration authority or restored guest/runtime state |", phase17);
        Assert.Contains("| `RawMeasurementSecret` | denied raw secret | never serializable checkpoint payload |", phase17);
        Assert.Contains("| `ActiveHostPointer` | denied active host pointer | never restored or trusted as portable state |", phase17);
        Assert.Contains("| `RawSealingKey` | denied raw key | never serializable checkpoint payload |", phase17);
        Assert.Contains("Restore validation is admission-only", phase17);
        Assert.Contains("it does not publish guest-visible evidence, completion publication, retire publication, compatibility projection values or production activation", phase17);
        Assert.Contains("Migration-serializable evidence is not guest-visible publication, completion publication, retire publication, runtime authority or production activation evidence", phase17);
        Assert.Contains("Recomputed-after-restore evidence is host-side rebuild input only, not serialized guest/runtime authority", phase17);
        Assert.Contains("No host-owned evidence serialization", phase17);
        Assert.Contains("No raw sealing key or raw measurement secret payload", phase17);
        Assert.Contains("No active host pointer restore", phase17);
        Assert.Contains("No restore validation as completion publication, retire publication or production activation evidence", phase17);
        Assert.Contains("No compatibility projection value, completion record or host-owned evidence import as checkpoint/restore authority", phase17);
        Assert.Contains("migration payload rejection of host/VMCS/compat/raw-secret/active-pointer authority", phase21);
        Assert.Contains("migration/checkpoint restore policy keeps guest-visible state, migration-serializable evidence, recomputed-after-restore host evidence and compatibility projection values separated", phase21);
        Assert.Contains("restore validation does not serialize, import or publish host-owned evidence, VMCS projection metadata, compatibility projection metadata, raw measurement secrets, raw sealing keys, active host pointers, completion records or compatibility projection values as guest/runtime authority", phase21);
    }

    [Fact]
    public void SecureComputeRefactoringNewDocs_DoNotTreatNestedDesignFenceAsExecutionOrMigrationAuthority()
    {
        List<string> failures = FindForbiddenSecureComputeRefactoringNewWording(
            ForbiddenNestedDesignFenceAuthorityClaims);

        Assert.True(
            failures.Count == 0,
            "Forbidden nested design-fence authority wording in SecureComputerefactoringNew:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));

        string phase19 = ReadSecureComputeRefactoringNew("19_nested_domain_design_fence.md");
        string phase21 = ReadSecureComputeRefactoringNew("21_release_gate_and_activation_checklist.md");

        Assert.Contains("Nested child intent is not nested backend execution authority", phase19);
        Assert.Contains("Parent-child monotonicity is proof of bounded policy only", phase19);
        Assert.Contains("Nested checkpoint is not migration authority, nested backend execution, VMCS12/VMCS02 authority or mutable Shadow VMCS authority", phase19);
        Assert.Contains("`SecureNestedDomainAdmissionPolicy`, `AllowedDesignFence`, `AllowedNoEffect`, child intent descriptors and nested projection/checkpoint services are design-fence/no-effect/admission surfaces, not implementation of nested SecureCompute execution", phase19);
        Assert.Contains("Nested evidence, telemetry and checkpoint facts are not guest/runtime authority, nested backend execution or production activation evidence", phase19);
        Assert.Contains("No nested checkpoint as migration authority", phase19);
        Assert.Contains("nested child intent, parent-child monotonicity and nested checkpoint remain design-fence evidence only", phase21);
        Assert.Contains("`SecureNestedDomainAdmissionPolicy`, `AllowedDesignFence`, `AllowedNoEffect`, child intent descriptors and nested projection/checkpoint services remain design-fence/no-effect/admission surfaces, not implementation of nested SecureCompute execution", phase21);
        Assert.Contains("nested execution excluded unless separately approved", phase21);
        Assert.Contains("nested evidence, telemetry and checkpoint facts remain quarantined from guest/runtime authority, nested backend execution and production activation evidence", phase21);
    }

    [Fact]
    public void SecureComputeNestedDesignFenceSources_DoNotAuthorizeBackendMutableStateOrVmcsMigrationAuthority()
    {
        string source = ReadProjectSource(NestedDesignFenceProductionSourcePaths());

        Assert.Contains("AllowedDesignFence", source);
        Assert.Contains("AllowedNoEffect", source);
        Assert.Contains("DeniedChildMigrationPayloadExpansion", source);
        Assert.Contains("DeniedNestedVmcsAuthority", source);
        Assert.Contains("DeniedMutableShadowVmcsAuthority", source);
        Assert.Contains("BackendSuccessAuthorized: false", source);
        Assert.Contains("MutableNestedStateAuthorized: false", source);
        Assert.Contains("NestedDomainProjectionCheckpointService", source);
        Assert.Contains("NestedProjectionService", source);
        Assert.Contains("HostOwnedEvidenceDenied", source);
        Assert.Contains("compatibility nested admission cannot bypass", source);

        foreach (Regex forbidden in ForbiddenNestedProductionAuthorityShortcuts)
        {
            Assert.DoesNotMatch(forbidden, source);
        }

        Assert.DoesNotContain("AllowBackendExecution = true", source);
        Assert.DoesNotContain("VmxCaps", source);
        Assert.DoesNotContain("VMREAD", source);
        Assert.DoesNotContain("VMWRITE", source);
        Assert.DoesNotContain("VmcsManager", source);
        Assert.DoesNotContain("IVmcsManager", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);
        Assert.DoesNotContain("ReadFieldValue(", source);
        Assert.DoesNotContain("WriteFieldValue(", source);
        Assert.DoesNotContain("new SecureComputeDomainDescriptor", source);
    }

    [Fact]
    public void SecureComputeRefactoringNewDocs_DoNotTreatBackendOwnerProofGateAsRuntimeExecutionOrPublication()
    {
        List<string> failures = FindForbiddenSecureComputeRefactoringNewWording(
            ForbiddenPositiveRuntimeExecutionAuthorityClaims);

        Assert.True(
            failures.Count == 0,
            "Forbidden backend-owner proof/runtime-execution wording in SecureComputerefactoringNew:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));

        string phase20 = ReadSecureComputeRefactoringNew("20_positive_runtime_execution_rfc_gate.md");
        string phase21 = ReadSecureComputeRefactoringNew("21_release_gate_and_activation_checklist.md");

        Assert.Contains("`SecureBackendOwnerRfcGate` and `AllowedProofOnlyNoExecution` are proof-only evidence surfaces, not secure backend execution", phase20);
        Assert.Contains("Approved RFC/ADR state, `ProofChainAccepted`, `SecureBackendOwnerDescriptor` and Phase 20 subphase labels (`20A`-`20E`) are not typed execution request/result implementation, backend execution authorization, completion publication, retire publication or production activation evidence", phase20);
        Assert.Contains("Proof-only/no-effect evidence cannot publish completion, retire effects or backend success", phase20);
        Assert.Contains("Phase 20 does not open nested execution; nested SecureCompute backend execution still requires a separate nested owner, evidence, monotonicity, checkpoint and publication test chain", phase20);
        Assert.Contains("No backend-owner wording as production activation claim", phase20);
        Assert.Contains("No completion publication or retire publication through `AllowedProofOnlyNoExecution`", phase20);
        Assert.Contains("No `20A`/`20B` RFC or owner proof as typed execution model, no-side-effect internal execution, completion record, retire publication or activation evidence", phase20);
        Assert.Contains("`SecureBackendOwnerRfcGate`, `AllowedProofOnlyNoExecution` and proof-only/no-effect evidence do not become secure backend execution, completion publication, retire publication or production activation claims", phase21);
        Assert.Contains("approved RFC/ADR state, `ProofChainAccepted`, `SecureBackendOwnerDescriptor`, Phase 20 subphase labels, typed-execution vocabulary, completion-record vocabulary and retire-publication vocabulary do not become backend execution, nested execution, completion/retire publication or production activation evidence", phase21);
    }

    [Fact]
    public void SecureComputeBackendOwnerRfcGateSources_DoNotAuthorizeRuntimeExecutionOrPublication()
    {
        string source = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Backend/SecureBackendOwnerDescriptor.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Backend/SecureBackendOwnerAdmissionPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Io/SecureIoHypercallAdmissionPolicy.cs");

        Assert.Contains("AllowedProofOnlyNoExecution", source);
        Assert.Contains("DeniedBackendExecutionClosed", source);
        Assert.Contains("DeniedBackendSuccessClosed", source);
        Assert.Contains("BackendExecutionAuthorized: false", source);
        Assert.Contains("CompletionPublicationAuthorized: false", source);
        Assert.Contains("RetirePublicationAuthorized: false", source);
        Assert.DoesNotContain("BackendExecutionAuthorized: true", source);
        Assert.DoesNotContain("CompletionPublicationAuthorized: true", source);
        Assert.DoesNotContain("RetirePublicationAuthorized: true", source);
        Assert.DoesNotContain("SecureBackendExecutionRequest", source);
        Assert.DoesNotContain("SecureBackendExecutionDecision", source);
        Assert.DoesNotContain("AllowedInternalExecutionNoPublication", source);
        Assert.DoesNotContain("AllowedCompletionRecordNoPublication", source);
        Assert.DoesNotContain("SecureCompletionRecord", source);
        Assert.DoesNotContain("NoSideEffectProbe", source);
        Assert.DoesNotContain("NestedBackendExecution", source);
        Assert.DoesNotContain("MutableNestedStateAuthorized: true", source);
        Assert.DoesNotContain("RuntimeOwnedPublication", source);
        Assert.DoesNotContain("TrapCompletionRouteDescriptor", source);
        Assert.DoesNotContain("TrapCompletionRouteService", source);
        Assert.DoesNotContain("TrapCompletionPublicationFence", source);
    }

    [Fact]
    public void SecureComputeRefactoringNewDocs_DoNotTreatEvidenceDebugOrAttestationAsAuthority()
    {
        List<string> failures = FindForbiddenSecureComputeRefactoringNewWording(
            ForbiddenEvidenceDebugAttestationAuthorityClaims);

        Assert.True(
            failures.Count == 0,
            "Forbidden evidence/debug/attestation authority wording in SecureComputerefactoringNew:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));

        string phase11 = ReadSecureComputeRefactoringNew("11_measurement_and_evidence_visibility.md");
        string phase12 = ReadSecureComputeRefactoringNew("12_debug_observability_and_attestation_boundary.md");
        string phase21 = ReadSecureComputeRefactoringNew("21_release_gate_and_activation_checklist.md");

        Assert.Contains("Guest-visible evidence is visibility only, not runtime authority, VMREAD authority, migration authority, completion publication or activation evidence", phase11);
        Assert.Contains("Host-owned and recomputed evidence remain quarantined from migration authority and production activation evidence", phase11);
        Assert.Contains("No evidence class as completion publication without an explicit completion fence", phase11);
        Assert.Contains("Debug traces and attestation facts are visibility surfaces, not runtime authority, VMREAD authority, migration authority or production activation evidence", phase12);
        Assert.Contains("No debug trace, attestation output or test telemetry as completion publication or activation evidence", phase12);
        Assert.Contains("measurement/debug/attestation visibility does not become runtime authority, VMREAD authority, migration authority, completion publication or production activation evidence", phase21);
    }

    [Fact]
    public void SecureComputeEvidenceDebugAttestationSources_DoNotAuthorizeRuntimeVmreadMigrationOrCompletion()
    {
        string source = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Measurement/DomainMeasurementDescriptor.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Measurement/SecureMeasurementAdmissionPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Evidence/SecureEvidencePolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Evidence/SecureEvidencePublicationPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Checkpoint/SecureCheckpointPayloadPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/SecureCompute/Sideband/EvidenceTransport/SecureComputeEvidenceSidebandEnvelope.cs");

        Assert.Contains("IsAttestationEvidenceOnly => true", source);
        Assert.Contains("DeniedHostOwnedEvidence", source);
        Assert.Contains("DeniedRecomputedEvidence", source);
        Assert.Contains("DeniedDebugTraceAsGuestState", source);
        Assert.Contains("Secure completion publication requires an explicit completion fence", source);
        Assert.Contains("Migration-serializable evidence is not a guest-visible publication class", source);
        Assert.Contains("Recomputed-after-restore evidence cannot be reused as guest-visible publication state", source);
        Assert.DoesNotContain("VmcsField", source);
        Assert.DoesNotContain("VmxCaps", source);
        Assert.DoesNotContain("VMREAD", source);
        Assert.DoesNotContain("VMWRITE", source);
        Assert.DoesNotContain("VmcsManager", source);
        Assert.DoesNotContain("IVmcsManager", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);
        Assert.DoesNotContain("ReadFieldValue(", source);
        Assert.DoesNotContain("WriteFieldValue(", source);
        Assert.DoesNotContain("RuntimeOwnedPublication", source);
        Assert.DoesNotContain("BackendExecutionAuthorized: true", source);
        Assert.DoesNotContain("CompletionPublicationAuthorized: true", source);
        Assert.DoesNotContain("RetirePublicationAuthorized: true", source);
        Assert.DoesNotContain("SecureBackendExecutionRequest", source);
        Assert.DoesNotContain("SecureBackendExecutionDecision", source);
    }

    [Fact]
    public void SecureComputeRefactoringNewDocs_DoNotTreatMemoryPrivateDomainPolicyAsIsaVmxMigrationOrActivationAuthority()
    {
        List<string> failures = FindForbiddenSecureComputeRefactoringNewWording(
            ForbiddenMemoryPrivateDomainAuthorityClaims);

        Assert.True(
            failures.Count == 0,
            "Forbidden memory/private-domain authority wording in SecureComputerefactoringNew:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));

        string phase13 = ReadSecureComputeRefactoringNew("13_memory_and_private_domain_policy.md");
        string phase21 = ReadSecureComputeRefactoringNew("21_release_gate_and_activation_checklist.md");

        Assert.Contains("Private/shared/measured memory descriptors, host-inspection metadata and runtime dirty/migration classes are neutral descriptor/runtime-admission facts", phase13);
        Assert.Contains("not hardware tags, tagged-memory semantics, CHERI-like ISA/memory semantics, VMX EPT/VPID/NPT authority, VMREAD/VMWRITE authority, migration authority or production activation evidence", phase13);
        Assert.Contains("Private memory descriptors are not hardware tags; shared memory descriptors are not raw pointer admission; measured memory descriptors are not production activation evidence", phase13);
        Assert.Contains("Host-inspection metadata is not VMREAD/VMWRITE authority; runtime-dirty classes and runtime-migration classes are not migration authority by themselves", phase13);
        Assert.Contains("Policy-sealed checkpoint payload contract is migration/storage validation only, not CHERI sealing, sealed capability, pointer-level authority or tag/provenance migration format", phase13);
        Assert.Contains("No hardware memory tags, capability registers, capability-bearing operands, per-pointer provenance or tagged-memory migration format", phase13);
        Assert.Contains("No EPT/VPID/NPT, VMREAD or VMWRITE as SecureCompute memory authority", phase13);
        Assert.Contains("memory/private-domain descriptors, host-inspection metadata, policy-sealed checkpoint payload contracts and runtime dirty/migration classes do not become hardware tags, CHERI-like ISA/memory semantics, VMX EPT/VPID/NPT authority, VMREAD/VMWRITE authority, migration authority or production activation evidence", phase21);
        Assert.Contains("private descriptors do not become hardware tags, shared descriptors do not become raw pointer admission, measured descriptors do not become production activation evidence", phase21);
        Assert.Contains("EPT/VPID/NPT/VMX artifacts and VMREAD schema/value projections do not become SecureCompute memory authority", phase21);
    }

    [Fact]
    public void SecureComputeMemoryPrivateDomainSources_DoNotCreateTaggedMemoryCapabilityAwareIsaOrVmxAuthority()
    {
        string memorySource = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Memory/SecureMemoryDomainDescriptor.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Memory/SecureMemoryAdmissionPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/HostInspection/SecureHostInspectionPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs");
        string migrationSource = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Migration/SecureMigrationDescriptor.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Migration/SecureMigrationAdmissionPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Checkpoint/SecureCheckpointPayloadPolicy.cs");

        Assert.Contains("SecureMemoryDomainDescriptor", memorySource);
        Assert.Contains("SecureRuntimeMutableDirtyPolicy", memorySource);
        Assert.Contains("SecureRuntimeMutableMigrationClass", memorySource);
        Assert.Contains("DeniedRuntimeMutableClassification", memorySource);
        Assert.Contains("SecurePrivateMemorySealedPayloadContract", migrationSource);
        Assert.Contains("DeniedPrivateMemoryWithoutSealedEncryptedContract", migrationSource);
        Assert.Contains("ContainsRawSealingKey", migrationSource);
        Assert.Contains("DeniedRawSealingKey", migrationSource);

        foreach (string forbidden in ForbiddenMemoryIsaAndTagTokens())
        {
            Assert.DoesNotContain(forbidden, memorySource);
            Assert.DoesNotContain(forbidden, migrationSource);
        }

        foreach (string forbidden in ForbiddenMemoryVmxAuthorityTokens())
        {
            Assert.DoesNotContain(forbidden, memorySource);
        }
    }

    [Fact]
    public void LayerOneTwoSecureComputeSources_DoNotImportFutureIsaOrVmxAuthority()
    {
        List<string> failures = new();
        foreach (string path in EnumerateLayerOneTwoProductionSources())
        {
            string source = File.ReadAllText(path);
            foreach (string token in ForbiddenLayerOneTwoSourceTokens)
            {
                if (source.Contains(token, StringComparison.Ordinal))
                {
                    failures.Add($"{Relative(path)} contains forbidden token `{token}`.");
                }
            }
        }

        Assert.True(
            failures.Count == 0,
            "Layer 1/2 SecureCompute product sources must remain descriptor/grant-only:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void PhaseSevenEightNineEvidenceTestsRemainPresent()
    {
        string secureTests = ReadProjectSource(
            "HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureAuthorityDisciplineTests.cs",
            "HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureIoHypercallPolicyTests.cs",
            "HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureNestedDomainDesignFenceTests.cs");
        string vmxTests = ReadProjectSource(
            "HybridCPU_ISE.Tests/VmxRefactoring/SecureComputeVmxPhase8BoundaryMatrixTests.cs",
            "HybridCPU_ISE.Tests/VmxRefactoring/SecureComputeVmxPhase9NestedFenceTests.cs");

        Assert.Contains("SecureGrantAuthority_ValidScalarShapeWithoutProvenanceIsDenied", secureTests);
        Assert.Contains("SecureGrantAuthority_CompatibilityProjectionCannotSatisfySecureAuthority", secureTests);
        Assert.Contains("SecureHypercall_AdmittedDeniedDoesNotAuthorizeBackendCompletionOrRetirePublication", secureTests);
        Assert.Contains("SecureNestedChildIntent_ValidSubsetIsDesignFenceOnly", secureTests);

        Assert.Contains("SecureComputeVmxReadMatrix_DeniesSecureSensitiveCompatibilityFields", vmxTests);
        Assert.Contains("SecureComputeVmxWriteVmxCapsCheckpointAndBackendProjectionAreDenied", vmxTests);
        Assert.Contains("SecureComputeVmxNestedSources_KeepShadowVmcsAsCompatibilityBridgeOnly", vmxTests);
    }

    [Fact]
    public void SecureComputeActivationPlan_RecordsPhase09OwnerAndPhase10ProjectionClosureProof()
    {
        string ownerSource = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/ExecutionState/PrivilegedExecutionStateDescriptor.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/ExecutionState/PrivilegedExecutionStateOwnerPolicy.cs");
        string ownerTests = ReadProjectSource(
            "HybridCPU_ISE.Tests/SecureComputeRefactoring/PrivilegedExecutionStateOwnerPolicyTests.cs");
        string projectionSource = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/SecureCompute/Compatibility/Projection/PrivilegedExecutionStateProjectionService.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs");
        string projectionTests = ReadProjectSource(
            "HybridCPU_ISE.Tests/VmxRefactoring/GuestCr0Cr4ReadOnlyProjectionTests.cs");
        string index = ReadSecureComputeActivationPlan("00_securecompute_activation_refactoring_index.md");
        string matrix = ReadSecureComputeActivationPlan("01_current_state_and_gap_matrix.md");
        string rfc = ReadSecureComputeActivationPlan("09_privileged_execution_state_owner_rfc.md");
        string projectionPlan = ReadSecureComputeActivationPlan("10_guestcr0_guestcr4_readonly_projection_plan.md");
        string conformance = ReadSecureComputeActivationPlan("21_conformance_negative_positive_test_matrix.md");
        string releaseGate = ReadSecureComputeActivationPlan("22_limited_securecompute_release_gate.md");
        string backlog = ReadSecureComputeActivationPlan("23_open_decision_backlog.md");

        Assert.Contains("PrivilegedExecutionStateDescriptor", ownerSource);
        Assert.Contains("PrivilegedExecutionStateOwnerPolicy", ownerSource);
        Assert.Contains("AllowedOwnerMaterializedProjectionClosed", ownerSource);
        Assert.Contains("ReadOnlyProjectionAuthorized: false", ownerSource);
        Assert.Contains("BackendExecutionAuthorized: false", ownerSource);
        Assert.Contains("CompletionPublicationAuthorized: false", ownerSource);
        Assert.Contains("RetirePublicationAuthorized: false", ownerSource);
        Assert.Contains("PrivilegedExecutionStateOwner_MissingDescriptor_Denied", ownerTests);
        Assert.Contains("PrivilegedExecutionStateOwner_DomainTagMismatch_Denied", ownerTests);
        Assert.Contains("PrivilegedExecutionStateOwner_AddressSpaceTagMismatch_Denied", ownerTests);
        Assert.Contains("PrivilegedExecutionStateOwner_StaleEpoch_Denied", ownerTests);
        Assert.Contains("PrivilegedExecutionStateOwner_ReservedBits_Denied", ownerTests);
        Assert.Contains("PrivilegedExecutionStateOwner_NotVmcsBacked_SourceGuard", ownerTests);
        Assert.Contains("PrivilegedExecutionStateDescriptor", projectionSource);
        Assert.Contains("PrivilegedExecutionStateOwnerPolicy", projectionSource);
        Assert.Contains("PrivilegedExecutionStateProjectionService", projectionSource);
        Assert.Contains("PrivilegedExecutionStateProjectionDenied", projectionSource);
        Assert.Contains("DeniedUnsupportedRegister", projectionSource);
        Assert.Contains("BackendSuccessAuthorized: false", projectionSource);
        Assert.Contains("MutationAuthorized: false", projectionSource);
        Assert.Contains("CompletionPublicationAuthorized: false", projectionSource);
        Assert.Contains("RetirePublicationAuthorized: false", projectionSource);
        Assert.DoesNotContain("TryReadScalarField", projectionSource);
        Assert.DoesNotContain("VmcsManager", projectionSource);
        Assert.DoesNotContain("VmxExecutionUnit", projectionSource);
        Assert.Contains("GuestCr0Cr4Projection_DeniedBeforeOwnerMaterialization", projectionTests);
        Assert.Contains("GuestCr0Cr4Projection_DeniedWithoutReadOnlySource", projectionTests);
        Assert.Contains("GuestCr0Cr4Projection_DeniedWithoutVisibilityPolicy", projectionTests);
        Assert.Contains("GuestCr0Cr4Projection_DeniedWithoutMigrationClass", projectionTests);
        Assert.Contains("GuestCr0Cr4Projection_DeniedWithoutConformanceProof", projectionTests);
        Assert.Contains("GuestCr0Cr4Projection_AllowedReadOnlyAfterAllGates", projectionTests);
        Assert.Contains("GuestCr0Cr4VmWrite_RemainsDenied", projectionTests);

        Assert.Contains("ADR-SC-PES-GuestCr0Cr4", rfc);
        Assert.Contains("RFC/ADR accepted; neutral owner-proof contract implemented", rfc);
        Assert.Contains("AllowedOwnerMaterializedProjectionClosed", rfc);
        Assert.Contains("Exit status: satisfied by code and tests", rfc);
        Assert.Contains("Phases 09 through 12 are closed", index);
        Assert.Contains("implemented narrow projection-only path", matrix);
        Assert.Contains("implemented, projection-only gate closed", projectionPlan);
        Assert.Contains("Implemented Phase 10 Positive Tests", conformance);
        Assert.Contains("Phases 09 and 10 now supply", releaseGate);
        Assert.Contains("Phases 09 and 10 closed by owner/projection code", backlog);
        Assert.Contains("Phase 11 secure memory/private-domain policy gate is closed", backlog);
        Assert.Contains("Phase 12 secure I/O/shared-buffer policy gate is closed", backlog);
        Assert.Contains("Phase 13 secure hypercall backend owner RFC is the exact next gate", backlog);
        Assert.Contains("Pool 1 closed", backlog);
    }

    [Fact]
    public void SecureComputeActivationPlan_RecordsPhase11SecureMemoryPrivateDomainPolicyClosureProof()
    {
        string memorySource = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Memory/SecureMemoryDomainDescriptor.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Memory/SecureMemoryAdmissionPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs");
        string migrationSource = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Migration/SecureMigrationDescriptor.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Migration/SecureMigrationAdmissionPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Checkpoint/SecureCheckpointPayloadPolicy.cs");
        string memoryTests = ReadProjectSource(
            "HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureMemoryDomainPolicyTests.cs");
        string index = ReadSecureComputeActivationPlan("00_securecompute_activation_refactoring_index.md");
        string matrix = ReadSecureComputeActivationPlan("01_current_state_and_gap_matrix.md");
        string phase11 = ReadSecureComputeActivationPlan("11_secure_memory_private_domain_policy_activation_plan.md");
        string conformance = ReadSecureComputeActivationPlan("21_conformance_negative_positive_test_matrix.md");
        string releaseGate = ReadSecureComputeActivationPlan("22_limited_securecompute_release_gate.md");
        string backlog = ReadSecureComputeActivationPlan("23_open_decision_backlog.md");

        Assert.Contains("SecureMemoryDomainDescriptor", memorySource);
        Assert.Contains("SecureMemoryAdmissionPolicy", memorySource);
        Assert.Contains("DeniedMissingDescriptor", memorySource);
        Assert.Contains("DeniedUnmaterializedDescriptor", memorySource);
        Assert.Contains("DeniedStalePolicyEpoch", memorySource);
        Assert.Contains("DeniedPrivateHostRead", memorySource);
        Assert.Contains("DeniedPrivateDma", memorySource);
        Assert.Contains("DeniedSharedRequiresExplicitPolicy", memorySource);
        Assert.Contains("DeniedDmaRequiresTypedGrant", memorySource);
        Assert.Contains("DeniedMeasuredRegionMissing", memorySource);
        Assert.Contains("DeniedRuntimeMutableClassification", memorySource);
        Assert.Contains("SecurePrivateMemorySealedPayloadContract", migrationSource);
        Assert.Contains("DeniedPrivateMemoryWithoutSealedEncryptedContract", migrationSource);
        Assert.Contains("ContainsRawSealingKey", migrationSource);
        Assert.Contains("DeniedRawSealingKey", migrationSource);

        Assert.Contains("SecureMemoryPolicy_MissingUnmaterializedOrStaleDescriptorDenied", memoryTests);
        Assert.Contains("SecureMemoryPolicy_PrivateMemoryIsNotHostReadable", memoryTests);
        Assert.Contains("SecureMemoryPolicy_DmaToPrivateMemoryDenied", memoryTests);
        Assert.Contains("SecureMemoryPolicy_DmaToPrivateMemoryDeniedThroughHypercallArgumentPath", memoryTests);
        Assert.Contains("SecureMemoryPolicy_DmaToExplicitSharedBufferRequiresMemoryIoPolicyAndTypedGrant", memoryTests);
        Assert.Contains("SecureMemoryPolicy_SharedDmaRequiresCurrentSharedBufferGrantEpoch", memoryTests);
        Assert.Contains("SecureMemoryPolicy_MeasurementRequiresMeasuredRegion", memoryTests);
        Assert.Contains("SecureMemoryPolicy_MeasuredAdmissionDoesNotSatisfyPrivateDomainActivation", memoryTests);
        Assert.Contains("SecureMemoryPolicy_RuntimeMutableTouchRequiresDirtyAndMigrationClassification", memoryTests);
        Assert.Contains("SecureMemoryPolicy_SealedPrivatePayloadContractIsValidationNotRawKeyOrTagAuthority", memoryTests);
        Assert.Contains("SecureMemorySources_DoNotCreateVmcsBackedTranslationAuthority", memoryTests);

        Assert.Contains("SecureMemoryDomainDescriptor` and `SecureMemoryAdmissionPolicy` implement the Phase 11", index);
        Assert.Contains("Closed memory/private-domain policy item", index);
        Assert.Contains("implemented Phase 11 policy gate", matrix);
        Assert.Contains("Status: implemented memory/private-domain policy-admission gate", phase11);
        Assert.Contains("Phase 11 closes only descriptor-owned policy admission", phase11);
        Assert.Contains("Implemented Phase 11 Positive Policy-Admission Tests", conformance);
        Assert.Contains("Phase 11 tests proving missing/unmaterialized/stale secure-memory descriptors deny admission", releaseGate);
        Assert.Contains("Phase 11 memory/private-domain policy admission is restricted to descriptor-owned policy checks and remains non-executing", releaseGate);
        Assert.Contains("Phase 11 secure memory/private-domain policy gate is closed", backlog);
        Assert.Contains("Phase 12 secure I/O/shared-buffer policy gate is closed", backlog);
        Assert.Contains("Phase 13 secure hypercall backend owner RFC is the exact next gate", backlog);

        foreach (string forbidden in ForbiddenMemoryIsaAndTagTokens())
        {
            Assert.DoesNotContain(forbidden, memorySource);
            Assert.DoesNotContain(forbidden, migrationSource);
        }

        foreach (string forbidden in ForbiddenMemoryVmxAuthorityTokens())
        {
            Assert.DoesNotContain(forbidden, memorySource);
        }
    }

    [Fact]
    public void SecureComputeActivationPlan_RecordsPhase12SecureIoSharedBufferPolicyClosureProof()
    {
        string ioSource = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Io/SecureIoDomainDescriptor.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Io/SecureIoHypercallAdmissionPolicy.cs");
        string ioTests = ReadProjectSource(
            "HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureIoHypercallPolicyTests.cs");
        string migrationTests = ReadProjectSource(
            "HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureMigrationPolicyTests.cs");
        string index = ReadSecureComputeActivationPlan("00_securecompute_activation_refactoring_index.md");
        string matrix = ReadSecureComputeActivationPlan("01_current_state_and_gap_matrix.md");
        string phase12 = ReadSecureComputeActivationPlan("12_secure_io_shared_buffer_policy_plan.md");
        string conformance = ReadSecureComputeActivationPlan("21_conformance_negative_positive_test_matrix.md");
        string releaseGate = ReadSecureComputeActivationPlan("22_limited_securecompute_release_gate.md");
        string backlog = ReadSecureComputeActivationPlan("23_open_decision_backlog.md");

        Assert.Contains("TryFindCurrentSharedBuffer", ioSource);
        Assert.Contains("candidate.IsOwnedBy(ownerDomainTag)", ioSource);
        Assert.Contains("candidate.HasCurrentLifetime(policyEpoch)", ioSource);
        Assert.Contains("candidate.Grant.MatchesEpoch(policyEpoch)", ioSource);
        Assert.Contains("IsPolicyAdmissionOnly", ioSource);
        Assert.Contains("CompletionPublicationAuthorized: false", ioSource);
        Assert.Contains("RetirePublicationAuthorized: false", ioSource);
        Assert.Contains("DeniedRawPrivatePointer", ioSource);
        Assert.Contains("DeniedSharedBufferBinding", ioSource);
        Assert.Contains("DeniedMissingTypedGrant", ioSource);
        Assert.Contains("DeniedBackendSuccessClosed", ioSource);
        Assert.DoesNotContain("AllowsSharedBuffer(", ioSource);
        Assert.DoesNotContain("completion.CompletionPublicationAuthorized", ioSource);
        Assert.DoesNotContain("completion.RetirePublicationAuthorized", ioSource);
        Assert.DoesNotContain("publicationFence?.CanPublishCompletion == true", ioSource);
        Assert.DoesNotContain("publicationFence?.CanPublishRetire == true", ioSource);
        Assert.DoesNotContain("VmCall", ioSource);
        Assert.DoesNotContain("Lane6", ioSource);
        Assert.DoesNotContain("Lane7", ioSource);

        Assert.Contains("SecureIo_DmaToPrivateMemoryDeniedAndSharedBufferRequiresTypedGrant", ioTests);
        Assert.Contains("SecureIo_FencePresenceDoesNotAuthorizeCompletionOrRetirePublication", ioTests);
        Assert.Contains("SecureIo_MissingNeutralOwnerAndPublicationFenceDeny", ioTests);
        Assert.Contains("SecureHypercall_RawPrivatePointerAndForgedOpaqueHandleDenied", ioTests);
        Assert.Contains("SecureHypercall_SharedBufferRequiresCurrentOwnerLifetimeEvidenceAndBufferGrant", ioTests);
        Assert.Contains("SecureHypercall_SharedBufferArgumentRequiresExplicitSharedBufferPolicy", ioTests);
        Assert.Contains("SecureHypercall_AdmittedDeniedDoesNotAuthorizeBackendCompletionOrRetirePublication", ioTests);
        Assert.Contains("SecureIoHypercallSources_DoNotCreateVmxVmcsOrVmxCapsAuthority", ioTests);
        Assert.Contains("SecureCheckpointPayloadClass.BackendBindingEvidence", migrationTests);
        Assert.Contains("SecureCheckpointPayloadClass.NativeTokenEvidence", migrationTests);

        Assert.Contains("Phase 12 secure I/O/shared-buffer policy gate is implemented", index);
        Assert.Contains("implemented Phase 12 policy-admission gate", matrix);
        Assert.Contains("Status: implemented secure I/O/shared-buffer policy-admission gate", phase12);
        Assert.Contains("Implemented Phase 12 Positive Policy-Admission Tests", conformance);
        Assert.Contains("Phase 12 tests proving shared-buffer owner, lifetime, evidence and buffer-grant epoch binding", releaseGate);
        Assert.Contains("Phase 12 secure I/O/shared-buffer policy gate is closed", backlog);
        Assert.Contains("Phase 13 secure hypercall backend owner RFC is the exact next gate", backlog);
    }

    [Fact]
    public void SecureComputeWhiteBook_IsSplitCurrentAndActivationPlanTraceable()
    {
        string root = SecureComputeWhiteBookRoot();
        string[] files = Directory
            .EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        string corpus = string.Join(
            Environment.NewLine,
            files.Select(File.ReadAllText));

        Assert.Equal(14, files.Length);
        Assert.Contains("00_README.md", files.Select(Path.GetFileName));
        Assert.Contains(
            "01_Phases_00_12_Evidence_Ledger.md",
            files.Select(Path.GetFileName));
        Assert.Contains(
            "02_Terminology_And_Status_Vocabulary.md",
            files.Select(Path.GetFileName));
        Assert.Contains(
            "HybridCPU_ISE/docs/ref2/SecureComputeActivationPlan/",
            corpus);
        Assert.Contains(
            "The exact next gate is Phase 13",
            corpus);
        Assert.Contains(
            "For every non-ordinary secure operation class",
            corpus);
        Assert.Contains(
            "An allowed I/O result is explicitly `IsPolicyAdmissionOnly`",
            corpus);
        Assert.Contains(
            "The tasks through Phase 12 are complete for the bounded closure classes",
            corpus);
        Assert.DoesNotContain(
            "only for non-ordinary `SecureDomainOperationClass` with an enabled descriptor",
            corpus);
        Assert.DoesNotContain(
            "GuestCr0` and `GuestCr4` remain denied",
            corpus);

        List<string> wordingFailures = FindForbiddenSecureComputeWhiteBookWording(
            ForbiddenAffirmativeDocClaims
                .Concat(ForbiddenRouteFencePublicationClaims)
                .Concat(ForbiddenPublicationMatrixCollapseClaims)
                .Concat(ForbiddenVmreadSchemaReadableClaims)
                .Concat(ForbiddenStreamLaneAuthorityClaims)
                .Concat(ForbiddenSecureIoLaneAuthorityClaims)
                .Concat(ForbiddenHypercallTrapRecognitionAuthorityClaims)
                .Concat(ForbiddenMigrationCheckpointAuthorityClaims)
                .Concat(ForbiddenNestedDesignFenceAuthorityClaims)
                .Concat(ForbiddenPositiveRuntimeExecutionAuthorityClaims)
                .Concat(ForbiddenEvidenceDebugAttestationAuthorityClaims)
                .Concat(ForbiddenMemoryPrivateDomainAuthorityClaims)
                .ToArray());
        Assert.True(
            wordingFailures.Count == 0,
            "Forbidden authority wording in split SecureCompute WhiteBook:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, wordingFailures));

        Regex markdownLink = new(@"\[[^\]]+\]\(([^)#]+\.md)\)");
        List<string> brokenLinks = new();
        foreach (string file in files)
        {
            foreach (Match match in markdownLink.Matches(File.ReadAllText(file)))
            {
                string target = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(file)!,
                    match.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar)));
                if (!File.Exists(target))
                {
                    brokenLinks.Add($"{Relative(file)} -> {match.Groups[1].Value}");
                }
            }
        }

        Assert.True(
            brokenLinks.Count == 0,
            "Broken SecureCompute WhiteBook links:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, brokenLinks));
    }

    [Fact]
    public void SecureComputeActivationPlan_RecordsStageBDescriptorRoutingClosureProof()
    {
        string runtimeAdmission = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs");
        string secureAdmissionPolicy = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Admission/SecureDomainAdmissionPolicy.cs");
        string hookTests = ReadProjectSource(
            "HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureRuntimeBoundaryAdmissionHookTests.cs");
        string index = ReadSecureComputeActivationPlan("00_securecompute_activation_refactoring_index.md");
        string matrix = ReadSecureComputeActivationPlan("01_current_state_and_gap_matrix.md");
        string guards = ReadSecureComputeActivationPlan("02_global_forbidden_regressions_and_release_guards.md");
        string noEffect = ReadSecureComputeActivationPlan("04_no_effect_disabled_baseline_revalidation.md");
        string stageB = ReadSecureComputeActivationPlan("06_stage_b_secure_admission_activation_plan.md");
        string conformance = ReadSecureComputeActivationPlan("21_conformance_negative_positive_test_matrix.md");
        string releaseGate = ReadSecureComputeActivationPlan("22_limited_securecompute_release_gate.md");
        string backlog = ReadSecureComputeActivationPlan("23_open_decision_backlog.md");

        Assert.Contains(
            "request.SecureOperationClass != SecureDomainOperationClass.Ordinary",
            runtimeAdmission);
        Assert.Contains("_secureAdmission.Admit(", runtimeAdmission);
        Assert.DoesNotContain("secureDescriptor is { IsEnabled: true }", runtimeAdmission);
        Assert.Contains("DeniedMissingDescriptor", secureAdmissionPolicy);
        Assert.Contains("DeniedDisabledDescriptor", secureAdmissionPolicy);
        Assert.Contains("DeniedUnmaterializedDescriptor", secureAdmissionPolicy);
        Assert.Contains("RuntimeBoundaryAdmission_OrdinaryOperationRemainsAllowedWhenDescriptorAbsent", hookTests);
        Assert.Contains("RuntimeBoundaryAdmission_OrdinaryOperationRemainsAllowedWhenDescriptorDisabled", hookTests);
        Assert.Contains("RuntimeBoundaryAdmission_NonOrdinaryOperationDeniesWhenDescriptorMissingThroughStageB", hookTests);
        Assert.Contains("RuntimeBoundaryAdmission_NonOrdinaryOperationDeniesWhenDescriptorDisabledThroughStageB", hookTests);
        Assert.Contains("RuntimeBoundaryAdmission_NonOrdinaryOperationDeniesWhenDescriptorUnmaterializedThroughStageB", hookTests);
        Assert.Contains("RuntimeBoundaryAdmission_PolicyDenyReasonReachesStageBHook", hookTests);
        Assert.Contains("RuntimeBoundaryAdmission_NoEnabledDescriptorGuardBypassContractTests", hookTests);
        Assert.Contains("NonOrdinarySecureOperationClasses", hookTests);
        Assert.DoesNotContain("CurrentReadinessBypass", hookTests);
        Assert.DoesNotContain("Current Stage B bypass evidence only", hookTests);
        Assert.Contains("Stage B P0 routing blocker is closed by runtime routing, taxonomy-wide tests and source scans", index);
        Assert.Contains("implemented, P0 routing blocker closed", matrix);
        Assert.Contains("Stage B bypass for non-ordinary secure operations remains a forbidden regression", guards);
        Assert.Contains("no-effect applies to ordinary operations only", noEffect);
        Assert.Contains("fixed by runtime routing and tests", stageB);
        Assert.Contains("runtime tests and source scans", stageB);
        Assert.Contains("old Stage B bypass tests are removed or inverted", stageB);
        Assert.Contains("ordinary no-effect: absent descriptor -> allowed", conformance);
        Assert.Contains("non-ordinary fail-closed: absent descriptor -> denied by Stage B", conformance);
        Assert.Contains("non-ordinary Stage B denial coverage is taxonomy-wide", conformance);
        Assert.Contains("runtime tests and source-scan proof", conformance);
        Assert.Contains("Stage B missing/disabled/unmaterialized descriptor bypass is closed in code and tests", releaseGate);
        Assert.Contains("runtime tests, source scans and conformance docs proving ordinary no-effect is separate from non-ordinary fail-closed routing across the full non-ordinary taxonomy", releaseGate);
        Assert.Contains("closed for P0 routing; retain post-fix verification", backlog);
        Assert.Contains("Pools 1 through 3 closed", backlog);
        Assert.Contains("Stage B regression hardening is closed taxonomy-wide", backlog);
        Assert.Contains("Keep runtime tests, source scans and release-gate conformance checks green", backlog);
    }

    private static string[] ForbiddenMemoryIsaAndTagTokens() =>
        new[]
        {
            "MemoryTag",
            "TagBit",
            "TaggedMemory",
            "CapabilityTag",
            "HardwareTag",
            "TagStorage",
            "TagMigrationPayload",
            "CapabilityLoad",
            "CapabilityStore",
            "CapabilityFetch",
            "CapabilityAwareLoad",
            "CapabilityAwareStore",
            "CapabilityAwareFetch",
            "LOAD_CAP",
            "STORE_CAP",
            "FETCH_CAP",
            "CapabilityOperand",
            "CapabilityRegister",
            "CHERI",
            "TagProvenance",
            "ProvisionalTag",
            "ProvenanceCheckpoint",
        };

    private static string[] ForbiddenMemoryVmxAuthorityTokens() =>
        new[]
        {
            "VmcsField",
            "VmxCaps",
            "VMREAD",
            "VMWRITE",
            "VmcsManager",
            "IVmcsManager",
            "VmxExecutionUnit",
            "ReadFieldValue(",
            "WriteFieldValue(",
            "EptPointer",
            "NptPointer",
            "Vpid",
            "VPID",
            "Npt",
            "NPT",
            "INVEPT",
            "INVVPID",
        };

    private static List<string> FindForbiddenSecureComputeRefactoringNewWording(
        IReadOnlyList<Regex> patterns)
    {
        List<string> failures = new();
        foreach (string path in EnumerateSecureComputeRefactoringNewMarkdownSources())
        {
            string[] lines = File.ReadAllLines(path);
            for (int index = 0; index < lines.Length; index++)
            {
                foreach (Regex pattern in patterns)
                {
                    if (!pattern.IsMatch(lines[index]) ||
                        IsForbiddenOrNegativeContext(lines, index))
                    {
                        continue;
                    }

                    failures.Add($"{Relative(path)}:{index + 1}: {lines[index].Trim()}");
                }
            }
        }

        return failures;
    }

    private static List<string> FindForbiddenSecureComputeWhiteBookWording(
        IReadOnlyList<Regex> patterns)
    {
        List<string> failures = new();
        foreach (string path in Directory
                     .EnumerateFiles(
                         SecureComputeWhiteBookRoot(),
                         "*.md",
                         SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            string[] lines = File.ReadAllLines(path);
            for (int index = 0; index < lines.Length; index++)
            {
                foreach (Regex pattern in patterns)
                {
                    if (!pattern.IsMatch(lines[index]) ||
                        IsForbiddenOrNegativeContext(lines, index))
                    {
                        continue;
                    }

                    failures.Add($"{Relative(path)}:{index + 1}: {lines[index].Trim()}");
                }
            }
        }

        return failures;
    }

    private static bool IsForbiddenOrNegativeContext(string[] lines, int index)
    {
        int first = Math.Max(0, index - 5);
        int count = Math.Min(lines.Length - first, 11);
        string window = string.Join(" ", lines.Skip(first).Take(count)).ToLowerInvariant();

        return window.Contains("forbidden", StringComparison.Ordinal) ||
            window.Contains("banned", StringComparison.Ordinal) ||
            window.Contains("banning", StringComparison.Ordinal) ||
            window.Contains("ban ", StringComparison.Ordinal) ||
            window.Contains("should be banned", StringComparison.Ordinal) ||
            window.Contains("must be banned", StringComparison.Ordinal) ||
            window.Contains("баниться", StringComparison.Ordinal) ||
            window.Contains("запрет", StringComparison.Ordinal) ||
            window.Contains("запрещ", StringComparison.Ordinal) ||
            window.Contains("cannot", StringComparison.Ordinal) ||
            window.Contains("can not", StringComparison.Ordinal) ||
            window.Contains("must not", StringComparison.Ordinal) ||
            window.Contains("do not", StringComparison.Ordinal) ||
            window.Contains("does not", StringComparison.Ordinal) ||
            window.Contains("not ", StringComparison.Ordinal) ||
            window.Contains("no ", StringComparison.Ordinal) ||
            window.Contains("non-goals", StringComparison.Ordinal) ||
            window.Contains("deny", StringComparison.Ordinal) ||
            window.Contains("denied", StringComparison.Ordinal) ||
            window.Contains("reject", StringComparison.Ordinal) ||
            window.Contains("unsafe", StringComparison.Ordinal) ||
            window.Contains("wrong form", StringComparison.Ordinal) ||
            window.Contains("bad example", StringComparison.Ordinal) ||
            window.Contains("not implementation guidance", StringComparison.Ordinal) ||
            window.Contains("regression", StringComparison.Ordinal);
    }

    private static IEnumerable<string> EnumerateSecureComputeMarkdownSources()
    {
        string root = SecureComputeRoot();
        return Directory
            .EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
            .Where(path => path.Contains(Path.DirectorySeparatorChar + "Plan" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                path.Contains(Path.DirectorySeparatorChar + "Plan2" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                path.Contains(Path.DirectorySeparatorChar + "Docs" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private static IEnumerable<string> EnumerateSecureComputeRefactoringNewMarkdownSources() =>
        Directory
            .EnumerateFiles(SecureComputeRefactoringNewRoot(), "*.md", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal);

    private static IEnumerable<string> EnumerateLayerOneTwoProductionSources()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] roots =
        {
            Path.Combine(repositoryRoot, "HybridCPU_ISE", "CloseToRTL", "Core", "Runtime", "Domains", "SecureCompute"),
            Path.Combine(repositoryRoot, "HybridCPU_ISE", "CloseToRTL", "Core", "Virtualization", "SecureCompute"),
        };

        return roots
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "Conformance" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private static string[] NestedDesignFenceProductionSourcePaths() =>
        new[]
        {
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Nested/SecureChildDomainIntentDescriptor.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Nested/SecureNestedDomainAdmissionPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Nested/NestedDomainProjectionCheckpointService.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Nested/CapabilityFilter/NestedCapabilityFilter.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Nested/Descriptors/NestedDomainDescriptor.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Nested/Policies/NestedEvidencePolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Nested/Projection/INestedProjectionService.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Nested/Projection/NestedProjectionService.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Nested/Validation/NestedValidationResult.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/Compatibility/Generated/VmcsProjection/NestedDomainControllerCompatibilityProjection.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/Compatibility/Generated/VmcsProjection/ShadowVmcsNestedProjectionService.cs",
        };

    private static string ReadPlan(string fileName) =>
        File.ReadAllText(Path.Combine(SecureComputeRoot(), "Plan", fileName));

    private static string ReadSecureComputeRefactoringNew(string fileName) =>
        File.ReadAllText(Path.Combine(SecureComputeRefactoringNewRoot(), fileName));

    private static string ReadSecureComputeActivationPlan(string fileName) =>
        File.ReadAllText(Path.Combine(SecureComputeActivationPlanRoot(), fileName));

    private static string ReadProjectSource(params string[] relativePaths)
    {
        string root = FindRepositoryRoot();
        return string.Concat(relativePaths.Select(path => File.ReadAllText(Path.Combine(
            root,
            path.Replace('/', Path.DirectorySeparatorChar)))));
    }

    private static string SecureComputeRoot() =>
        Path.Combine(
            FindRepositoryRoot(),
            "HybridCPU_ISE",
            "CloseToRTL",
            "Core",
            "Runtime",
            "Domains",
            "SecureCompute");

    private static string SecureComputeRefactoringNewRoot() =>
        Path.Combine(
            FindRepositoryRoot(),
            "HybridCPU_ISE",
            "docs",
            "ref2",
            "SecureComputerefactoringNew");

    private static string SecureComputeActivationPlanRoot() =>
        Path.Combine(
            FindRepositoryRoot(),
            "HybridCPU_ISE",
            "docs",
            "ref2",
            "SecureComputeActivationPlan");

    private static string SecureComputeWhiteBookRoot() =>
        Path.Combine(
            FindRepositoryRoot(),
            "Documentation",
            "SecureCompute WhiteBook");

    private static string Relative(string path) =>
        Path.GetRelativePath(FindRepositoryRoot(), path).Replace('\\', '/');

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE.sln")) ||
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate HybridCPU ISE repository root.");
    }
}
