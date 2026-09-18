# Closure 245: Control VMREAD Values Explicitly Denied

Date: 2026-05-29

## Decision

Selected the clean option for the current code state: keep control VMREAD values denied.

The alternative, a small generated/read-only compatibility-control mapper, is not admitted because `CompatibilityControlDescriptor` currently exposes neutral fail-closed control semantics, not a frozen VMX control-bit value contract. Opening even one control field now would require either an inferred bit mapping or a fake compatibility value.

## Selected Slice

`VmcsReadOnlyValueProjectionService` now recognizes `VmcsFieldProjectionOwner.CompatibilityControlDescriptor` only to return an explicit denial:

- `CompatibilityControlValueProjectionDenied`.

This replaces the generic `NeutralOwnerValueSourceMissing` outcome for control-owned schema entries and makes the architectural choice visible in conformance.

## Fields Kept Denied

Still denied after runtime admission and generated schema owner lookup:

- `PinBasedControls`;
- `ProcBasedControls`;
- `ExitControls`;
- `EntryControls`;
- `SecondaryProcControls`.

They do not project `0`, a VMCS cached value, a policy enum value, or any synthesized control-bit mask.

## Neutral Owner State

`CompatibilityControlDescriptor` remains the neutral control-semantics owner under `Core/Runtime/Capabilities/CompatibilityControls`.

Its materialized `CompatibilityControlReadOnlyView.FailClosedProjectionOnly` records:

- runtime admission requirements;
- read-projection-only execution;
- write, backend-execution, and authoritative-mutation denial;
- neutral trap/result/fence requirements;
- neutral completion/publication requirements;
- entry/admission validation requirements;
- nested-intent and memory-owner requirements;
- denied control-value projection.

Those semantics are not VMX control-bit values.

## Why No Control Mapper Was Added

A compatibility-control mapper would need a separate neutral control-bit value contract that defines field-by-field projection semantics for frozen VMX control aliases. That contract does not exist yet.

Keeping controls denied leaves fewer legacy tails:

- no VMX/VMCS control vocabulary becomes runtime authority;
- no fake zero-value projection;
- no scalar VMCS cache fallback;
- no active VMCS pointer;
- no VMCS manager;
- no backend success path.

## Tests

Updated `VmxCompatibilityControlOwnerDesignTests` to prove:

- materialized neutral control semantics still keep control VMREAD values denied;
- control-owned schema entries return `CompatibilityControlValueProjectionDenied`;
- the denial reason states that no frozen VMX control-bit value projection contract is admitted;
- `VmcsReadOnlyValueProjectionService` still has no `ProjectCompatibilityControl` mapper and contains no concrete control-field value cases;
- no `ReadFieldValue`, `WriteFieldValue`, `VmxExecutionUnit`, or `VmcsManager` path appears.

## Documentation

Updated:

- `audit3.md`;
- `audit4.md`;
- `audit5.md`;
- `2026-05-24-vmx-current-model-completion-audit.md`;
- `ОСНОВЫ и ПРАВИЛА VMX.md`.

## Verification

Builds:

- production build: passed (`HybridCPU_ISE.csproj --no-restore`, existing warnings only).
- tests build: passed (`HybridCPU_ISE.Tests.csproj --no-restore`, existing warnings only).
- console compatibility build: passed (`TestAssemblerConsoleApps.csproj --no-restore`, existing obsolete-constructor warnings only).

Tests:

- `VmxCompatibilityControlOwnerDesignTests`: 4 passed.
- `VmxMemoryOwnedVmReadValueProjectionTests` plus `VmxGeneratedReadOnlyVmReadValueProjectionTests`: 12 passed.
- `RuntimeBoundaryAdmissionTests`: 4 passed.
- `VmxFirstAdmittedCompatibilityPathTests`: 1 passed.
- `VmxProjectionSchemaAndQuarantineTests`: 1 passed.
- `VmxCompatibilityProjectionInventoryTests`: 1 passed.
- broad VMX excluding NonVmx: 230 passed.

Static:

- forbidden production virtualization marker scan excluding conformance: no matches.
- `Virtualization/Substrate` project include scan: no matches.
- `VmxExecutionUnit.cs`, `VmcsManager.cs`, and `IVmcsManager.cs` scan outside `CloseToHSL`: no files.
- `git diff --check`: no content errors; Git reported existing LF-to-CRLF working-copy warnings.

Note: the first console compatibility build was run in parallel with other builds and failed on a transient file lock against `HybridCPU_ISE.dll`, followed by cascade missing-type errors. Rerunning the console build by itself passed.

## Residual Risk

Control fields remain intentionally non-projectable. If a future workload needs control-bit compatibility values, it must first introduce a neutral control-bit value contract and then a generated/read-only mapper with conformance proving no VMCS field store fallback.

## Next Heavy Step

Runtime-owned trap completion route design: if VMCALL/intercept ever needs real publication, introduce the neutral completion-route owner and admission policy first, then let VMX project the already-authorized result.
