# SecureCompute WhiteBook

Status date: 2026-06-12.

## Purpose

This is the split architectural WhiteBook for SecureCompute in HybridCPU-v2 / CloseToRTL.

The normative source for development order, phase status, release requirements and future gates is:

`HybridCPU_ISE/docs/ref2/SecureComputeActivationPlan/`

This WhiteBook is a maintained architectural projection of that corpus. It explains verified behavior, authority ownership and boundaries. It does not independently approve implementation or activation.

## Current Status

- Phases 00-08 establish and verify baseline, process, no-effect, materialization, Stage B, grant and evidence gates.
- Phase 09 implements the neutral privileged execution-state owner proof for `GuestCr0` and `GuestCr4`.
- Phase 10 implements a field-specific read-only compatibility projection after all owner, source, visibility, migration and conformance gates.
- Phase 11 implements secure memory/private-domain policy admission.
- Phase 12 implements secure I/O/shared-buffer policy admission.
- Secure backend execution, completion publication from a secure backend, retire publication, nested secure execution and compiler secure emission remain closed.
- The exact next gate is Phase 13: secure hypercall backend owner RFC.

This is activation readiness hardening and bounded policy implementation, not production SecureCompute activation.

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
- no migration authority from host-owned evidence, raw secrets, raw sealing keys, active host pointers or compatibility metadata.
