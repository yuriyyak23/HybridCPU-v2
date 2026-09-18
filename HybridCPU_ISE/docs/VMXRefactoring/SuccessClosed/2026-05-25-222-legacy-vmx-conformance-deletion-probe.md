# 2026-05-25 Task 222: Legacy VMX Conformance Deletion Probe

## Scope

Probe whether `Legacy/VMX/Conformance` can be deleted safely after VMX compatibility frontend freeze.

## Probe

- Temporarily moved `HybridCPU_ISE/Legacy/VMX/Conformance` to `C:/Users/Yuriy Kurnosov/Desktop/New folder/VMX-conformance-delete-probe-*`.
- Ran production build.
- Ran tests build.
- Restored the folder in `finally`.

## Result

- Production build passed without `Legacy/VMX/Conformance`.
- Tests build failed without `Legacy/VMX/Conformance` with `190` compile errors.
- Missing symbols are evidence contracts and manifest types, including:
  - `LegacyVmxQuarantineManifest`
  - `LegacyVmxQuarantineEntry`
  - `LegacyShadowVmcsBlockRemovalContract`
  - `LegacyVmxV1ExecutionAdapterSurfaceReturnContract`
  - `LegacyVmxExecutionUnitRemovalContract`
  - `LegacyVmcsManagerVmxPublicationAuthorityRemovalContract`
  - `LegacyVmcsManagerRemovalContract`

## Classification

- `Legacy/VMX/Conformance` is not a production runtime or ABI dependency.
- It owns no execution, memory, I/O, lane, nested, capability, host-evidence, checkpoint, completion, or retire authority.
- It is still compiled test-only/historical evidence used directly by:
  - `HybridCPU_ISE.Tests/VmxRefactoring/VmxProjectionSchemaAndQuarantineTests.cs`
  - `HybridCPU_ISE.Tests/VmxRefactoring/VmxCapsProjectionBoundaryTests.cs`

## Decision

Do not delete `Legacy/VMX/Conformance` in this step.

The folder is production-independent, but deletion is blocked by direct compiled test evidence dependencies. Safe deletion requires a separate decoupling pass that rewrites those tests to static/file evidence or retires the historical proof contracts, then repeats the same physical move-away probe.

## Inventory

- `Legacy/VMX/Compatibility`: `0` `.cs`
- `Legacy/VMX/Conformance`: `37` `.cs`
- Total `Legacy/VMX`: `37` `.cs`
- `VmxExecutionUnit.cs`: absent
- `VmcsManager.cs`: absent
- `IVmcsManager.cs`: absent

## Freeze Status

VMX compatibility frontend freeze remains declared for the current compiled production/frontend surface.

This closure does not add or remove runtime authority. It records a post-freeze evidence cleanup blocker.

## Next Step

Decouple VMX tests from compiled `Legacy/VMX/Conformance` contracts and manifest types, then rerun:

- production build with the folder moved away;
- tests build with the folder moved away;
- focused VMX projection/quarantine tests;
- broad `FullyQualifiedName~Vmx` matrix.
