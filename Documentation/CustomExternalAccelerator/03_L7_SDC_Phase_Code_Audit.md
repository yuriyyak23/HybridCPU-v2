# L7-SDC Phase Plans Code Audit

Status: phase-plan to live-code alignment audit; updated after Phase 14
documentation quarantine and claim-safety closure.

Audit date: 2026-04-28.

Scope:

- `Documentation/CustomExternalAccelerator/Phases/*.md`
- `Documentation/CustomExternalAccelerator/00_L7_SDC_Executive_Spec.md`
- `Documentation/CustomExternalAccelerator/01_L7_SDC_Migration_Phases.md`
- `Documentation/CustomExternalAccelerator/02_L7_SDC_Test_And_Rollback_Plan.md`
- live ISE/compiler/test surfaces referenced by the phase plans

## Audit method

The phase plans were checked against the live repository using PowerShell
inventory and targeted symbol/path searches:

- enumerate all phase files
- extract path-like references from backticks
- verify existing path references with `Test-Path` or wildcard expansion
- inspect existing methods/types with `Select-String`
- classify missing references as either stale-path errors or planned new
  L7-SDC implementation surfaces
- patch stale references in the phase plans

This audit was refreshed after the Phase 14 documentation quarantine pass. The
Phase 12 compiler-emission gate, Phase 13 telemetry/evidence gate, affected
baselines, and `TestAssemblerConsoleApps matrix-smoke` diagnostics passed on
2026-04-28. Each future implementation phase still has an explicit closure gate
that requires focused tests and diagnostics comparison with
`Documentation/AsmAppTestResults.md`.

## Confirmed live code anchors

Placement and scheduling:

- `MicroOp.SetHardPinnedPlacement(SlotClass requiredSlotClass, byte pinnedLaneId)`
  exists in `HybridCPU_ISE/Core/Pipeline/MicroOps/MicroOp.cs`.
- `MicroOp.SetClassFlexiblePlacement(SlotClass requiredSlotClass)` exists in the
  same file.
- `SlotClass.SystemSingleton` and `SlotClass.BranchControl` are both lane7 masks
  in `HybridCPU_ISE/Core/Pipeline/Scheduling/SlotClassDefinitions.cs`.
- The compiler-visible masks in
  `HybridCPU_Compiler/Core/IR/Model/IrHazardEnums.cs` map `System` and
  `Control` to `Slot7`; `DmaStream` maps to `Slot6`.
- `HybridCPU_Compiler/Core/IR/Hazards/HybridCpuSlotModel.Analysis.cs` maps
  `IrResourceClass.System` to the system slot mask and
  `IrResourceClass.DmaStream` to the lane6 DMA/stream mask.

Legacy custom accelerator quarantine:

- `CustomAcceleratorMicroOp` exists in
  `HybridCPU_ISE/Core/Pipeline/MicroOps/MicroOp.Misc.cs`.
- `CustomAcceleratorMicroOp.Execute(ref CPU_Core core)` throws through
  `InstructionRegistry.CreateUnsupportedCustomAcceleratorException(...)`.
- `CustomAcceleratorMicroOp.InitializeMetadata(...)` and
  `CustomAcceleratorMicroOp.EmitWriteBackRetireRecords(...)` exist, but the
  writeback/retire records remain unreachable through successful legacy
  execution.
- `InstructionRegistry.RegisterAccelerator(...)`,
  `InstructionRegistry.GetAccelerator(...)`,
  `InstructionRegistry.GetAllAccelerators()`, and
  `InstructionRegistry.CreateUnsupportedCustomAcceleratorException(...)` exist
  in `InstructionRegistry.Accelerators.cs`.
- `ICustomAccelerator` exists in `InstructionRegistry.Types.cs`.
- `AcceleratorRuntimeFailClosed.cs` exists and preserves unsupported
  custom-accelerator DMA seams.

MatMul fixture:

- `MatMulAccelerator` implements `ICustomAccelerator`.
- The real legacy fixture methods are:
  - `MatMulAccelerator.Execute(uint opcode, ulong[] operands, byte[] config)`
  - `MatMulAccelerator.GetLatency(uint opcode, ulong[] operands)`
  - `MatMulAccelerator.GetResourceFootprint(uint opcode)`
  - `MatMulAccelerator.IsPipelined(uint opcode)`
  - `MatMulAccelerator.Reset()`
