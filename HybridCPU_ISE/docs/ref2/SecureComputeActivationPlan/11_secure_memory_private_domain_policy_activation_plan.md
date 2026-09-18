# Secure Memory Private Domain Policy Activation Plan

## Phase Metadata

- File name: `11_secure_memory_private_domain_policy_activation_plan.md`
- Phase goal: prepare activation-grade secure memory policy without importing hardware tag or CHERI semantics.
- Status: partial policy class; region-map canonicalization and production memory-effect enforcement are open.
- Scope: private, shared, measured and runtime-mutable memory descriptors, host visibility, DMA, dirty policy and migration class.
- No-goals: no tagged memory, no capability-aware memory instructions, no VMX EPT/VPID/NPT authority and no activation evidence from memory descriptors.

## Current Baseline

Secure memory descriptors classify private/shared/measured/runtime-mutable regions. Private memory denies host reads and DMA. Shared buffers require explicit descriptors, current shared-buffer grants and typed capability grants. Runtime-mutable regions require dirty and migration classification. Measured regions may be admitted for measurement but do not become private-domain activation evidence.

## Authority Owner

The descriptor/policy class owns only classification. No production load/store/translation/cache/backing-store/prefetch owner calls it. A future canonical memory-attempt owner must enforce a SafetyVerifier certificate and exact region ID before translation and every effect.

Accepted recommended design: the memory-domain registry materializes a checked, sorted, disjoint interval map with stable region IDs, digest and generation. Zero-length, arithmetic overflow, duplicate, overlap and ambiguous containment reject the entire map. Enforcement is two-phase: pre-translation intent admission and post-translation effect verification. Coverage includes fetch, load, store, atomic, cache fill/writeback, prefetch, assists, FSP and DMA; a compiler hint or post-access range check cannot substitute for either phase.

## What Can Be Implemented

- memory classification matrix implemented in `SecureMemoryDomainDescriptor`;
- private/shared/measured/runtime-mutable admission implemented in `SecureMemoryAdmissionPolicy`;
- dirty/migration class requirements enforced for runtime-mutable regions;
- shared-buffer handoff constraints require I/O policy, owner/lifetime/evidence binding, current shared-buffer grant and typed capability grant;
- negative tests cover missing/unmaterialized/stale descriptors, host/private/DMA violations and private migration without sealed/encrypted payload contract.

## What Remains Denied/Future-Gated

- hardware memory tags;
- tagged-memory semantics;
- CHERI-like memory semantics;
- VMX EPT/VPID/NPT authority;
- VMREAD/VMWRITE memory authority;
- migration authority from measured descriptor alone;
- production private-memory migration without a sealed/encrypted payload contract and restore revalidation;
- secure backend execution or publication from memory admission.

## Forbidden Shortcuts

- private descriptor as hardware tag;
- measured descriptor as activation evidence;
- shared descriptor as raw pointer admission;
- dirty class as migration permission without policy;
- policy-sealed payload as CHERI sealing.

## Required RFC/ADR

No RFC/ADR for fail-closed policy. Production private-memory migration or new secure memory execution semantics require separate RFC/ADR.

## Code Anchors

- `SecureMemoryDomainDescriptor.cs`
- `SecureMemoryAdmissionPolicy.cs`
- `SecureIoDomainDescriptor.cs`
- `SecureMigrationDescriptor.cs`
- `SecureMigrationAdmissionPolicy.cs`
- `SecureCheckpointPayloadPolicy.cs`

## Documentation Anchors

- `SecureComputerefactoringNew/13_memory_and_private_domain_policy.md`
- `SecureComputerefactoringNew/17_migration_checkpoint_restore.md`
- `SecureCompute Plan/03-layer1-secure-memory-plan.md`

## Required Tests

- private host read denied;
- private DMA denied;
- shared DMA requires explicit shared-buffer descriptor;
- shared DMA requires current typed grant;
- measured descriptor participates in admission but not activation evidence;
- missing, unmaterialized and stale secure-memory descriptors fail closed;
- shared DMA requires current shared-buffer grant epoch;
- runtime-mutable region requires dirty and migration classification;
- private migration denied without sealed/encrypted payload contract.

## Required Static/Source Scans

- `hardware tag`
- `tagged memory`
- `CHERI`
- `EPT.*SecureCompute`
- `NPT.*SecureCompute`
- `VPID.*SecureCompute`
- `VMREAD.*SecureMemory`
- `measured.*activation`

## Migration/Evidence Classification

Private memory requires explicit sealed/encrypted payload contract and restore revalidation before any migration path can be considered. Shared memory is descriptor-controlled and grant-bound. Measured memory is evidence-related but not activation evidence.

## Completion/Retire Implications

Memory admission has no completion or retire publication authority.

## SecureCompute Activation Implications

Memory policy is required for future activation but is not sufficient and cannot be the first activation claim by itself. Phase 11 currently specifies and directly tests policy semantics only.

## Exit Criteria

- memory matrix complete;
- tests deny forbidden host/DMA/migration paths;
- tests prove measured admission does not satisfy private-domain activation;
- source guards preserve no hardware tag, no CHERI and no VMX memory-authority boundary.

Exit status: open. `TryFindRegion` is first-match and constructors do not reject overlap; production memory paths do not call this policy. Closure requires canonical non-overlapping maps plus pre-translation enforcement and negative cache/prefetch/FSP/fault tests.

## Dependency

Previous: `10_guestcr0_guestcr4_readonly_projection_plan.md`. Next: `12_secure_io_shared_buffer_policy_plan.md`.
