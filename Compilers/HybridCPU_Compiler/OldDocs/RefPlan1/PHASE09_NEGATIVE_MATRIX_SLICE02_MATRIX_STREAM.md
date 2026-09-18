# Phase 09 Negative Matrix Slice 02 - MatrixTile and Stream/Vector

Status: implemented as the second Phase 09 negative-matrix slice before caller migration.

## Implemented source

- `HybridCPU_ISE.Tests/CompilerTests/CompilerPhase09MatrixTileNegativeMatrixTests.cs`
- `HybridCPU_ISE.Tests/CompilerTests/CompilerPhase09StreamVectorNegativeMatrixTests.cs`

No production lowering was added. No public callers were migrated in this slice.

## MatrixTile coverage

The MatrixTile negative matrix now asserts:

- only the current four helper rows are MatrixTile-positive:
  - `MTILE_LOAD`
  - `MTILE_STORE`
  - `MTILE_MACC`
  - `MTRANSPOSE`
- vector aliases, dot-product aliases, lane6 DSC, lane7 accelerator submit, and Stream/vector opcodes are not MatrixTile-positive;
- helper rows keep `UsesFallbackPath == false`;
- helper rows keep `UsesAliasPromotion == false`;
- compiler cannot override runtime-owned MatrixTile legality;
- malformed shape rejects before emission and keeps `InstructionCount == 0`;
- unsupported dtype/numeric policy rejects before emission and keeps `InstructionCount == 0`;
- unsupported layout policy rejects before emission and keeps `InstructionCount == 0`;
- unsupported accumulator policy rejects before emission and keeps `InstructionCount == 0`;
- `MatrixTileHelperOnly` provider shell rejects lowering and does not allow scalar/vector/Stream fallback.

This protects the Phase 09 rule:

```text
MatrixTile unsupported op/dtype/shape/layout/accumulator rejects with no fallback.
```

## Stream/vector coverage

The Stream/vector negative matrix now asserts:

- only `VLOAD` and `VSTORE` are positive vector-transfer helper rows;
- dot-product, transpose, MatrixTile, lane6 DSC, and lane7 accelerator submit opcodes are not vector-transfer positive helper rows;
- helper rows keep `UsesFallbackPath == false`;
- helper rows keep `UsesAliasPromotion == false`;
- compiler cannot override runtime-owned vector-transfer legality;
- zero-length shape rejects before emission and keeps `InstructionCount == 0`;
- stride smaller than element size rejects before emission and keeps `InstructionCount == 0`;
- unknown dtype rejects before emission and keeps `InstructionCount == 0`;
- recovered malformed carriers with `StreamLength == 0`, `Stride == 0`, indexed addressing, or 2D addressing fail closed in IR construction;
- `StreamEngineVector` provider shell rejects lowering and does not allow scalar fallback.

This protects the Phase 09 rule:

```text
Stream/vector unsupported shape/dtype/stride rejects with no scalar fallback.
```

Current observation: predicate masks are a byte-valued part of the existing typed vector-transfer shape ABI. This slice does not invent a new unsupported predicate rule. Predicate-specific policy should remain an explicit future gate if the architecture narrows the supported mask space.

## Validation

MatrixTile targeted gate:

```text
dotnet test C:\Users\Yuriy Kurnosov\Desktop\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj --filter FullyQualifiedName~CompilerPhase09MatrixTileNegativeMatrixTests --no-restore
```

Result: passed, 4 tests.

Stream/vector targeted gate:

```text
dotnet test C:\Users\Yuriy Kurnosov\Desktop\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj --filter FullyQualifiedName~CompilerPhase09StreamVectorNegativeMatrixTests --no-restore
```

Result: passed, 7 tests.

Combined compiler/runtime Phase 09 gate:

```text
dotnet test C:\Users\Yuriy Kurnosov\Desktop\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj --filter "FullyQualifiedName~CompilerCoreAuthorityBoundaryNegativeTests|FullyQualifiedName~CompilerPhase09NegativeMatrixTests|FullyQualifiedName~CompilerPhase09MatrixTileNegativeMatrixTests|FullyQualifiedName~CompilerPhase09StreamVectorNegativeMatrixTests|FullyQualifiedName~CompilerNoEmissionBoundaryTests|FullyQualifiedName~CompilerContractHandshakeTests|FullyQualifiedName~L7SdcNativeCarrierValidationTests|FullyQualifiedName~CompilerMatrixTilePositiveEmissionTests|FullyQualifiedName~CompilerVectorTransferPositiveEmissionTests" --no-restore
```

Result: passed, 118 tests.

## Historical Next Task

The DSC lane6 negative matrix listed here has since been implemented. For the
current open compiler work, use:

- `PHASE09_REMAINING_COMPILER_WORK.md`
- `CONTINUATION_PROMPT_PHASE09.md`
