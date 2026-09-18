# PHASE03_INTENT_CONTOURS_IMPLEMENTATION

Status: initial implementation slice, 2026-07-08.

Implemented source:

```text
HybridCPU_Compiler/Core/IR/Intent/CompilerSemanticIntent.cs
HybridCPU_Compiler/Core/IR/Contours/CompilerExecutionContourSelection.cs
```

Implemented types:

```text
SemanticIntentKind
CompilerSemanticIntent
ICompilerIntentClassifier
ExecutionContourKind
CompilerSidebandRequirement
CompilerExecutionContourSelection
IExecutionContourSelector
CompilerDefaultExecutionContourSelector
```

## Slice Boundary

This is an observe/wrap/preparation slice only.

It does not:

```text
emit carrier
emit sideband
emit descriptor
produce typed-slot facts
invoke a lowering provider
migrate existing lowering callers
grant runtime legality
grant execution/publication/commit/retire authority
```

## Current Selection Semantics

```text
Unknown -> UnknownRejected
ScalarAlu -> NativeVliwScalar
LoadStore -> NativeVliwLoadStore
BranchControl -> NativeVliwBranchControl
VectorStream -> StreamEngineVector
MatrixTile -> MatrixTileHelperOnly
DmaStreamCompute -> DmaStreamComputeLane6
ExternalAcceleratorCommand -> L7SdcLane7
VmxCompatibilityProjection -> VmxProjectionOnly
SecureComputeAdmission -> SecureComputePolicyAdmissionOnly
RuntimeAssist -> FutureGated
NonExecutable -> NoEmission
```

## Authority Rules

`CompilerSemanticIntent` has no contour, emission, carrier, descriptor, memory
publication, register publication, commit, or retire fields.

`CompilerExecutionContourSelection` is not a lowering decision. It records
contour selection diagnostics and runtime dependencies only.

All current contour selections set fallback to forbidden by default.

VMX and SecureCompute use no-emission contours:

```text
VmxProjectionOnly
SecureComputePolicyAdmissionOnly
```

DSC and L7 remain distinct descriptor contours:

```text
DmaStreamComputeLane6
L7SdcLane7
```

MatrixTile remains helper ABI only:

```text
MatrixTileHelperOnly
```

Unsupported or unknown input fails closed:

```text
UnknownRejected
```

## Negative Gates

Covered by:

```text
HybridCPU_ISE.Tests/CompilerTests/CompilerCoreAuthorityBoundaryNegativeTests.cs
```

The tests assert:

```text
intent and contour types are separate
no combined SemanticIntentClassification compiler source appears
unknown contour cannot fall back to scalar
MatrixTile cannot fall back to scalar/vector/Stream
DSC and L7 select distinct descriptor contours
VMX and SecureCompute select no-emission contours only
```
