# HybridCPU ISE Deep Audit Report

Generated: 2026-06-10T17:32:32.972147+00:00

## Executive Summary

Baseline build passes, but the test project fails on 11 architecture-boundary tests. The strongest active defects are vector-memory placement drift, hidden vector memory fallback/no-op paths, and a matrix/tile authority conflict between opened runtime/compiler surfaces and older fail-closed/facade ABI tests. Lane6 DSC, Lane7 SDC, and VMX are comparatively well anchored and should be preserved as reference patterns.

## Findings Summary

| ID | Severity | Status | Title | Phase |
|---|---|---|---|---|
| F-001 | High | Active | VLOAD/VSTORE materialize memory side effects as Vector/ALU and IsMemoryOp=false | P1/P2 |
| F-002 | High | Active | Vector segment and 2D memory micro-ops inherit ALU placement and contain fallback/no-op success paths | P2 |
| F-003 | High | Active | Matrix/tile contour is opened but its slot authority remains ALU for MTILE_LOAD/STORE | P3/P4 |
| F-004 | Medium | Active | App facade phase-01 ABI tests conflict with newly published matrix helper methods | P5 |
| F-005 | Medium | Active | Test suite contains stale negative/skip assumptions for already registered/opened contours | P0/P6 |
| F-006 | Medium | Latent | IsMemoryOp is overloaded and no longer matches memory ranges, ordering, or physical resource ownership | P1/P3 |
| F-007 | Medium | Latent | InstructionClassifier falls through to ScalarAlu/Free for unlisted opcodes | P3/P6 |
| F-008 | Medium | Active | CLMUL compiler test anchor expects contract text in the wrong source surface | P6 |
| F-009 | Low | Active | global.json SDK pin differs from selected local dotnet SDK | P0 |
| F-010 | Low | Confirmed-good | Lane6 DSC, Lane7 SDC, and VMX have strong hard-pinning evidence | P0/P6 |

## 1. Scope

Audit scope is the local tree at `C:/Users/Yuriy Kurnosov/Desktop/HybridCPU ISE`. I treated the filesystem as the source of truth and produced only audit artifacts under `_audit_output` and `Documentation/ActualRefactoring/DeepResearch/Plan1`.

## 2. Authority Model

Source authority order used here: production code first, executable tests second, docs third, generated inventories last. Git history was not used as an authority, per request.

## 3. Excluded Paths

The manifest excludes `.git`, IDE state, build outputs, packages, coverage, test result folders, and `_audit_output`. The full exclusion list is in `excluded-paths.txt`.

## 4. Snapshot

The scanner saw `3034` files after exclusions. Category counts: `{'other': 202, 'doc': 929, 'source': 1463, 'project': 31, 'config': 6, 'generated': 14, 'test': 389}`. Exact SHA-256 rows are in `source-manifest.sha256`.

## 5. Environment

The repo pins SDK `10.0.201` in `global.json`, while `dotnet --version` selected `10.0.204`. Build passed under the observed SDK, but this is a reproducibility drift.

## 6. Baseline Build

Sequential baseline command `dotnet build "HybridCPU v2.slnx" -v minimal --no-restore -m:1 /p:UseSharedCompilation=false` passed with 0 errors and 73 warnings. A prior parallel build/test lock failure is explicitly excluded from baseline evidence.

## 7. Baseline Tests

Baseline test command `dotnet test "HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj" -v minimal --no-build` failed: 7875 total, 7862 passed, 11 failed, 2 skipped. The failures cluster around compiler facade ABI, matrix/tile open-vs-closed authority, CLMUL source anchors, and vector no-emission expectations.

## 8. Project Inventory

The solution contains 29 `.csproj` files plus `HybridCPU v2.slnx`, `Directory.Build.props`, and `global.json`. Details are in `project-inventory.csv`.

## 9. Bundle Topology

