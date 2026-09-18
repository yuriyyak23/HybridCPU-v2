# Phase 42 — exact GuestCr0/GuestCr4 VMREAD scalar delivery production composition

Status: closed by clean implementation subject `253e33435b1500a04ecde9228631fb3fab547d15`
and later non-self-referential evidence record
`evidence/2026-08-11-phase42-guest-cr0-cr4-vmread-scalar-delivery-clean-evidence.json`.

## 2026-06-11 Audit Contract

- File name: `42_guest_cr0_cr4_vmread_scalar_delivery_production.md`.
- Purpose: compose the accepted Phase 41 scalar-delivery D2 with the existing Phase 40 projection and canonical scalar pipeline.
- Status: exact implementation, clean subject verification and later non-self-referential evidence are closed.
- Scope: only `VmcsField.GuestCr0` and `VmcsField.GuestCr4`, one x1-x31 scalar destination, attempt-bound opaque receipt, scalar EX/MEM/WB and `RetireCoordinator` commit.
- No-goals: no broad VMREAD, VMWRITE, VMCS value source, direct architectural write, VMCALL operand/E5/E6 reuse, backend/trap completion, compiler emission or adjacent virtualization activation.
- Code anchors: `VmReadScalarDeliveryAcceptedPolicyResolver.cs`, `VmReadScalarDeliveryCanonicalComposition.cs`, `MicroOpScheduler.ExactVmReadScalarDelivery.cs`, `MicroOp.IO.cs`, `CPU_Core.PipelineExecution.VmxRetire.cs`.
- Authority owner: the existing `PrivilegedExecutionStateOwnerPolicy` and `PrivilegedExecutionStateDescriptor`; accepted governance and the receipt grant no source authority.
- Required RFC/ADR: accepted `D2-HV-VMREAD-SCALAR-DELIVERY-V1-GUEST-CR0-CR4-0001`; the earlier projection D2 is unchanged.
- Acceptance criteria: exact policy resolution, canonical E1 reachability, atomic owner/domain/address-space/epoch snapshot, single-use replay/restore-bound receipt, canonical scalar retire record and deterministic adjacent denial.
- Tests/static scans: `VmxPhase41ScalarDeliveryProductionTests`, Phase 40 owner/projection tests, full VMX matrix, Release without test hooks and forbidden/reachability scans.
- Risks: receipt reuse, mixed descriptor/epoch observation, compatibility metadata becoming a value source, or VMX retire/trap completion shortcuts.
- Next-gate dependency: satisfied by the named clean subject and later evidence record; no adjacent feature pool opens automatically.

The full owner-map columns are: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.

## Production boundary

The only decode carrier is the existing canonical `VmxMicroOp` created for frozen
`VMREAD`. Common SafetyVerifier E1 remains attempt/bundle/replay admission and has
no value or delivery authority. The VMCALL-specific operand snapshot is rejected
for VMREAD and is not reused.

After canonical register-source reading, the exact VMREAD composition resolves
the accepted Phase 41 policy, admits the earlier read-only projection, and asks
the existing privileged execution-state policy/service for the selected value.
Descriptor, current policy epoch, domain, address space and profile generation
are captured under one composition lock.

The opaque `VmReadScalarResultReceipt` binds E1 attempt/issuer/bundle/replay,
restore generation, domain, address space, descriptor epoch, exact field,
destination and scalar value. It is single-use and becomes invalid after profile
disable, replay replacement, restore/descriptor replacement or epoch replacement.
An observed restore-generation change without a revalidated descriptor/epoch
disables the exact profile; `ReplaceAfterRestore` also leaves it disabled until
an explicit exact reactivation.

The receipt only enables the existing primary scalar carrier. It produces no
`VmxRetireEffect`, backend result, VMCS mutation or trap completion. At precise
writeback the carrier emits one `RetireRecord.RegisterWrite`; the existing
`RetireCoordinator` remains the sole architectural commit owner. Squash or stale
receipt before retire produces no architectural write.

## Activation and rollback

The scheduler has no scalar-delivery binding by default. Configuration accepts
only an already enabled exact composition whose policy lookup matches the exact
decision, namespace, operation, two-field set, owner, result/effect ABI,
`Capability=None` and `DrainOnly`. Disable removes the scheduler binding and
increments composition generation, invalidating outstanding receipts.

No `enableVmread` broad flag exists. The compatibility frontend cannot configure
the internal scheduler binding and cannot manufacture a receipt.

## Explicit denials

- every field other than GuestCr0 and GuestCr4;
- x0, missing/invalid destination or nonzero reserved Rs2;
- missing, mismatched or revoked Phase 41 policy;
- missing/unmaterialized/stale/wrong-domain/wrong-address-space descriptor;
- wrong register kinds, reserved bits, missing required bits, evidence,
  migration classification or conformance proof;
- VMCS scalar/backing-store source, VMWRITE, host aliases and compatibility controls;
- VMCALL receipts/E5/E6, backend execution, trap completion and VMX retire effect;
- SecureCompute, nested, memory/IOMMU/I/O/device/lane/stream and compiler expansion.
