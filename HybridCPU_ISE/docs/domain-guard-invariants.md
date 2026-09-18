# Domain Guard Invariants

This document is the Phase 04 reviewer-facing closure surface for domain,
owner, and boundary guards. It records the landed guard plane without expanding
it into a security proof story that the runtime does not implement.

## Scope

The guard plane is a runtime legality authority surface. It is not a hardware
root-of-trust, not a signing system, and not a hardware root-of-trust for
external attestation. The code proves simulated/runtime guard ordering and
fail-closed scheduling behavior inside the ISE.

The central rule is guard-before-reuse: domain and owner guards precede replay reuse and certificate acceptance.

## Domain Certificate Rule

`SafetyVerifier.EvaluateDomainIsolationProbe(...)` defines the domain
certificate predicate:

- if `DomainTag == 0` and `KernelDomainIsolation` is enabled while the pod has a
  non-zero domain certificate, the candidate is rejected as kernel-to-user;
- if `DomainTag == 0` and no user-domain certificate is active, the probe
  allows the operation;
- if the pod domain certificate is zero, no domain enforcement is configured;
- otherwise `(DomainTag & podDomainCert) != 0` is required.

`VerifyDomainCertificate(...)` is a compatibility-facing bool wrapper over this
probe. Production scheduler/runtime legality consumes checker-owned decisions
through `IRuntimeLegalityService`.

## SMT Guard Invariants

`SafetyVerifier.EvaluateSmtLegality(...)` evaluates guards before replay reuse:

1. `EvaluateSmtBoundaryGuard(...)`
2. `TryRejectSmtOwnerDomainGuard(...)`
3. replay-template match and `LegalityAuthoritySource.ReplayPhaseCertificate`
4. current-bundle structural certificate fallback

Guard rejects are emitted with `LegalityAuthoritySource.GuardPlane` and
`attemptedReplayCertificateReuse: false`.

| Guard | Reject surface | Meaning |
| --- | --- | --- |
| boundary guard | `RejectKind.Boundary` | Serializing boundary blocks SMT admission before replay/template reuse. |
| owner guard | `RejectKind.OwnerMismatch` | Candidate owner context differs from known bundle owner context. |
| domain guard | `RejectKind.DomainMismatch` | Candidate domain tag does not satisfy the bundle owner domain certificate. |

The scheduler exposes these through `SmtOwnerContextGuardRejects`,
`SmtDomainGuardRejects`, and `SmtBoundaryGuardRejects` rather than hiding them
behind structural certificate rejects.

## Inter-Core Guard Invariants

`SafetyVerifier.EvaluateInterCoreLegality(...)` resolves inter-core guards
before attempting replay certificate reuse. The early guard seam is
`TryResolveInterCoreGuardDecision(...)`:

- requested domain tags route through `EvaluateInterCoreDomainGuard(...)`;
- scoreboard-pending candidates are rejected as a guard-plane rare hazard;
- same-owner candidates may pass the guard plane without replay reuse;
- non-stealable candidates are rejected before certificate acceptance;
- control-flow candidates are rejected for ordering before certificate
  acceptance.

Only after those guard decisions pass may the inter-core path attempt replay
certificate reuse or fall back to the live structural certificate.

## Assist Guard Invariants

`TryValidateAssistMicroOp(...)` validates assist ownership and domain context
before an assist can execute on its carrier:

- assist runtime epoch must match;
- assist replay epoch must match the live replay phase, otherwise
  `AssistInvalidationReason.Replay` is published;
- carrier, donor, and target virtual threads must still be foreground-issuable;
- explicit core ownership must match the live pod/core context;
- explicit donor source snapshots must match donor VT, owner context, domain tag,
  and donor assist epoch;
- local donor sources must match owner context and domain tag;
- non-zero assist domain tags must satisfy `CsrMemDomainCert`.

Owner/domain failures publish `AssistInvalidationReason.OwnerInvalidation` for
local scope drift. Inter-core owner or donor snapshot failures publish
`AssistInvalidationReason.InterCoreOwnerDrift`. Donor assist epoch drift
publishes `AssistInvalidationReason.InterCoreBoundaryDrift`.

Assist invalidation remains telemetry-visible and replay-trace-visible. It does
not make assists retiring architectural operations.

## Evidence Surfaces

Primary code authority:

- `HybridCPU_ISE/Core/Pipeline/Safety/SafetyVerifier.Guards.cs`
- `HybridCPU_ISE/Core/Pipeline/Safety/SafetyVerifier.RuntimeLegality.cs`
- `HybridCPU_ISE/Core/Pipeline/Safety/SafetyVerifier.SmtLegality.cs`
- `HybridCPU_ISE/Core/Pipeline/Safety/SafetyVerifier.Types.cs`
- `HybridCPU_ISE/Core/Pipeline/Core/CPU_Core.Assist.cs`
- `HybridCPU_ISE/Core/Pipeline/Certificates/ReplayPhaseSubstrate.Implementations.cs`
- `HybridCPU_ISE/Core/Pipeline/Scheduling/MicroOpScheduler.Assist.cs`

Primary proof surfaces:

- `HybridCPU_ISE.Tests/tests/Phase09SafetyVerifierGuardMatrixProofTests.cs`
- `HybridCPU_ISE.Tests/SafetyAndVerification/EarlyDomainCertFilteringTests.cs`
- `HybridCPU_ISE.Tests/FSPAndSpeculative/DomainIsolationStressTests.cs`
- `HybridCPU_ISE.Tests/PhasingAndExtensions/DomainIsolationContractTests.cs`
- `HybridCPU_ISE.Tests/tests/Phase09ReplayFlushInvalidationReasonTests.cs`
- `HybridCPU_ISE.Tests/tests/Phase09ReplayEnvelopeDocumentationTests.cs`

If prose and code diverge, code plus proof wins and this document must be
updated.
