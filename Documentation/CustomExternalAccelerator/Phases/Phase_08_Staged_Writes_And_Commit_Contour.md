# Phase 08 - Staged writes and commit contour

Status: closed.

Goal:

- Implement the commit-plane rule that staged writes are the only path to
  architectural memory visibility.
- Make direct device writes, partial writes, and owner/domain/mapping drift fail
  closed.

ISE files likely touched:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Commit/*`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Memory/*`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/*`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Backends/*`
- `HybridCPU_ISE/Memory/Registers/StreamRegisterFile*.cs`
- cache model files if present
- CPU memory write helpers used by DmaStreamCompute staged commit

Compiler files likely touched:

- none required

New ISE types:

- `AcceleratorStagedWrite`
- `AcceleratorStagedWriteSet`
- `AcceleratorCommitCoordinator`
- `AcceleratorCommitResult`
- `AcceleratorCommitFault`
- `AcceleratorRollbackRecord`
- `AcceleratorCommitInvalidationPlan`

Methods to design:

- `AcceleratorCommitCoordinator.TryCommit(...)`
- `AcceleratorCommitCoordinator.ValidateCommitPreconditions(...)`
- `AcceleratorCommitCoordinator.ValidateExactCoverage(...)`
- `AcceleratorCommitCoordinator.ValidateOwnerDomainAndMapping(...)`
- `AcceleratorCommitCoordinator.ApplyAllOrNone(...)`
- `AcceleratorCommitCoordinator.Rollback(...)`
- `AcceleratorCommitCoordinator.InvalidateSrfAndCache(...)`
- `AcceleratorStagedWriteSet.CoversExactly(...)`
- `AcceleratorStagedWriteSet.NormalizeDestinationFootprint(...)`

Commit preconditions:

- token state is `CommitPending`
- owner/domain guard revalidates
- mapping epoch revalidates
- descriptor identity hash matches token binding
- normalized footprint hash matches token binding
- staged writes exactly cover destination footprint
- v1 conflict manager allows final commit
- backend has not performed direct architectural write

Commit behavior:

- apply all staged writes atomically for v1 all-or-none policy
- rollback or fault on partial failure
- invalidate overlapping SRF windows
- update or invalidate cache model
- record bytes committed and rollback telemetry
- transition token to `Committed` or `Faulted`

Tests to add:

- `L7SdcCommitTests`
- `L7SdcRollbackTests`
- `L7SdcSrfCacheInvalidationTests`

Test cases:

- staged writes invisible before commit
- device completion not visible before commit
- exact staged coverage commits
- missing staged byte faults
- partial memory write rolls back or faults
- owner drift blocks commit
- mapping epoch drift blocks commit
- SRF overlap invalidated on commit
- cache overlap invalidated or updated on commit
- direct write violation never counts as commit

Must not break:

- DmaStreamCompute staged token commit semantics
- StreamRegisterFile invalidation tests
- memory fault/rollback tests

Phase closure validation:

- Run focused tests:

```powershell
dotnet test --filter "L7SdcCommit|L7SdcRollback|L7SdcSrfCacheInvalidation"
```

- Run affected baseline filters:

```powershell
dotnet test --filter "DmaStreamComputeCommitToken|StreamRegisterFile|Phase09"
```

- Run diagnostics console:

```powershell
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

- Compare with `Documentation/AsmAppTestResults.md`; committed bytes and
  invalidation counters must only change in tests that use L7-SDC.

Definition of done:

- token commit is the only architectural publication point
- partial visibility cannot be hidden as success

Rollback rule:

- leave tokens at `CommitPending` and fault commit attempts if all-or-none
  correctness is uncertain
