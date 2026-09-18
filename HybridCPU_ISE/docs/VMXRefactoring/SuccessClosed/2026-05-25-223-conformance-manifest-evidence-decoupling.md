# Closure 223: Conformance Manifest Evidence Decoupling

Date: 2026-05-25

## Slice

Selected slice: decouple tests from the compiled `LegacyVmxQuarantineManifest` / `LegacyVmxQuarantineEntry` evidence contract.

This is intentionally not a whole `Legacy/VMX/Conformance` deletion. The goal was to remove one direct compiled test dependency and prove the selected evidence file can be moved away.

## Dependency Inventory

Direct compiled test dependents found:

- `HybridCPU_ISE.Tests/VmxRefactoring/VmxProjectionSchemaAndQuarantineTests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxCapsProjectionBoundaryTests.cs`

Direct dependency classes found in tests include:

- quarantine manifest evidence: `LegacyVmxQuarantineManifest`, `LegacyVmxQuarantineEntry`;
- removal contracts: `LegacyVmx*RemovalContract`, `LegacyVmcs*RemovalContract`, `LegacyShadowVmcsBlockRemovalContract`;
- return/freeze inventory contracts: `LegacyVmxV1ExecutionAdapterSurfaceReturnContract`, `LegacyVmxFreezeReadinessCertificationContract`, `LegacyVmxRetainedCompatibilitySurfaceInventoryContract`;
- substrate/frozen-alias/nested contracts: `CapabilityProjectionPlacementServiceSubstrateExtractionContract`, `CoreVmxSubstrateResidualExtractionContract`, `FinalFrozenAliasQuarantineContract`, `NestedDomainProjectionCheckpointOwnerContract`, `NestedCompositionContract`;
- obsolete retired behavior text under `#if false` remains historical specification text and is not compiled.

Classification:

- still useful as static/file evidence: quarantine manifest path inventory; retained/removed path assertions;
- historical proof already superseded by closure docs: previous manifest class as compiled conformance evidence;
- obsolete retired behavior/test comments: old executable `VmxExecutionUnit` and `VmcsManager` blocks under `#if false`;
- must remain until replacement conformance exists: removal contracts, freeze readiness, retained-surface, frozen-alias, capability/substrate, and nested-composition contracts.

## What Changed

- Added `HybridCPU_ISE.Tests/VmxRefactoring/VmxQuarantineEvidenceManifest.cs`.
- Rewrote `VmxProjectionSchemaAndQuarantineTests.cs` to use test-local `VmxQuarantineEvidenceManifest`, `VmxQuarantineEvidenceEntry`, and `VmxQuarantineReturnProof`.
- Removed compiled test references to `LegacyVmxQuarantineManifest`, `LegacyVmxQuarantineEntry`, and manifest-only `LegacyReverseImportRequest` usage.
- Left `Legacy/VMX/Conformance/AuthorityBoundary/LegacyVmxQuarantineManifest.cs` in place as historical evidence.

## Authority Boundary

Production authority was not touched.

- No production runtime owner was created.
- No VMCS field store, active pointer state, manager, adapter, or VMX backend path was introduced.
- No legacy-marked C# source was returned to `Core/VMX`.
- The new helper lives only in the tests project and carries static path evidence.

## Counts

- `Legacy/VMX` C# sources after the slice: `37`.
- `Legacy/VMX/Compatibility` C# sources: `0`.
- `Core/VMX` legacy-marked C# sources: `0`.

## Move-Away Probe

Selected-file move-away probe passed.

Moved temporarily:

```text
HybridCPU_ISE/Legacy/VMX/Conformance/AuthorityBoundary/LegacyVmxQuarantineManifest.cs
-> Desktop/New folder/vmx-conformance-probe-223-quarantine-manifest/LegacyVmxQuarantineManifest.cs
```

Probe result:

- production build exit: `0`;
- tests build exit: `0`;
- file restored in `finally`.

Whole-folder deletion was not claimed. Other conformance contracts still block full deletion.

## Verification

Baseline before changes:

- `Core/VMX` legacy-marked C# scan: `0`;
- `Legacy/VMX` C# source count: `37`;
- production build: passed;
- tests build: passed.

After implementation:

- production build: passed;
- tests build: passed;
- selected-file move-away production build: passed;
- selected-file move-away tests build: passed;
- static test scan for `LegacyVmxQuarantineManifest`, `LegacyVmxQuarantineEntry`, `LegacyReverseImportRequest`: no matches.

## Residual Risk

The test-local manifest is static evidence and can drift from the historical conformance manifest if future edits update only one copy. This is acceptable for the slice because the goal is compiled dependency removal, not a new runtime source of truth.

The whole conformance folder remains test-blocked by other direct contracts.

## Next Step

Continue with the next honest conformance decoupling slice, likely one of:

- replace a small removal-contract dependency with source/path assertions in tests; or
- retire an obsolete historical contract already superseded by closure docs.

If conformance decoupling becomes too broad, switch to the VMCSv2 mutable helper method audit for `VmcsV2Descriptor.cs` and `VmcsV2Blocks.cs`.
