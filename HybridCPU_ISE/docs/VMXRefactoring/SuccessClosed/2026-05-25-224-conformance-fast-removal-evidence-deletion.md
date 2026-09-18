# Closure 224: Conformance Fast Removal Evidence Deletion

Date: 2026-05-25

## Slice

Selected slice: delete the first fast static evidence pool from `Legacy/VMX/Conformance/AuthorityBoundary` after moving its useful path/marker assertions into test-local evidence.

This is intentionally not a whole `Legacy/VMX/Conformance` deletion. The goal was to reduce the compiled legacy conformance source count while keeping production authority untouched.

## Dependency Inventory

Direct compiled test callers found:

- `HybridCPU_ISE.Tests/VmxRefactoring/VmxProjectionSchemaAndQuarantineTests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxCapsProjectionBoundaryTests.cs`

Deleted evidence contracts:

- `LegacyVmxQuarantineManifest`
- `LegacyVmxV1AdapterBoundaryRemovalContract`
- `LegacyVmxV2AdapterBoundaryRemovalContract`
- `LegacyCsrBackedVmxCapabilityDescriptorSourceRemovalContract`
- `LegacyVmxTranslationInvalidationBackendRemovalContract`
- `LegacyVmxIoVirtualizationBackendRemovalContract`
- `LegacyVmxExecutionUnitRemovalContract`

Classification:

- still useful as static/file evidence: removed path assertions, current fail-closed routing paths, generated projection markers, neutral capability/memory/I/O owner markers;
- historical proof superseded by closure docs: compiled conformance shell files for the deleted contracts;
- obsolete retired behavior/test comments: old executable VMX behavior remains non-compiled historical text only;
- must remain until replacement conformance exists: retained-surface, freeze-readiness, frozen-alias, capability/substrate, nested-composition, VMCS removal, shadow VMCS block, and remaining removal contracts.

## What Changed

- Added `HybridCPU_ISE.Tests/VmxRefactoring/VmxLegacyFastRemovalEvidenceContracts.cs`.
- Deleted from `HybridCPU_ISE/Legacy/VMX/Conformance/AuthorityBoundary`:
  - `LegacyVmxQuarantineManifest.cs`
  - `LegacyVmxV1AdapterBoundaryRemovalContract.cs`
  - `LegacyVmxV2AdapterBoundaryRemovalContract.cs`
  - `LegacyCsrBackedVmxCapabilityDescriptorSourceRemovalContract.cs`
  - `LegacyVmxTranslationInvalidationBackendRemovalContract.cs`
  - `LegacyVmxIoVirtualizationBackendRemovalContract.cs`
  - `LegacyVmxExecutionUnitRemovalContract.cs`
- Left remaining conformance contracts in place. Whole-folder deletion is still blocked by tests.

## Authority Boundary

Production authority was not touched.

- No production runtime owner was created.
- No VMCS field store, active pointer state, manager, adapter, or VMX backend path was introduced.
- No legacy-marked C# source was returned to `Core/VMX`.
- The new helper lives only in the tests project and carries static path/marker evidence.

## Counts

- `Legacy/VMX` C# sources after the slice: `30`.
- `Legacy/VMX/Conformance` C# sources: `30`.
- `Legacy/VMX/Compatibility` C# sources: `0`.
- `Core/VMX` legacy-marked C# sources: `0`.

## Move-Away Probe

Selected-file move-away was replaced by physical deletion of the seven selected files. Production and tests builds passed after deletion.

Full-folder move-away probe after the deletion:

```text
HybridCPU_ISE/Legacy/VMX/Conformance
-> Desktop/New folder/vmx-conformance-full-probe-after-224/Conformance
```

Probe result:

- production build exit: `0`;
- tests build exit: `1`;
- folder restored in `finally`.

First remaining test blockers reported by the probe:

- `LegacyVmxRetainedCompatibilitySurfaceInventoryContract`
- `LegacyVmxRetainedCompatibilityInventoryEntry`
- `LegacyVmxFreezeReadinessCertificationContract`
- `LegacyVmxFreezeReadinessInventoryEntry`

## Verification

After implementation:

- production build: passed;
- tests build: passed;
- `VmxProjectionSchemaAndQuarantineTests`: `58/58` passed;
- broad `FullyQualifiedName~Vmx`: `258/258` passed;
- final production build: passed with `0` warnings and `0` errors;
- selected deleted file scan: no selected files remain under `Legacy/VMX/Conformance/AuthorityBoundary`;
- heavy carrier scan: `VmxExecutionUnit.cs`, `VmcsManager.cs`, and `IVmcsManager.cs` absent.

Static counts:

```text
CoreVmxLegacyMarkedCs=0
LegacyVmxCs=30
LegacyVmxCompatibilityCs=0
LegacyVmxConformanceCs=30
```

## Residual Risk

The test-local evidence duplicates path/marker strings that were previously compiled from conformance shells. This is acceptable for deletion progress because the remaining source of truth is the production file system plus closure/audit docs, not a VMX runtime owner.

Whole-folder deletion remains blocked by remaining compiled test dependencies.

## Next Step

Continue with the next conformance decoupling pool: retained-surface and freeze-readiness evidence. If that becomes too broad, switch to the VMCSv2 mutable helper authority audit for `VmcsV2Descriptor.cs` and `VmcsV2Blocks.cs`.