`BundleMetadata.BundleSlotCount = 8` (`HybridCPU_ISE/NonRTL/Core/Pipeline/Metadata/BundleMetadata.cs:19`) and `AbstractBundle.cs:7` describes `8 slots x 32 bytes = 256 bytes`; fetch uses a 256-byte buffer at `CPU_Core.PipelineExecution.StageFlow.cs:193`.

## 10. Carrier And Sideband

`VliwBundleAnnotations.cs:10-16` carries sideband annotations, and `VliwBundleAnnotations.cs:45-63` builds `CoreBundleMetadata` only when explicit slot metadata exists. The raw carrier and scheduler metadata are therefore separate surfaces.

## 11. Slot Classes

`SlotClassDefinitions.cs:17-35` defines ALU, LSU, DMA stream, branch, system, and unclassified classes. Lane masks at `SlotClassDefinitions.cs:173-196` map ALU to lanes 0-3, LSU to 4-5, DMA to 6, and branch/system to lane 7.

## 12. Lane 7 Alias

`SlotClassDefinitions.cs:220-224` explicitly aliases `BranchControl` and `SystemSingleton` on lane 7. Hard-pinned system, trap, VMX, and SDC operations match this shape.

## 13. MicroOp Authority

`MicroOp.cs:621` owns `Placement`; `MicroOp.cs:644-666` exposes flexible/hard-pinned placement helpers; `ApplyCanonicalDecodeProjection` at `MicroOp.cs:623-642` can overwrite class, serialization, placement, and memory predicates from decode projection.

## 14. Admission Metadata

`MicroOpAdmissionMetadata.cs:14-30` captures memory/control/register/placement facts and `MicroOpAdmissionMetadata.cs:98-103` copies `IsMemoryOp` and `Placement` directly from the materialized micro-op. Bad micro-op taxonomy therefore propagates into scheduling evidence.

## 15. Instruction Class Taxonomy

`InstructionClass.cs:8-33` has ISA-level classes including `Memory`, `System`, `SmtVt`, and `Vmx`. This is not equivalent to physical slot class; the audit found several places where those concepts are conflated.

## 16. IR Taxonomy

`IrEnums.cs:24-33` defines compiler resource classes including `VectorAlu`, `LoadStore`, `DmaStream`, and `System`. The compiler/runtime bridge needs explicit mapping to `SlotClass`, not inference from names.

## 17. Opcode Registry

`OpcodeInfo.Registry.Helpers.cs:64-80` publishes semantics from `OpcodeRegistry.Opcodes`. `OpcodeInfo.Registry.Data.Vector.cs:69-74` currently publishes VLOAD, VSTORE, and MTILE rows.

## 18. Classifier Defaults

`InstructionClassifier.cs:26-40` consults the registry first, but `InstructionClassifier.cs:233-234` defaults unlisted instruction classes to `ScalarAlu`, and `InstructionClassifier.cs:373-374` defaults serialization to `Free`. This is a latent fail-open classifier path for classifier-only consumers.

## 19. Decoder Chain

`DecodedBundleTransportProjector.cs:343-351` calls `InstructionRegistry.CreateMicroOp` and then applies canonical projection when required. This makes `InstructionRegistry` and concrete micro-op metadata the decisive runtime authority.

## 20. InstructionRegistry Chain

`InstructionRegistry.Runtime.cs:70-87` applies registered factories/descriptors; `InstructionRegistry.Runtime.cs:106-107` throws for unknown opcodes. Registry materialization is fail-closed, but descriptor overrides can still preserve inconsistent placement/memory facts.

## 21. VLOAD VSTORE

`InstructionRegistry.Initialize.Vector.cs:21-24` says legacy transfer opcodes now materialize concrete micro-ops. The factory at `InstructionRegistry.Helpers.Vector.cs:162-180` returns `VectorTransferMicroOp`; the class at `VectorMicroOps.Data.cs:727-755` sets memory ranges but `IsMemoryOp=false`, and inherits ALU placement from `VectorMicroOps.cs:48-58`.

