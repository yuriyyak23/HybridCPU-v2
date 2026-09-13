# 02. ISE Secure Policy Hardening

## Goal

Make SecureCompute admission exhaustive and deny-by-default before any external `ProductionSecure` claim is allowed.

## Required changes

`SecureDomainOperationClass` must have explicit policy for every non-ordinary operation. In particular, add dedicated checks for:

- `CreateEvidence` — evidence policy, visibility and producer classification;
- `PublishCompletion` — exact operation/domain binding and publication policy;
- `PublishRetireSideEffect` — exact retire/publication authorization;
- `SecureMigration` — explicit migration policy, sealing/anti-replay capability and generation;
- `NestedSecureDomain` — exact parent subset and nested secure policy;
- `CompatibilityProjection` — read/project-only policy, never authority mutation.

Existing `SecureIo` and `SecureHypercall` checks remain explicit. Unknown enum values or unsupported classes deny.

## Secure memory

Retain exact domain-tag/address-space binding. Private/shared/measured/runtime-mutable classifications remain semantic policy inputs and do not by themselves prove hardware isolation.

## Root authority and compatibility

- VMX/compatibility frontend cannot activate SecureCompute by setting capability bits or mutable compatibility state.
- compatibility projection cannot mint root authority or bypass RuntimeBoundaryAdmissionService.
- ordinary no-state execution cannot enter a secure domain.

## Evidence rules

- measurement/evidence is a predicate input, not execution permission;
- hardware-rooted claims require an explicitly classified producer/assurance level;
- model/emulated evidence cannot satisfy hardware-attestation-required policy;
- stale evidence generation fails before secure effect/publication.

## Publication rules

Secure completion and retire-side publication require exact current domain/memory/evidence context. Completion alone is not publication authorization.

## Required tests

Add one allow and multiple deny cases for every secure operation class, including missing policy, stale generation, wrong domain/address-space, compatibility mutation attempt and unsupported/unknown operation class.

## Exit criterion

No non-ordinary secure operation reaches a generic allow path; every class is explicitly policy-governed or explicitly unsupported.
