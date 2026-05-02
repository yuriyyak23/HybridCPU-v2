# Phase 14 - Documentation quarantine and claim safety

Status:

- Closed on 2026-04-28.
- Documentation quarantine updated final L7-SDC status and claim boundaries
  through Phase 13 without changing runtime behavior.
- Latest diagnostics artifact:
  `TestResults/TestAssemblerConsoleApps/20260428_195656_422_matrix-smoke`.

Goal:

- Align all public documentation with implemented L7-SDC authority,
  placement, descriptor, token, memory, and rollback boundaries.

Documentation files likely touched:

- `Documentation/CustomExternalAccelerator/*`
- `Documentation/CustomExternalAccelerator/Phases/*`
- `Documentation/Stream WhiteBook/DmaStreamCompute/*`
- `Documentation/Stream WhiteBook/StreamEngine DmaStreamCompute/*`
- `HybridCPU_ISE/docs/assist-semantics.md`
- relevant README/research notes only if they make active execution claims

ISE files likely touched:

- comments only where runtime comments overclaim capability or execution

Compiler files likely touched:

- comments only where compiler docs overclaim runtime fallback or capability
  authority

Claim boundaries to enforce:

- L7-SDC is lane7 `SystemSingleton`, hard-pinned to lane7
- L7-SDC uses typed sideband descriptors
- L7-SDC raw carrier must be clean
- L7-SDC owner/domain/mapping guard is authority
- token handle is lookup key, not authority
- telemetry/replay/certificate/registry data is evidence only
- external device completion is not commit
- staged write token commit is architectural visibility
- `DmaStreamCompute` remains lane6 CPU-native stream compute
- StreamEngine/SRF/VectorALU are helper/test backend internals only when
  explicitly wrapped
- VDSA assist remains non-retiring, replay-discardable, non-compute
- legacy custom accelerator execution is not active architecture

Forbidden documentation claims:

- legacy custom accelerator is active compute
- MatMul fixture publishes memory
- registry success grants execution
- BranchControl authorizes external accelerator commands
- lane6 DmaStreamClass carries external accelerator commands
- runtime can silently fallback after accelerator rejection
- direct device write is architectural commit
- token identity alone can commit
- telemetry is authority

Tests to add:

- `L7SdcDocumentationClaimSafetyTests`
- extend existing Phase09/Phase12 documentation claim-safety tests

Test cases:

- grep/parse docs for forbidden claims
- assert phase docs mention TestAssemblerConsoleApps closure gate
- assert L7-SDC docs mention hard-pinned `SystemSingleton`
- assert docs mention no production call into `ICustomAccelerator.Execute()`
- assert docs mention mapping epoch for detach/suspend

Must not break:

- existing claim-safety tests
- historical idea files may remain historical input, but final specs must not
  adopt unsafe claims

Phase closure validation:

- Run focused tests:

```powershell
dotnet test --filter "L7SdcDocumentationClaimSafety|Phase09ClaimSafety|Phase12"
```

- Run affected baseline filters:

```powershell
dotnet test --filter "Phase09|Phase12|Phase4Extensibility"
```

- Run diagnostics console:

```powershell
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

- Compare with `Documentation/AsmAppTestResults.md`; documentation-only edits
  should not change diagnostics.

Definition of done:

- final docs match implemented boundaries
- claim-safety tests fail on overclaim

Rollback rule:

- revert unsafe documentation claims only
- do not modify runtime fail-closed behavior to satisfy docs

## Validation closure evidence - 2026-04-28

Focused gate:

```powershell
dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --filter "L7SdcDocumentationClaimSafety|Phase09ClaimSafety|Phase12" --no-restore
```

Result: passed, `228/228`, `0` skipped.

Affected baseline:

```powershell
dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --filter "Phase09|Phase12|Phase4Extensibility" --no-restore
```

Result: passed, `1321/1321`, `0` skipped.

Diagnostics:

```powershell
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

Result: succeeded, `3` child runs, artifacts under
`TestResults/TestAssemblerConsoleApps/20260428_195656_422_matrix-smoke`.

Diagnostics shape note:

- `matrix-smoke` still emits `safety`, `replay-reuse`, and `assistant`.
- Phase 14 changed documentation and claim-safety tests only; it did not add
  diagnostics fields or runtime profile semantics.
- `Documentation/AsmAppTestResults.md` was not updated for this phase.