## 22. Vector Segment Memory

`LoadSegmentMicroOp`, `Load2DMicroOp`, `StoreSegmentMicroOp`, and `Store2DMicroOp` inherit ALU placement from the base vector class while executing memory through `MemorySubsystem` or `BurstIO` (`VectorMicroOps.Memory.cs:18-158,722-876`). Store segment/2D can return success with no store buffer.

## 23. Indexed Vector Memory

`GatherMicroOp` and `StoreScatterMicroOp` are the good reference: constructors set `Class=MicroOpClass.Lsu`, `InstructionClass.Memory`, and `SetClassFlexiblePlacement(SlotClass.LsuClass)` (`VectorMicroOps.Memory.cs:168-190,884-907`).

## 24. IsMemoryOp Overload

The same boolean means different things: `VectorTransferMicroOp` and indexed vector memory can publish memory ranges with `IsMemoryOp=false`, while DSC uses `IsMemoryOp=true` for lane6 descriptor runtime. This should be split into footprint/effect/ordering/slot-owner predicates.

## 25. Lane 6 DSC

`DmaStreamComputeMicroOp.cs:42-53` hard-pins DSC to `DmaStreamClass` lane 6, sets `InstructionClass.Memory`, and marks side effects. Runtime execution and retire commit are separated at `DmaStreamComputeMicroOp.cs:121-149`. This is a confirmed-good anchor.

## 26. DSC Guarding

`DmaStreamComputeDescriptorParser.cs:139-204` fail-closes unguarded DSC1 parsing and requires guard-plane/owner evidence for guarded parse. This is the pattern to preserve while refactoring memory taxonomy.

## 27. Lane 7 SDC

`SystemDeviceCommandMicroOp.cs:57-67` pins SDC to lane 7 system singleton. `SystemDeviceCommandMicroOp.cs:196-255` contains fail-closed paths for descriptorless submit and guest-lane compatibility. This is another confirmed-good anchor.

## 28. VMX

`MicroOp.IO.cs:139-164` defines `VmxMicroOp` as `InstructionClass.Vmx`, `SerializationClass.VmxSerial`, hard-pinned to lane 7. VMX tests assert registry/classifier/materializer consistency.

## 29. Matrix Tile Registry

`InstructionRegistry.Helpers.MatrixTile.cs:12-15` registers MTILE_LOAD, MTILE_STORE, MTILE_MACC, and MTRANSPOSE; `InstructionRegistry.Helpers.MatrixTile.cs:53-72` publishes descriptors; `InstructionRegistry.Helpers.MatrixTile.cs:142-147` creates typed MTILE micro-ops.

## 30. Matrix Tile Placement

`MatrixTileMicroOps.cs:71` sets ALU flexible placement for every matrix/tile micro-op; `MatrixTileMicroOps.cs:128-143` later marks MTILE_LOAD/STORE as `InstructionClass.Memory`, `IsMemoryOp=true`, and builds memory resources. This is the central matrix/tile authority conflict.

## 31. Matrix Tile Tests

Positive retire/golden tests prove the contour is open (`Phase10MatrixTileRetirePublicationTests.cs:18-76`, `Phase12MatrixTilePositiveGoldenArtifactTests.cs:129-144`), while baseline fail-closed tests still expect traps for MTILE rows. One side must become authoritative.

## 32. Compiler Facade

`IAppAsmFacade.cs:96-113` and `AppAsmFacade.cs:684-721` publish matrix helper methods. `CompilerFacadeAbiPhase01Tests.cs:288-354` expects the old phase-01 inventory; `Phase12FacadeFamilyDeprecationTests.cs:60-79` forbids new production mentions outside facade boundaries.

## 33. Compiler Positive Emission

`ThreadCompilerContext.MatrixTile.cs:10-62` contains compile helpers; `CompilerMatrixTilePositiveEmissionAbiContract.cs:282-300` names them as positive emission helpers. This conflicts with older no-emission assumptions but may represent intended Phase13 handoff.

