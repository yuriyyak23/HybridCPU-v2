# Runtime Legality Chain

## Scope

This document fixes the current legality chain for the active typed-slot SMT
path. It keeps scheduler preparation, checker-owned legality, replay reuse, and
resource-legality witness semantics separate. Manuscript-facing prose may call
the ordered checks a boundary-guard chain and the reusable object a legality
witness; this document keeps the repository names explicit for auditability.

## Scheduler Preparation Of The Working Bundle

The scheduler prepares live bundle state before candidate-specific admission
begins.

- `PackBundleIntraCoreSmt(...)` computes current class capacity from the owner
  bundle.
- It builds the live `BundleResourceCertificate4Way`,
  `SmtBundleMetadata4Way`, and `BoundaryGuardState`.
- It publishes that state through `PrepareSmt(...)`.

This preparation creates the current working-bundle legality context. The
scheduler does not skip directly from nomination to lane placement.

## Checker-Owned Decision Order

For SMT legality, `SafetyVerifier.EvaluateSmtLegality(...)` follows this live
order:

1. `EvaluateSmtBoundaryGuard(...)`
2. owner/context and domain rejection through `TryRejectSmtOwnerDomainGuard(...)`
3. replay/template reuse on `PhaseCertificateTemplateKey4Way` match
4. structural-witness fallback otherwise, implemented by the current-bundle
   structural certificate

The scheduler consumes `LegalityDecision`. It does not re-derive legality from
raw masks after the checker has already spoken.

## Authority Sources And Witness Role

The active typed-slot SMT authority sources are:

- `LegalityAuthoritySource.GuardPlane`
- `LegalityAuthoritySource.ReplayPhaseCertificate`
- `LegalityAuthoritySource.StructuralCertificate`

Repository certificates therefore act as runtime-local legality witnesses
inside the checker-owned service. They are not external attestation artifacts,
and they do not independently rank candidates, choose a lane, or publish
retirement. The final checker-owned verdict surface remains
`LegalityDecision`.

## Replay Reuse And Invalidation

Replay reuse is bounded and invalidation-driven.

- `PhaseCertificateTemplateKey4Way` combines `ReplayPhaseKey`,
  `BundleResourceCertificate4Way.StructuralIdentity`,
  `SmtBundleMetadata4Way`, and `BoundaryGuardState`.
- `RefreshSmtAfterMutation(...)` refreshes the reusable template after a live
  bundle mutation.
- `InvalidatePhaseMismatch(...)` withdraws reuse when the live replay phase no
  longer matches the prepared phase.

Owner, domain, boundary, or mutation drift therefore closes the reuse window or
forces structural-witness fallback.

## Structural Identity Does Not Flatten Hazard Domains

`BundleResourceCertificate4Way` keeps shared and per-VT hazard domains
separate.

- `SharedMask` records genuinely shared structural resource state.
- `RegMaskVT0..VT3` record per-VT register-group hazard state.
- `BundleResourceCertificateIdentity4Way.Create(...)` incorporates those fields
  separately when producing `StructuralIdentity`.

Replay/template compatibility therefore preserves, rather than erases, the
distinction between shared structural resources and per-VT register hazards.

## Repository-Facing Non-Claims

This document does not claim that:

- replay reuse is unconditional;
- the scheduler is the final legality authority;
- structural identity is a flat hazard hash that forgets shared/per-VT scope;
- legality success by itself chooses the final lane or retires the operation.

## Code And Proof Surfaces

Primary code authority:

- `HybridCPU_ISE/Core/Pipeline/Safety/SafetyVerifier.SmtLegality.cs`
- `HybridCPU_ISE/Core/Pipeline/Certificates/BundleResourceCertificate4Way.cs`
- `HybridCPU_ISE/Core/Pipeline/Certificates/ReplayPhaseSubstrate.cs`
- `HybridCPU_ISE/Core/Pipeline/Certificates/ReplayPhaseSubstrate.Implementations.cs`
- `HybridCPU_ISE/Core/Legality/BundleLegalityAnalyzer.cs`

Representative proof surfaces:

- `HybridCPU_ISE.Tests/tests/Phase09RuntimeLegalityServiceReachabilityProofTests.cs`
- `HybridCPU_ISE.Tests/tests/Phase09ReplayCertificateCoordinatorProofTests.cs`
- `HybridCPU_ISE.Tests/FSPAndSpeculative/DeterminismEnvelopeTests.cs`
