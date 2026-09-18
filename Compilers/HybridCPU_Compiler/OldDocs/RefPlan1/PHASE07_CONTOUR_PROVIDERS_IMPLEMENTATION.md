# PHASE07_CONTOUR_PROVIDERS_IMPLEMENTATION

Status: initial implementation slice, 2026-07-08.

Implemented source:

```text
HybridCPU_Compiler/Core/IR/Contours/ContourProviderContracts.cs
```

Implemented concepts:

```text
CompilerCapabilityObservationState
CompilerTargetProfile
CompilerLoweringContext
CompilerCapabilityObservation
ContourAnalysisReport
IContourAnalyzer
IContourLoweringProvider
IContourLoweringProviderRegistry
DefaultContourLoweringProviderRegistry
```

Provider shells:

```text
ScalarVliwLoweringProvider
LoadStoreVliwLoweringProvider
BranchControlVliwLoweringProvider
StreamVectorLoweringProvider
MatrixTileLoweringProvider
DmaStreamComputeLoweringProvider
L7SdcLoweringProvider
VmxProjectionLoweringProvider
SecureComputeAdmissionLoweringProvider
```

## Slice Boundary

This is a contract and provider-shell slice only.

It does not:

```text
migrate legacy lowering callers
perform production backend lowering
route fallback between contours
turn capability observations into authority
claim runtime legality
claim execution/publication/commit/retire
```

## Analyzer / Provider Split

`IContourAnalyzer` produces `ContourAnalysisReport`.

`IContourLoweringProvider` consumes a report and returns
`CompilerLoweringDecision`.

Analysis reports are evidence only. They do not emit:

```text
carrier
sideband
descriptor
typed-slot facts
runtime bridge acceptance
```

Provider shells currently reject production lowering with typed decisions. This
keeps the migration in observe/wrap mode until Phase 07 caller migration is
explicitly started.

## Registry Semantics

`DefaultContourLoweringProviderRegistry` is a responsibility router, not a
fallback router.

Unknown contours resolve to:

```text
RejectedContourAnalyzer
RejectedContourLoweringProvider
ExecutionContourKind.UnknownRejected
CompilerRejectReason.UnknownContour
FallbackPolicy.Forbidden
```

Cross-contour analysis passed to a provider returns:

```text
CompilerRejectReason.CrossContourFallbackForbidden
```

No scalar fallback is attempted.

## Capability Observations

Current provider observations use safe states only:

```text
NoEmission
HelperOnly
ScopedRuntimeContour
```

VMX and SecureCompute providers observe:

```text
CompilerCapabilityObservationState.NoEmission
CompilerRuntimeAuthorityDependency.NoRuntimeActionBecauseNoEmission
```

MatrixTile and Stream/vector providers observe helper-only states and do not
claim production lowering.

## Negative Gates

Covered by:

```text
HybridCPU_ISE.Tests/CompilerTests/CompilerCoreAuthorityBoundaryNegativeTests.cs
```

The tests assert:

```text
unknown contour fails closed without scalar fallback
analyzer and provider are separate
analysis evidence has NoExecutionClaim
provider shell does not perform production lowering
cross-contour analysis rejects instead of falling back
VMX and SecureCompute observe no-emission only
```

## Historical Next Phase

At the time of this slice the next phase was Phase 08. That phase has since
been implemented. For the current open work, use:

- `PHASE09_REMAINING_COMPILER_WORK.md`
- `CONTINUATION_PROMPT_PHASE09.md`
