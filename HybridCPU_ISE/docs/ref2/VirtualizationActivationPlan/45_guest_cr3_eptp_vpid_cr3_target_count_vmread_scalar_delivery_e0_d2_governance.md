# Phase 45 — memory-owned GuestCr3/EptPointer/Vpid/Cr3TargetCount VMREAD scalar-delivery E0/D2 governance

Status: immutable E0/SpecV2, a later non-self-referential AcceptanceRecordV2 and machine validation are materialized. Production composition remains unauthorized.

## 2026-06-11 Audit Contract

- File name: `45_guest_cr3_eptp_vpid_cr3_target_count_vmread_scalar_delivery_e0_d2_governance.md`.
- Purpose: freeze the exact memory-owned E0 and D2 governance package without production composition.
- Status: immutable spec and later non-self-referential acceptance are machine-materialized; runtime scalar delivery is unauthorized.
- Scope: exact `GuestCr3`/`EptPointer`/`Vpid`/`Cr3TargetCount` scalar-delivery policy and its canonical MemoryDomain freshness prerequisite.
- No-goals: no receipt implementation, runtime activation, adjacent field, backend, completion, VMWRITE, VMCALL, compiler or memory-transaction change.
- Code anchors: `MemoryDomainRuntime.AddressSpaceGeneration.cs`, `Phase45MemoryOwnedVmReadScalarDeliveryE0Contract.cs`, `Phase45MemoryOwnedVmReadScalarDeliveryDecisionSpecV2.cs`, `MemoryDomainDescriptor.cs`, `MemoryDomainReadOnlyTranslationView.cs`.
- Authority owner: existing neutral `MemoryDomainDescriptor` via canonical `MemoryDomainRuntime`; governance and VMCS metadata are not authority.
- Required RFC/ADR: this separate exact decision; earlier GuestCr and GuestPc/Sp/Flags decisions remain unchanged.
- Acceptance criteria: exact identity/fields/owner/source/freshness/evidence/domain/ABI/migration constraints with no side authority.
- Tests/static scans: Phase 45 generation, spec, acceptance, provenance, negative and forbidden-shortcut tests plus full VMX/SecureCompute matrices and Release build.
- Risks: treating caller-supplied translation state or generation as current, or treating acceptance as runtime admission.
- Next-gate dependency: separately authorized exact production composition after this accepted D2; no field group or runtime path opens automatically.

The full owner-map columns are: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.

## Exact scope

- DecisionId: `D2-HV-VMREAD-SCALAR-DELIVERY-V1-GUEST-CR3-EPTP-VPID-CR3TC-0003`.
- Namespace: `HybridCPU.VMREAD.ScalarDelivery.v1`.
- Operation: `DELIVER_GUEST_CR3_EPTP_VPID_CR3TC_SCALAR_V1`.
- Fields: only `GuestCr3`, `EptPointer`, `Vpid`, and `Cr3TargetCount`.
- Authority plane: existing `MemoryAddressSpaceReadProjection`.
- Owner/source: existing `MemoryDomainDescriptor` and canonical `MemoryDomainRuntime`; values come only from a materialized `MemoryDomainReadOnlyTranslationView`.
- Result/effect candidates: `ScalarU64ToDestinationRegister` / `ArchitecturalRegisterResultOnly`.
- Capability: `None`; VMCALL grants and capability bit 41 are unrelated.
- Migration: `DrainOnly`; receipts, seals, physical destinations and VMREAD output are not serialized.
- Activation: default disabled. This package creates no activation binding or production receipt path.

## E0 freshness result

The legacy descriptor-carried `AddressSpaceGeneration` was caller-provided and did not advance for every value-changing replacement, rebind, rematerialization or restore. It therefore could not serve as freshness authority. The canonical `MemoryDomainRuntime` now owns a non-zero generation and atomically captures owner, value, domain, address space, current generation and exact field. A caller-provided view or generation cannot establish freshness.

Exact value rules are independent: `GuestCr3` is the canonical address-space root; `EptPointer` requires an owned valid second-stage root; `Vpid` requires enabled tagging and a non-zero tag; `Cr3TargetCount` is the canonical bounded target count. Failure of one field gate grants nothing for another field.

The canonical ingress remains `VmxCompatibilityAdmissionService.AdmitVmReadProjection`, and the existing `ReadCompatibilityProjection` / `FullDomainRuntime` boundary remains unchanged. VMCS identifiers are compatibility metadata only. This governance object is not source authority, admission, a capability, a receipt, a PRF grant or a retire grant.

## Explicit denials

No VMCS scalar/backing-store source, direct architectural register write, `VmxRetireEffect` authority, VMCALL decision reuse, trap/backend completion, VMWRITE, adjacent field, host alias, `HostCr3`, compatibility-control value, compiler change, nested/SecureCompute, memory transaction, IOMMU, I/O, device, Lane6/Lane7/Stream or broad VMREAD activation is authorized. Production composition requires a separate bounded authorization after machine acceptance.

The authoritative generation prerequisite is commit `3cb896e37fc7b5775099bf34ca9082e488a73dd3`, tree `840d48603162146de36e51d65bca7d6ebe151d8c`. Immutable SpecV2 is commit `4299d743af293f0a0780eb5289a54c3154259ad2`, tree `71ac59e42a8abfec6fb8adee958d483b08de7529`, digest `7cc2ad6bca9cc808aa6d42767dba5c7eaefed1a34180cb6caf3b34384662df21`. The later AcceptanceRecordV2 is contained by commit `8b3675b5eb4a1a83a7feff95e02c6d7b8e8f1920`, tree `aae2962fb7417c2581b9e8739b9166c9367542eb`, acceptance digest `741be410f5e699d9a953b3d8a8fe1171abbbda6d798b9b97dbb89e10bae55dcc`; the record itself remains non-self-referential.
