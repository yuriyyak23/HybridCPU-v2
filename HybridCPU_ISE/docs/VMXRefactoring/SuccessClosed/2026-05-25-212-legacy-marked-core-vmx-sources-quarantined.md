# Task 212: legacy-marked Core VMX sources physically quarantined

Date: 2026-05-25
Status: closed

## Rule / basis

- The current audit requires VMX to remain a frozen compatibility frontend over neutral runtime/domain owners.
- Physical visibility matters for the remaining removal program: compiled sources that explicitly carry legacy vocabulary must not appear to be ordinary `Core/VMX` ownership.

## Closed slice

- Relocated every `.cs` source containing `legacy` (case-insensitive) from `Core/VMX` into `Legacy/VMX`, preserving its relative path below the old root.
- The initial inventory identified 36 files; two nested conformance contracts acquired the new quarantine path while references were corrected and were relocated by the same invariant, for 38 quarantined C# sources in the final state.
- Moved compiled compatibility production surfaces include denied invalidation/IOTLB backends, v1/v2 adapter boundaries, the CSR-backed fail-closed capability source, legacy-marked VMX decode/retire vocabulary, and the generated Shadow VMCS nested projection service.
- Moved legacy-named conformance surfaces keep their existing namespaces and continue compiling as proof artifacts from their explicit quarantine location.

## Authority status

- This is a physical classification step only. It does not restore success-path VMX execution or create a legacy runtime owner.
- `LegacyVmxTranslationInvalidationBackend` and `LegacyVmxIoVirtualizationBackend` remain denied/no-effect aliases.
- `LegacyCsrBackedVmxCapabilityDescriptorSource` remains a fail-closed empty capability projection.
- Existing neutral runtime owners for memory, I/O, lanes, nested projection/checkpoint, completion, and evidence remain unchanged outside `Legacy/VMX`.
- `VmxExecutionUnit.cs`, `VmcsManager.cs`, and `IVmcsManager.cs` remain absent.

## Conformance update

- `LegacyVmxQuarantineManifest` now marks physically retained compatibility artifacts as required quarantine and keeps removed authority origins classified as removed.
- Path-based compatibility/extraction contracts were redirected to the quarantine paths.
- `VmxProjectionSchemaAndQuarantineTests` now proves that `Core/VMX/**/*.cs` contains no `legacy` marker, that the quarantine is populated intentionally, and that removed heavy carriers are not restored.
- Old assertions that `Legacy/VMX` must be empty were replaced with the new no-heavy-carrier quarantine proof.

## Verification

- Baseline production build before relocation: passed with projection lineage verified.
- Production build after relocation: passed with projection lineage verified; 54 existing warnings, 0 errors.
- Tests build after relocation: passed with projection lineage verified; 93 existing warnings, 0 errors.
- `FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests`: passed 55/55.
- `FullyQualifiedName~CoreVmxAuthorityBoundaryTests`: passed 1/1.
- Focused `LegacyMarkedCoreVmxSources|FinalFrozenAliasQuarantine|LegacyVmxQuarantineManifest|RemovedLegacyVmxExecutionUnit|LegacyVmcsManager` filter: passed 22/22.
- Static scan: no `.cs` under `Core/VMX` contains `legacy`; `Legacy/VMX` contains 38 quarantined C# sources.

## Residual risk / next heavy step

- Centralization makes the next executable audit sharper: enumerate compiled production files under `Legacy/VMX`, classify reachability and ABI necessity, remove dead aliases without replacement, and retain only generated/read-only or typed fail-closed frozen compatibility surfaces.
- Do not declare VMX freeze until the compiled quarantine plus remaining generated/debug/lifecycle vocabulary has been reviewed end to end.
