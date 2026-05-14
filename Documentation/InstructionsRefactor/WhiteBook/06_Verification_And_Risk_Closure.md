# Verification And Risk Closure

Updated: 2026-05-14.

## Verification Snapshot

Latest documented slices:

| Slice | Result |
|---|---:|
| Focused runtime baseline | Passed 482, Failed 0 |
| Combined runtime baseline | Passed 511, Failed 0 |
| Phase 08 atomic semantics | Passed 26, Failed 0 |
| Phase 08 load/store semantics | Passed 36, Failed 0 |
| Published Phase 09 replay/rollback and SMT/VT ownership slice | Passed 41, Failed 0 |
| Touched replay/rollback broad filter | Passed 79, Failed 0 |
| Lane6 DSC / DmaStream / Lane7 pinning published slice | Passed 186, Failed 0 |
| Lane6/Lane7 touched pinning support filter | Passed 168, Failed 0 |
| Lane7 L7-SDC published contract slice | Passed 303, Failed 0 |
| Lane7 L7-SDC touched support filter | Passed 299, Failed 0 |
| Compiler scalar-tail inventory slice | Passed 19, Failed 0 |
| Runtime scalar closure through `ZEXT.W` | Passed 148, Failed 0 |
| Phase 10 atomic ordering plus `FENCE` / `FENCE_I` | Passed 49, Failed 0 |
| Retire contract closure touched filter | Passed 197, Failed 0 |

These results describe the currently documented closure state. They should be
rerun when the related contour is touched.

## Standard Runtime Gates

Before changing an ISE contour:

1. Start with `git status --short`.
2. Re-read `00_README.md`, the audit and the phase file for the contour.
3. Confirm live code and focused tests, not docs alone.
4. Keep edits scoped to the runtime-owned contour.
5. Preserve opcode values and VLIW instruction/bundle encoding.
6. Keep compiler lowering closed unless explicitly scoped.

After changing a runtime contour:

1. Run focused tests for the touched contour.
2. Run the focused runtime baseline.
3. Run the combined runtime baseline when the contour closes.
4. Run adjacent Phase 08/09/10 slices if retire, replay, lane or memory behavior
   was touched.
5. Run `TestAssemblerConsoleApps` and compare with the source comparison log.

## Focused Commands From Current Closure

Phase 10 focused atomic/fence command:

```powershell
dotnet test .\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj --no-restore --filter "FullyQualifiedName~Phase10AtomicAcquireReleaseOrderingTests|FullyQualifiedName~Phase10FenceOrderingAndVisibilityTests"
```

Focused runtime baseline:

```powershell
dotnet test .\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj --no-restore --filter "FullyQualifiedName~InstructionsRefactor|FullyQualifiedName~IsaV4SurfaceTests"
```

Documentation/conformance cleanup checks may use the documentation-focused test
slice when documentation changes:

```powershell
dotnet test .\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj --no-restore --filter "FullyQualifiedName~Documentation"
```

## Assembler-App Risk Closure

Every completed task must close risk with:

```powershell
dotnet run --project .\TestAssemblerConsoleApps\TestAssemblerConsoleApps.csproj
```

Then compare the live result with:

```text
C:\Users\Yuriy Kurnosov\Desktop\HybridCPU ISE\Documentation\AsmAppTestResults.md
```

The comparison log records an older successful profile:

```text
200 iterations
10 child runs
no stream-vector
```

The current live matrix is expected to use:

```text
250 iterations
11 child runs
stream-vector included
```

This drift is acceptable only if:

- process exit code is `0`;
- aggregate status is `Succeeded`;
- all child runs are green;
- `stream-vector` is green;
- focused runtime semantics remain green.

Do not silently rewrite `AsmAppTestResults.md`.

Latest documented risk-closure artifact:

```text
C:\Users\Yuriy Kurnosov\Desktop\HybridCPU ISE\TestResults\TestAssemblerConsoleApps\20260513_222020_759_matrix
```

## Evidence Discipline

A test result closes the claim that it actually covers. It does not authorize
adjacent contours. Examples:

- Phase 10 `FENCE_I` tests do not open `ICACHE_INVAL`;
- atomic aq/rl tests do not open compiler atomic lowering;
- Lane6 descriptor tests do not open production DMA execution;
- Lane7 carrier tests do not open device backend execution;
- scalar compiler tail tests do not open broad vector/matrix/cache lowering.

## Handoff Checklist For Future Work

When opening a future contour, document all of these before coding:

- selected instruction/form;
- current support status;
- explicit runtime model;
- accepted ABI payload;
- rejected ABI payloads;
- decoder projection rules;
- MicroOp materialization rules;
- execution/capture behavior;
- retire publication behavior;
- rollback/replay behavior;
- lane/resource ownership;
- focused positive tests;
- focused negative tests;
- compiler emission status;
- assembler-app risk closure result.

