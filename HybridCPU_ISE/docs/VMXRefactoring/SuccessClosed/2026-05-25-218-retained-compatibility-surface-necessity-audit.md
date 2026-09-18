# Task 218: retained compatibility surface necessity audit

Date: 2026-05-25
Status: closed

## Scope

This task audits the three remaining compiled production sources under `Legacy/VMX/Compatibility` after tasks `213`-`217`.

Retained production sources:

- `Legacy/VMX/Compatibility/Frontend/Decode/VmxInstructionPayload.cs`
- `Legacy/VMX/Compatibility/Frontend/Retire/VmxRetireModel.cs`
- `Legacy/VMX/Compatibility/Generated/VmcsProjection/ShadowVmcsNestedProjectionService.cs`

No production source was deleted in this task.

## Necessity classification

- `VmxInstructionPayload` remains necessary frozen opcode/decode payload vocabulary. It is consumed by `VmxCompatDecodeBoundary`, `InstructionIR`, and `VmxMicroOp` construction.
- `VmxRetireModel` remains necessary typed retire effect/result vocabulary. Current production callers use it to publish fail-closed VMX effects and retire outcomes.
- `ShadowVmcsNestedProjectionService` remains necessary generated compatibility bridge vocabulary because `NestedDomainControllerCompatibilityProjection` constructs it. It remains fail-closed with `CompatibilityProjectionFailed`.

## Authority mutation audit

The retained sources are carrier/projection vocabulary only. The new retained-surface contract rejects direct markers for:

- typed grant creation or mutation;
- CSR/hardware writes;
- VMCS manager or restored frontend ownership;
- host-owned evidence stores;
- memory generation, IOTLB, DMA, and I/O authority mutation;
- Lane6/Lane7 runtime state mutation;
- completion routing ownership;
- checkpoint/restore ownership;
- nested projection/checkpoint service construction.

Production `Core` retire routing remains fail-closed. Current production sources call `VmxRetireEffect.Fault`; success/mutation factories such as `VmcsRead`, `VmcsWrite`, `VmcsPointerEffect`, `VmCall`, `Invalidation`, `VmFunc`, `ExtendedState`, `InterceptExit`, `Control`, and `Abort` are not used by production `Core`.

## Generated/debug/lifecycle conformance inventory

Classified as evidence-only and not runtime ownership:

- ABI freeze contract;
- generated projection lineage build contract;
- virtualization golden artifact manifest;
- no-emission and compatibility-write no-mutation contracts;
- migration replay denial contract;
- fail-trace publication removal contract;
- VMCS pointer lifecycle removal contract;
- debug-trace substrate extraction contract.

These contracts remain conformance gates, not VMX/VMCS runtime managers.

## Inventory

- Production `.cs` under `Legacy/VMX/Compatibility`: `3`.
- Total `.cs` under `Legacy/VMX`: `39` after adding `LegacyVmxRetainedCompatibilitySurfaceInventoryContract`.
- `Core/VMX` remains free of legacy-marked `.cs`.
- `VmxExecutionUnit.cs`, `VmcsManager.cs`, and `IVmcsManager.cs` remain absent without replacement.

## Verification

- Production build after code change: passed with projection lineage verified; `54` existing warnings, `0` errors.
- Tests build after code change: passed; `93` existing warnings, `0` errors.
- `FullyQualifiedName~LegacyVmxRetainedCompatibilitySurfaceInventory`: passed `1/1`.
- `FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests`: passed `57/57`.

## Residual risk / next heavy step

This is not a VMX freeze declaration.

The next heavy step is final freeze-readiness certification: run the compatibility frontend, generated-lineage, no-emission, golden artifact, migration replay, host-evidence, and broad repository-shape debt filters together, then separate known unrelated broad-filter failures from any remaining architectural blockers.
