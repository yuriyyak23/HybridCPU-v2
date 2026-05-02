# L7-SDC Migration Phase Index

Status: phase index; phases 00-14 are closed, phase 15 full validation is the
next open gate.

The detailed migration plan is split into per-phase files under
`Documentation/CustomExternalAccelerator/Phases`.

Every phase must preserve:

- fixed W=8 bundle topology
- `DmaStreamCompute` lane6 `DmaStreamClass`
- external accelerator command hard-pinned to lane7 with
  `SetHardPinnedPlacement(SlotClass.SystemSingleton, 7)`
- no `BranchControl` authority for external accelerator commands
- typed descriptor sideband only
- clean native raw carrier validation
- owner/domain guard before descriptor, capability, submit, execution,
  commit, and exception publication
- pinned or epoch-validated mappings for detachable/suspendable tokens
- token and telemetry as evidence/container only
- staged writes as the only path to architectural commit
- no production call from L7-SDC into `ICustomAccelerator.Execute()`
- no runtime fallback to DmaStreamCompute, StreamEngine, VectorALU,
  GenericMicroOp, ALU, scalar, or legacy custom execution

Common phase closure gate:

1. Add or update focused tests for the phase.
2. Run the focused test filter named by the phase file.
3. Run the existing affected baseline filters named by the phase file.
4. Run the diagnostics console:

```powershell
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

5. Compare the new diagnostics output and artifact summary against
   `Documentation/AsmAppTestResults.md`.
6. Treat IPC, retired-instruction, cycle, stall, legality-reject,
   issue-width, memory-stall, and branch/control/system-progress drift as a
   regression unless the phase file explicitly records an intentional change.
7. Update `Documentation/AsmAppTestResults.md` only when the delta is understood
   and documented.

Current code-alignment note:

- Existing ISE/compiler/test surfaces referenced by the phase files were checked
  against the live tree on 2026-04-28 after Phase 13 closure and the Phase 14
  documentation quarantine pass.
- The L7-SDC implementation area exists under
  `HybridCPU_ISE/Core/Execution/ExternalAccelerators` with `Auth`,
  `Backends`, `Capabilities`, `Commit`, `Descriptors`, `Fences`, `Memory`,
  `Queues`, `Telemetry`, and `Tokens` surfaces.
- `HybridCPU_ISE/Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs` exists
  and contains the fail-closed lane7 `SystemSingleton` carrier family.
- There is no separate `AcceleratorCommandMicroOps.cs`; operation-specific
  L7-SDC micro-op classes live in `SystemDeviceCommandMicroOp.cs`.
- Phase 12 compiler surfaces exist under `HybridCPU_Compiler` and preserve
  explicit accelerator intent, typed sideband transport, lane7
  `SystemSingleton` placement, and no runtime fallback after emitted
  `ACCEL_SUBMIT`.
- Phase 13 telemetry surfaces exist under
  `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Telemetry` and
  `HybridCPU_ISE/Core/Diagnostics`; telemetry snapshots remain immutable
  evidence and are not guard, capability, submit, backend, commit,
  cancellation, fence, fault, or exception-publication authority.
- The current stream/DMA documentation folder is
  `Documentation/Stream WhiteBook/StreamEngine DmaStreamCompute`.
- The old requested `Documentation/MemoryAccelerators` name is provenance only,
  not a live repository path.
- Detailed code-audit results are recorded in
  [03_L7_SDC_Phase_Code_Audit.md](03_L7_SDC_Phase_Code_Audit.md).

Closed implementation state through Phase 14:

- Phase 00: audit and naming closure.
- Phase 01: legacy scaffold quarantine confirmation.
- Phase 02: capability registry metadata-only.
- Phase 03: lane7 system opcode surface validation and closure.
- Phase 04: descriptor ABI parser and native carrier validation.
- Phase 05: owner/domain and mapping/IOMMU epoch guard integration.
- Phase 06: `AcceleratorToken` lifecycle and register ABI.
- Phase 07: null/fake backend and queue model.
- Phase 08: staged writes and guard-backed commit contour.
- Phase 09: deterministic poll, wait, cancel, fence, fault-publication, and
  bounded lane7 pressure model.
- Phase 10: memory conflict manager and active footprint truth.
- Phase 11: MatMul metadata-only capability provider, typed descriptor schema,
  resource model, and staging-only fake backend contour.
- Phase 12: compiler explicit-intent emission path, typed sideband transport,
  lane7 `ACCEL_SUBMIT` lowering, and no runtime fallback after emitted
  accelerator command rejection.
- Phase 13: telemetry/evidence counters, immutable snapshots, and additive
  diagnostics export; telemetry remains evidence only.
- Phase 14: documentation quarantine and claim-safety audit against live
  implementation boundaries.

Next open gate:

- Phase 15 - Full validation baseline and rollback. Run the complete validation
  baseline without widening L7-SDC authority, placement, telemetry, compiler, or
  backend semantics.

Phase files:

- [Phase 00 - Audit and naming closure](Phases/Phase_00_Audit_And_Naming_Closure.md)
- [Phase 01 - Legacy scaffold quarantine confirmation](Phases/Phase_01_Legacy_Scaffold_Quarantine.md)
- [Phase 02 - Capability registry metadata-only](Phases/Phase_02_Capability_Registry_Metadata_Only.md)
- [Phase 03 - Lane7 system opcode surface](Phases/Phase_03_Lane7_System_Opcode_Surface.md)
- [Phase 04 - Descriptor ABI parser and carrier validation](Phases/Phase_04_Descriptor_ABI_Parser_And_Carrier_Validation.md)
- [Phase 05 - Owner/domain and mapping-epoch guard integration](Phases/Phase_05_Owner_Domain_And_Mapping_Guards.md)
- [Phase 06 - AcceleratorToken lifecycle and register ABI](Phases/Phase_06_AcceleratorToken_Lifecycle_And_Register_ABI.md)
- [Phase 07 - Null/fake backend and queue model](Phases/Phase_07_Null_Fake_Backend_And_Queue_Model.md)
- [Phase 08 - Staged writes and commit contour](Phases/Phase_08_Staged_Writes_And_Commit_Contour.md)
- [Phase 09 - Fault, cancel, fence, and wait semantics](Phases/Phase_09_Fault_Cancel_Fence_Wait_Semantics.md)
- [Phase 10 - Memory conflict manager](Phases/Phase_10_Memory_Conflict_Manager.md)
- [Phase 11 - MatMul capability provider migration](Phases/Phase_11_MatMul_Capability_Provider.md)
- [Phase 12 - Compiler emission path](Phases/Phase_12_Compiler_Emission_Path.md)
- [Phase 13 - Telemetry and evidence surfaces](Phases/Phase_13_Telemetry_And_Evidence.md)
- [Phase 14 - Documentation quarantine and claim safety](Phases/Phase_14_Documentation_Quarantine_And_Claim_Safety.md)
- [Phase 15 - Full validation baseline and rollback](Phases/Phase_15_Full_Validation_And_Rollback.md)
