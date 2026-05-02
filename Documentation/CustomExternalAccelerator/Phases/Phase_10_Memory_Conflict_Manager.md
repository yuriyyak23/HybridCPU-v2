# Phase 10 - Memory conflict manager

Status: closed on 2026-04-28.

Goal:

- Add `ExternalAcceleratorConflictManager` as the owner of L7-SDC active
  footprint truth.
- Enforce v1 serialize-or-reject policy for CPU, DmaStreamCompute, SRF, assist,
  and accelerator-token overlaps.

Current prerequisites:

- Phases 00-09 are closed.
- Staged writes are the only L7-SDC architectural publication path.
- Device completion is not commit.
- Poll, wait, cancel, fence, and fault publication revalidate owner/domain and
  mapping/IOMMU authority.
- Phase 10 `ExternalAcceleratorConflictManager` owns active token footprint
  truth for L7-SDC overlap decisions.
- No runtime fallback to DmaStreamCompute, StreamEngine, VectorALU,
  GenericMicroOp, scalar/ALU/vector, or legacy custom accelerator may be added.

ISE files likely touched:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Conflicts/*`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/*`
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/*`
- `HybridCPU_ISE/Core/Execution/StreamEngine/*`
- `HybridCPU_ISE/Memory/Registers/StreamRegisterFile*.cs`
- `HybridCPU_ISE/Core/Pipeline/Core/CPU_Core.PipelineExecution.Memory.cs`
- `HybridCPU_ISE/Core/Pipeline/Assist/AssistMicroOp.cs`
- cache model files if present

Compiler files likely touched:

- hazard model only if compile-time conflict annotations are emitted

New ISE types:

- `ExternalAcceleratorConflictManager`
- `AcceleratorActiveFootprintTable`
- `AcceleratorFootprintReservation`
- `AcceleratorConflictClass`
- `AcceleratorConflictDecision`
- `AcceleratorConflictFault`

Methods to design:

- `ExternalAcceleratorConflictManager.TryReserveOnSubmit(...)`
- `ExternalAcceleratorConflictManager.NotifyCpuLoad(...)`
- `ExternalAcceleratorConflictManager.NotifyCpuStore(...)`
- `ExternalAcceleratorConflictManager.NotifyDmaStreamComputeAdmission(...)`
- `ExternalAcceleratorConflictManager.NotifySrfWarmWindow(...)`
- `ExternalAcceleratorConflictManager.NotifyAssistIngressWindow(...)`
- `ExternalAcceleratorConflictManager.ValidateBeforeCommit(...)`
- `ExternalAcceleratorConflictManager.ReleaseTokenFootprint(...)`
- `ExternalAcceleratorConflictManager.InvalidateSrfCacheOnCommit(...)`

Conflict stages:

- submit-time footprint reservation
- execution-time conflict monitoring
- commit-time final validation

v1 conflict classes:

- CPU store overlaps accelerator read/write
- CPU load overlaps accelerator write
- DmaStreamCompute overlaps accelerator write
- accelerator write overlaps SRF warmed window
- assist/SRF warm overlaps accelerator write
- two accelerator tokens write same region
- fence/serializing boundary while token active
- VM/domain/mapping transition while token active

Tests to add:

- `L7SdcConflictManagerTests`
- `L7SdcDmaStreamComputeConflictTests`
- `L7SdcSrfAssistConflictTests`

Test cases:

- submit-time overlap rejects
- CPU store overlap serializes or rejects
- CPU load overlap with accelerator write serializes or rejects
- DmaStreamCompute write overlap rejects one side
- SRF warm overlap invalidates or rejects per phase policy
- assist warm overlap rejects as authority
- two accelerator writers same region reject
- commit-time drift faults
- conflict success/fence success is not authority for commit without Phase 08
  coordinator preconditions
- conflict evidence cannot bypass owner/domain, mapping/IOMMU epoch,
  descriptor identity, footprint identity, exact staged coverage, or
  direct-write violation checks

Must not break:

- DmaStreamCompute canonical lane6 path
- SRF warming/prefetch assist semantics
- StreamEngine helper tests
- baseline memory ordering behavior

Phase closure validation:

- Run focused tests:

```powershell
dotnet test --filter "L7SdcConflictManager|L7SdcDmaStreamComputeConflict|L7SdcSrfAssistConflict"
```

- Run affected baseline filters:

```powershell
dotnet test --filter "DmaStreamCompute|StreamRegisterFile|AssistRuntime|Phase09"
```

- Run diagnostics console:

```powershell
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

- Compare with `Documentation/AsmAppTestResults.md`; memory stalls, legality
  rejects, and DmaStreamCompute counters must be reviewed for regressions.

Definition of done:

- conflict manager owns active token footprint truth
- every v1 conflict class serializes or rejects

Rollback rule:

- conservatively reject overlapping accelerator submits if conflict monitoring
  is incomplete