## 34. CLMUL Anchor Drift

The failed CLMUL test expects the compiler source under inspection to contain `CompilerDeferredScalarAbiContract`; the contract type exists in `CompilerDeferredScalarAbiContract.cs:16-318`. This looks like test-source selection drift rather than a missing contract.

## 35. Skipped Tests

`Phase03CarrierProjectionTransportTailTests.cs:611,640` skip VLOAD/VSTORE tests claiming they are absent from OpcodeRegistry, but `OpcodeInfo.Registry.Data.Vector.cs:69-70` now registers them. These skips are stale.

## 36. Reachability

Curated opcode-to-runtime reachability is in `reachability-matrix.csv`. The highest-risk rows are VLOAD/VSTORE, segment/2D vector memory, and MTILE_LOAD/STORE.

## 37. Coverage

Coverage summary is in `test-coverage-matrix.csv`. The suite is large and mostly passing, but the failing and skipped tests sit exactly on architecture-boundary truth claims.

## 38. Findings

`findings.csv` contains 10 rows: 3 high active findings, 4 medium active/latent findings, 1 SDK reproducibility issue, and 1 confirmed-good anchor row for Lane6/Lane7/VMX.

## 39. Refactoring Plan

`refactoring-phases.md` proposes P0-P7: freeze baseline, add invariant guards, normalize vector memory, split memory semantics, decide matrix/tile authority, resolve compiler facade handoff, repair tests, and harden CI.

## 40. Residual Risk

No production files were edited. The main residual risk is architectural ambiguity: whether vector/matrix memory operations should be LSU-owned, runtime-capture-owned, or explicitly exempted with tests and docs.


## Appendix A - Failed Baseline Tests

- CompilerFacadeAbiPhase01Tests.AppFacadePublicAbi_StaysAtPhase01CompatibilityInventory
- Phase12FacadeFamilyDeprecationTests.FacadeInterfaces_NoNewProductionMentionsOutsideFacadeBoundary
- Phase09VectorPermute2ExecutableTests.Vperm2_CompilerSurfaceRemainsNoEmissionForDeltaHelpers
- Phase09VectorTransposeExecutableTests.Vtranspose_CompilerSurfaceRemainsNoEmissionForDeltaHelpers
- Phase09VectorSlideOneDownExecutableTests.Vslide1Down_CompilerSurfaceRemainsNoEmissionForDeltaHelpers
- Phase09VectorSlideOneUpExecutableTests.Vslide1Up_CompilerSurfaceRemainsNoEmissionForDeltaHelpers
- Phase03ScalarCarryLessClmulExecutableTests.Clmul_CompilerEmission_OpensCarryLessRowsWhileAdjacentContoursRemainClosed
- Phase03FallbackTransportSummaryTailTests.DecodeFullBundle_UnsupportedOptionalMatrixMemoryContours_FailClosedAsDecodeFaultTrap(MTILE_LOAD)
- Phase03FallbackTransportSummaryTailTests.DecodeFullBundle_UnsupportedOptionalMatrixMemoryContours_FailClosedAsDecodeFaultTrap(MTILE_STORE)
- Phase03FallbackTransportSummaryTailTests.DecodeFullBundle_UnsupportedOptionalMatrixContours_FailClosedAsDecodeFaultTrap(MTILE_MACC)
- Phase03FallbackTransportSummaryTailTests.DecodeFullBundle_UnsupportedOptionalMatrixContours_FailClosedAsDecodeFaultTrap(MTRANSPOSE)

## Appendix B - Skipped Baseline Tests

- Phase03CarrierProjectionTransportTailTests.LegacySlotCarrierMaterializer_VectorLoadProjection_PublishesTwoSurfaceTransferMemoryShape
- Phase03CarrierProjectionTransportTailTests.LegacySlotCarrierMaterializer_VectorStoreProjection_PublishesTwoSurfaceTransferMemoryShape