- Phase docs were corrected from stale `EstimateLatency` /
  `GetResourceRequirements` wording to the live `GetLatency` /
  `GetResourceFootprint` names.

DmaStreamCompute:

- `DmaStreamComputeMicroOp` exists and uses
  `SetClassFlexiblePlacement(SlotClass.DmaStreamClass)`, preserving lane6
  class placement.
- `DmaStreamComputeMicroOp.Execute(...)` remains intentionally disabled and
  fail-closed.
- `DmaStreamComputeDescriptorParser.ExecutionEnabled` is `false`.
- `DmaStreamComputeDescriptorParser.Parse(...)` and
  `DmaStreamComputeDescriptorParser.TryDecodeRawVliwCarrier(...)` exist.
- `DmaStreamComputeToken.Commit(...)` is the live commit method; phase docs were
  corrected from stale `TryCommit` wording.
- `DmaStreamComputeToken.TryAdmit(...)`,
  `StageDestinationWrite(...)`, `MarkComputeComplete(...)`, `Cancel(...)`, and
  `PublishFault(...)` exist and remain the nearest model for token admission,
  staging, commit, cancel, and fault evidence.

L7-SDC implementation surfaces:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Auth` exists and contains
  owner/domain, mapping epoch, IOMMU-domain epoch, and guard validation
  surfaces.
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Capabilities` exists and
  contains metadata-only capability registry/provider surfaces.
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Descriptors` exists and
  contains typed descriptor ABI and carrier-validation surfaces.
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens` exists and
  contains token lifecycle, handle/status ABI, observation/control policies, and
  fault publication surfaces.
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Backends`,
  `Queues`, and `Memory` exist and model null/fake backends, queue admission,
  guarded source reads, staging, and direct-write violation evidence.
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Commit` exists and models
  staged-write commit, rollback, all-or-none publication, and SRF/cache
  invalidation evidence.
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Fences` exists and models
  Phase 09 scoped fence behavior plus bounded submit/poll lane7 pressure
  evidence.
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Conflicts` exists and
  models Phase 10 active-footprint reservation, execution-time overlap
  notification, commit-time validation, and guarded footprint release.
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Telemetry` exists and
  models Phase 13 reject, lifecycle, byte, conflict, latency, operation, and
  evidence-record counters. `AcceleratorTelemetrySnapshot` is immutable
  observation data, not an authority token.
- Phase 11 MatMul provider/schema/backend surfaces exist under
  `Capabilities`, `Descriptors`, and `Backends` and remain metadata-only,
  descriptor-backed, and staging-only.
- `HybridCPU_ISE/Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs` exists.
  L7-SDC command carriers are hard-pinned with
  `SetHardPinnedPlacement(SlotClass.SystemSingleton, 7)` and direct `Execute`
  remains fail-closed.
- There is no separate `AcceleratorCommandMicroOps.cs`; references to that file
  are stale unless a future refactor intentionally splits the carrier classes.

Compiler transport:

- `HybridCPU_Compiler/Core/IR/Model/IrAcceleratorModels.cs` exists and contains
  `IrAcceleratorIntent`, `IrAcceleratorCommand`,
  `IrAcceleratorDescriptorSideband`, `CompilerAcceleratorCapabilityModel`,
  `CompilerAcceleratorLoweringDecision`, and `AcceleratorLoweringMode`.
- `HybridCpuThreadCompilerContext.CompileAcceleratorSubmit(...)` exists and
  lowers explicit accelerator intent only after compiler-side capability
  strategy accepts it.
- `InstructionIR`, `InstructionSlotMetadata`, and
  `HybridCPU_Compiler/Core/IR/Model/IrSlotMetadata.cs` carry L7-SDC typed
  sideband separately from DmaStreamCompute sideband.
- The ISE decoder/projector path preserves typed
  `AcceleratorCommandDescriptor` sideband while keeping raw reserved bits, raw
  VT hints, and raw `Src2` zero.
- Regular stream intent remains lane6 `DmaStreamCompute`; runtime rejection
  after emitted `ACCEL_SUBMIT` remains rejection, not fallback.

Diagnostics and telemetry:

- `HybridCPU_ISE/Core/Diagnostics/TypedSlotTelemetryProfile.cs` exports optional
  `AcceleratorTelemetry` separately from existing DmaStreamCompute telemetry.
- `HybridCPU_ISE/Core/Diagnostics/TelemetryExporter.cs` exposes L7-SDC
  telemetry snapshots through additive compatible hooks.
- Telemetry/profile snapshots are evidence only and cannot authorize descriptor,
  capability, submit, backend, commit, cancellation, fence, fault, or exception
  publication decisions.

Diagnostics harness:

- `TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj` exists.
- `Program.cs` supports `matrix-smoke`, `matrix-runtime`, `matrix-memory`,
  `matrix-full`, `--iterations`, and `--telemetry-logs`.
- `Documentation/AsmAppTestResults.md` exists and is the current comparison
  target required by all phase closure gates.

## Corrected stale references

The audit patched these plan/spec references:

- `Documentation/StreamEngine and DmaStreamCompute` ->
  `Documentation/Stream WhiteBook/StreamEngine DmaStreamCompute`
- `DmaStreamComputeToken.TryCommit` -> `DmaStreamComputeToken.Commit`
- `MatMulAccelerator.EstimateLatency` -> `MatMulAccelerator.GetLatency`
- `MatMulAccelerator.GetResourceRequirements` ->
  `MatMulAccelerator.GetResourceFootprint`

## Remaining planned surfaces

No Phase 12-14 compiler, telemetry, or documentation quarantine implementation
surfaces are intentionally open. Phase 15 is validation and rollback closure,
not an authority expansion.

The existing L7-SDC surfaces must remain fail-closed where direct micro-op
execution or authority-sensitive publication would otherwise bypass guard,
descriptor, token, backend, or commit-plane checks.

## Per-phase audit result

| Phase | Code-alignment result |
| --- | --- |
| 00 | Closed. Existing audit surfaces and current documentation paths match live code. |
| 01 | Closed. Legacy custom, registry, and MatMul fixture method names match live code and remain quarantined. |
| 02 | Closed. L7-SDC capability registry is metadata-only and cannot authorize decode, submit, backend execution, or commit. |
| 03 | Closed. Opcode/classifier/scheduler/compiler slot surfaces and lane7 fail-closed carriers exist. |
| 04 | Closed. L7-SDC descriptor ABI parser and native carrier validation exist. |
| 05 | Closed. L7-SDC owner/domain plus mapping/IOMMU epoch guard surfaces exist. |
| 06 | Closed. L7-SDC token lifecycle, handle ABI, status ABI, and register ABI exist. |
| 07 | Closed. Null/fake backend, queue model, guarded memory portal, and direct-write violation evidence exist. |
| 08 | Closed. Staged-write commit coordinator, rollback, exact coverage, all-or-none publication, and SRF/cache invalidation evidence exist. |
| 09 | Closed. Poll/wait/cancel/fence, fault publication, and lane7 pressure evidence exist without commit-plane bypass. |
| 10 | Closed. `ExternalAcceleratorConflictManager` owns active token footprint truth and enforces v1 overlap serialize/reject/fault/drain decisions without commit authority. |
| 11 | Closed. MatMul exists as metadata-only capability provider, conservative descriptor schema/resource model, and staging-only fake backend; legacy execute remains quarantined. |
| 12 | Closed. Explicit compiler accelerator intent, typed sideband transport, lane7 `ACCEL_SUBMIT`, and no-runtime-fallback tests exist. |
| 13 | Closed. L7-SDC telemetry/evidence counters, immutable snapshots, and additive diagnostics export exist; DmaStreamCompute telemetry remains separate. |
| 14 | Closed. Current L7-SDC, DmaStreamCompute, stream/assist, and implementation-boundary documentation matches live tree and claim-safety tests guard against affirmative unsafe claims. |
| 15 | Test and diagnostics project references exist. |

## Remaining implementation cautions

- Do not place L7-SDC implementation under the legacy custom registry.
- Do not reuse `CustomAcceleratorMicroOp` as the new carrier.
- Do not call `ICustomAccelerator.Execute()` from production L7-SDC backends.
- Do not weaken `DmaStreamComputeMicroOp` fail-closed direct execution while
  adding external accelerator code.
- Do not treat the current compiler `System = Slot7` mask as enough for L7-SDC;
  runtime micro-ops still need hard-pinned lane7 placement.
- Do not add L7-SDC descriptor data to raw reserved VLIW fields; extend typed
  sideband transport.
- Do not publish future `ExternalAccelerators/*` authority, backend, or compiler
  behavior as implemented until its closure tests and `TestAssemblerConsoleApps`
  regression comparison pass.
