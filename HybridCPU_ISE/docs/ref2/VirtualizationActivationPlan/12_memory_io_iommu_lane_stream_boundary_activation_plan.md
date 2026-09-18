# Phase 12 - Memory I/O IOMMU Lane Stream Boundary Activation Plan

Status: boundary gate. No Lane6/Lane7/Stream virtualization authority is opened.

## 2026-06-11 Audit Contract

- File name: `12_memory_io_iommu_lane_stream_boundary_activation_plan.md`.
- Purpose: keep memory/I/O/IOMMU/Lane6/Lane7/Stream evidence from becoming virtualization authority.
- Status: boundary gate only; no lane/stream virtualization authority is opened.
- Scope: memory read-only VMREAD projection, I/O/IOMMU domains, lane runtime/helper surfaces, telemetry/evidence migration visibility.
- No-goals: no VMX-driven DMA/IOMMU, no lane passthrough, no stream helper state as VMCS/virtualization authority, no compiler lowering through lane surfaces.
- Code anchors: `MemoryDomainReadOnlyTranslationView.cs`, `DmaStreamComputeMicroOp.cs`, `DmaStreamComputeDescriptorParser.cs`, `DmaStreamComputeRuntime.cs`, `SystemDeviceCommandMicroOp.cs`, StreamEngine/SFR documentation anchors.
- Authority owner: memory, I/O/IOMMU, Lane6, Lane7, Stream, and evidence owners for their own domains; VMX aliases own none.
- Required RFC/ADR: mandatory if any path touches memory mutation, I/O, DMA, IOMMU, Lane6, Lane7, or Stream, with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: first VMCALL leaf must be no-state/no-side-effect for these domains; otherwise `требует owner-specific RFC/ADR`.
- Tests/static scans: lane token migration denial, telemetry non-authority, stream helper non-VMCS state, no VMX frontend lane runtime authority calls.
- Risks: treating telemetry, replay evidence, helper success, or tokens as completion/retire/migration authority.
- Next-gate dependency: Phase 15 evidence/migration classes and any future lane-touching RFC/ADR.

## Phase Goal

Keep memory, I/O, IOMMU, Lane6, Lane7, and Stream evidence from becoming VMX authority while defining what a future path would need if it touched these domains.

## Historical Baseline (2026-06-11)

Memory-domain read-only projection exists for `GuestCr3`, `EptPointer`, `Vpid`, and `Cr3TargetCount`. I/O/IOMMU and lane surfaces have neutral/runtime/helper contours, but they are not VMX authority.

Lane placement is not authority. Lane-local runtime authority remains authoritative for lane-local state only: the Lane 6 runtime owns its queue state, token namespace and fences; the Lane 7 runtime owns its accelerator-token namespace, handles, backend binding, completion routing and checkpoint state. A native Lane6/Lane7 token may therefore be lane-local authority, but it is not virtualization authority, completion-publication authority or retire authority.

No production attempt-bound virtualized transaction contract exists. Future memory/device-capable virtualization requires the staged composition below rather than one guest transaction envelope:

```text
VirtualizedTransactionIntent
  + existing MemoryDomain authorization
  + existing Io/IOMMU authorization
  + existing Device authorization
  + lane-local admission where required
  + attempt-bound opaque authorization tokens
  + VirtualizedTransactionReceipt
```

These are derived live authorizations of existing subsystem owners. They do not create new Memory/IOMMU/device owners and cannot be collapsed into a VMX-owned envelope, compatibility DTO or transferable all-domain token.

## ISE-MEM-IO-LANE-STREAM-BOUNDARY-12 - Closure Record

Closure date: 2026-06-18.

Closure state: closed `DENIED/FUTURE-GATED BASELINE / NO-PASSTHROUGH-AUTHORITY`.

This closure records the current memory, I/O, IOMMU, Lane6, Lane7, and Stream boundary baseline. It closes the audit assumption that these surfaces must remain non-authoritative for virtualization; it does not implement VMX-driven DMA/IOMMU, lane passthrough, stream backend authority, completion publication, retire publication, or migration authority.

Verified baseline:

| surface | code/test evidence | current result |
| --- | --- | --- |
| memory-owned VMREAD fields | `GuestCr3`, `EptPointer`, `Vpid`, and `Cr3TargetCount` are read-only projection vocabulary; schema write remains denied | projection only |
| `HostCr3` | denied by the Phase 04 denied-surface baseline | no host address-space authority |
| I/O/IOMMU aliases | `VmxCompatibilityIoAliasesAreReadOnlyDenied` returns no binding, translation, invalidation, dirty-log, or mutation result | denied |
| DMA authority | `DmaAuthorityService` requires I/O domain authority, window, binding, permissions, and fences | no VMX shortcut |
| Lane6 | compatibility-only descriptor returns `RuntimeAuthorityRequired`; guest Lane6 compatibility execution is fail-closed | no passthrough |
| Lane7 | compatibility-only descriptor returns `RuntimeAuthorityRequired`; guest Lane7 compatibility execution is fail-closed | no passthrough |
| Stream/helper evidence | telemetry, replay evidence, helper/model results, tokens, and backend bindings remain host-owned or model/helper-only | no VMCS/migration authority |
| VMX frontend and SecureCompute projection | scans keep lane/stream runtimes out of frontend handlers and secure projection fences | no authority import |

Closure invariants:

- Memory VMREAD projection is not memory mutation, DMA authority, IOMMU authority, completion publication, retire publication, or migration authority.
- I/O/IOMMU helper evidence cannot become VMCS state or VMX backend authority.
- Native Lane6/Lane7/Stream tokens, telemetry, replay evidence, helper success, backend bindings, scheduler pressure, and debug traces cannot migrate as guest virtualization state. Any owner-defined lane checkpoint state remains separately owned and must follow that lane owner's checkpoint contract.
- `DmaStreamComputeRuntime`, `VmxDmaDescriptorValidator`, `ExternalAcceleratorRuntime`, `Lane6DomainRuntime`, `Lane7DomainRuntime`, `Lane7CompletionPolicy`, and `SystemDeviceCommandMicroOp` must not be called from VMX frontend admission/dispatch/retire or SecureCompute VMX projection as authority.
- Any future path that touches memory mutation, I/O, DMA, IOMMU, Lane6, Lane7, or Stream requires an owner-specific RFC/ADR with capability, evidence, migration, completion, retire, and adjacent denial tests.

## Owner Of Authority

- Memory domain descriptors and translation controls for address-space facts.
- I/O/IOMMU domain descriptors for DMA windows and device authority.
- Lane6/Lane7 runtime/helper owners for their own native contours.
- Evidence policies for visibility.

VMX aliases own none of these.

## What Can Be Implemented

- Boundary tests proving VMX frontend does not import lane tokens/telemetry as authority.
- Migration non-leak tests for Lane6/Lane7/Stream evidence.
- Static scans for runtime/helper evidence flowing into VMCS projection.
- Schema and negative/static tests for the staged `VirtualizedTransactionIntent`/authorization-token/`VirtualizedTransactionReceipt` contract, without a production issuer or backend.
- For the recommended first VMCALL leaf, an explicit statement that no memory/I/O/lane side effects exist.

## What Remains Denied/Future-Gated

- VMX-driven DMA/IOMMU operations.
- Lane6/Lane7 passthrough.
- Stream helper state as virtualization authority.
- Migration of lane tokens as guest state.
- Compiler/backend lowering through lane surfaces for virtualization.

## Forbidden Shortcuts

- Using `DmaStreamCompute` tokens as domain authority.
- Treating Lane7 telemetry or fake backend results as completion authority.
- Treating Stream warm/prefetch success as replay, migration, or commit authority.
- Using IOMMU helper evidence as VMCS state.
- Treating lane placement as authority or treating a native lane token as virtualization, completion or retire authority.
- Replacing the staged owner-derived authorizations with one forgeable guest transaction envelope.

## Required RFC/ADR

Required if any activation path touches memory mutation, I/O, DMA, IOMMU, Lane6, Lane7, or Stream. It must define `VirtualizedTransactionIntent`, every existing-owner authorization, lane-local admission where required, attempt-bound opaque tokens and `VirtualizedTransactionReceipt`; it must not appoint a replacement monolithic owner. Not required for a no-state VMCALL leaf that explicitly has no such side effects.

## Code Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Memory/Translation/MemoryDomainReadOnlyTranslationView.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Lane6DmaStream/DmaStreamComputeMicroOp.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/DmaStreamCompute/DmaStreamComputeDescriptorParser.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Lane7Accelerator/SystemDeviceCommandMicroOp.cs`
- StreamEngine/SFR paths named in `Documentation/Stream WhiteBook/**`

## Documentation Anchors

- `HybridCPU_ISE/docs/ref2/Old/VirtualiztionRefactoringNew/10_memory_io_iommu_lanes_and_stream_boundary.md`
- `Documentation/Virtualization WhiteBook/07_Memory_IO_Lanes.md`
- `Documentation/Stream WhiteBook/DmaStreamCompute/01_Current_Contract.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/04_Authority_Model.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/10_Telemetry_And_Evidence.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/11_DmaStreamCompute_And_Assist_Separation.md`

## Required Tests

- Lane6/Lane7 tokens cannot migrate as guest state.
- Telemetry cannot authorize backend execution.
- Stream helper evidence cannot become VMCS state.
- VMX frontend cannot call lane runtime as authority.
- Memory-owned VMREAD stays read-only projection.
- Lane-local authority remains valid only for lane-local state and cannot substitute for virtualization/completion/retire authority.
- Missing, stale, cross-attempt or wrong-owner MemoryDomain, Io/IOMMU, Device or lane-local authorization denies the staged transaction.
- No receipt is created if any required authorization is absent, and no single envelope can bypass an existing owner.

## Required Static/Source Scans

```powershell
rg -n "DmaStreamCompute|Lane6|Lane7|Stream|Telemetry|ReplayEvidence|Token" HybridCPU_ISE/CloseToHSL/Core/Virtualization HybridCPU_ISE/CloseToHSL/Core/Runtime/Events HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion
```

Matches must be absent or explicitly denied/projection-only.

## Migration/Evidence Classification

Lane and Stream telemetry, replay evidence, backend binding caches, scheduler pressure, native tokens, and debug traces are host-owned or model/helper-only. They are not guest state and must not appear in migration/checkpoint images as authority.

## Completion/Retire Implications

Lane/Stream completions cannot satisfy virtualization completion/retire gates unless a future owner-specific RFC/ADR maps them through neutral route/fence policy. The recommended first VMCALL leaf should avoid these surfaces entirely.

## Exit Criteria

- Boundary tests and scans are defined.
- No lane/stream evidence is treated as virtualization authority.
- The attempt-bound staged transaction contract is either fully owner-mapped and typed or remains absent; a partial envelope is fail-closed.
- Any future lane-touching activation path is blocked behind RFC/ADR.

## Dependency On Previous/Next Phase

Depends on Phases 02 and 03. Phase 15 uses the same evidence classes for migration/checkpoint.
