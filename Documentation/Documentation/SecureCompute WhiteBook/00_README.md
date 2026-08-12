# SecureCompute WhiteBook

Status date: 2026-08-06.

## Purpose

This is the split architectural WhiteBook for SecureCompute in HybridCPU-v2 / CloseToHSL.

The normative source for development order, phase status, release requirements and future gates is:

`HybridCPU_ISE/docs/ref2/SecureComputeActivationPlan/`

This WhiteBook is a maintained architectural projection of that corpus. It explains verified behavior, authority ownership and boundaries. It does not independently approve implementation or activation.

## Current Status

- The current audit baseline is recorded in ActivationPlan phase `24`. The audited SHA is unavailable in the local object database; the current checkout is dirty and must not be represented as immutable release evidence.
- Ordinary disabled/absent descriptor behavior and generic non-ordinary policy routing have unit coverage, but end-to-end disabled observational equivalence is not proven.
- The root descriptor has public construction and two carrier sources (`DomainRuntimeContext.SecureCompute` and request-supplied `SecureDescriptor`). No canonical registry, opaque binding or single materialization/revocation owner exists.
- `RuntimeBoundaryAdmissionService` is a generic policy boundary. Its observed production callers are VMX compatibility projections using the default ordinary operation class; it is not proven as CPU Stage-B enforcement.
- SafetyVerifier issues no immutable SecureCompute admission certificate binding operation, domain, VT, slot, epoch and effects identity.
- Grant policy validates caller-supplied handles and booleans; no mint/revoke ledger owner is implemented.
- Memory, I/O, migration, publication and evidence components are policy/classification surfaces, not production effect-path enforcement. Region and shared-buffer maps are not yet canonicalized against overlaps and duplicate identities.
- `GuestCr0`/`GuestCr4` remain the only verified narrow read-only VMX projection; VMX has zero SecureCompute authority.
- Compiler no-emission is enforced by contour/lowering rejection. Clean generated-artifact inventory and hash reproducibility remain open.
- Phase 18 is unconditionally future/design-fenced. Phase 20 is a pre-activation evidence classifier. Phase 21 is a negative/future-gated conformance matrix. Phase 22 is a hard-denied release gate.
- Positive backend execution, secure completion/retire publication, nested execution, compiler secure emission and limited/production release remain forbidden.

This is activation readiness hardening and bounded policy implementation, not production SecureCompute activation.

The dependency-ordered remediation plan, phase classification and C0/C1/C2 blocker register are in `HybridCPU_ISE/docs/ref2/SecureComputeActivationPlan/24_audit_revalidation_and_dependency_order.md`.

## Reading Order

1. [`01_Architecture/01_Position_And_Authority.md`](01_Architecture/01_Position_And_Authority.md)
2. [`01_Architecture/02_Runtime_Admission_And_Descriptors.md`](01_Architecture/02_Runtime_Admission_And_Descriptors.md)
3. [`01_Architecture/03_Compatibility_And_VMX_Boundary.md`](01_Architecture/03_Compatibility_And_VMX_Boundary.md)
4. [`02_Policy_Domains/01_Memory_And_Private_Domains.md`](02_Policy_Domains/01_Memory_And_Private_Domains.md)
5. [`02_Policy_Domains/02_Measurement_Evidence_And_Grants.md`](02_Policy_Domains/02_Measurement_Evidence_And_Grants.md)
6. [`02_Policy_Domains/03_Secure_IO_And_Shared_Buffers.md`](02_Policy_Domains/03_Secure_IO_And_Shared_Buffers.md)
7. [`02_Policy_Domains/04_Migration_Checkpoint_Restore.md`](02_Policy_Domains/04_Migration_Checkpoint_Restore.md)
8. [`03_Activation_Governance/01_Phases_00_12_Evidence_Ledger.md`](03_Activation_Governance/01_Phases_00_12_Evidence_Ledger.md)
9. [`03_Activation_Governance/02_Release_Conformance_And_Static_Guards.md`](03_Activation_Governance/02_Release_Conformance_And_Static_Guards.md)
10. [`03_Activation_Governance/03_Future_Gates_13_23.md`](03_Activation_Governance/03_Future_Gates_13_23.md)
11. [`04_Traceability/01_Code_Test_Document_Map.md`](04_Traceability/01_Code_Test_Document_Map.md)
12. [`04_Traceability/02_Terminology_And_Status_Vocabulary.md`](04_Traceability/02_Terminology_And_Status_Vocabulary.md)

## Global Boundaries

- no VMX authority;
- no secure VMCS;
- no `VmxCaps` capability source;
- no VMCS-owned secure state;
- no CHERI ISA, tagged memory or capability registers;
- no capability-aware `LOAD`, `STORE` or `FETCH`;
- no compiler secure-emission shortcut;
- no backend execution from proof-only or policy-admission results;
- no completion or retire publication from admission alone;
- no nested SecureCompute execution;
- no migration/output-manifest authority from host-owned evidence, native tokens, raw secrets, raw sealing keys, active host pointers, VMCS metadata or compatibility metadata.
