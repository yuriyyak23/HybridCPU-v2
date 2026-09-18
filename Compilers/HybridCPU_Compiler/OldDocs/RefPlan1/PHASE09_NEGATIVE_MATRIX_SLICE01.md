# Phase 09 Negative Matrix Slice 01

Status: implemented as the first Phase 09 negative-matrix slice after evidence/telemetry snapshots.

## Implemented source

- `HybridCPU_ISE.Tests/CompilerTests/CompilerPhase09NegativeMatrixTests.cs`
- `HybridCPU_Compiler/Core/IR/Artifacts/CompilerEmissionPackage.cs`

## Scope

This slice expands the authority-boundary matrix before caller migration or legacy cleanup. It does not add production backend lowering and does not migrate public compiler entrypoints.

The slice covers:

- descriptor ABI validity without payload;
- runtime bridge accepted status preserving runtime Stage A/B, commit, retire, and publication requirements;
- missing typed-slot facts in compatibility mode remaining weaker than validated facts;
- unknown/future-gated contour rejection with no scalar fallback;
- cross-contour provider analysis rejection with no fallback;
- production lowering remaining rejected for provider-shell paths.

## Minimal behavior guard

`DescriptorEnvelopeValidator` now fails closed when `DescriptorAbiStatus.ValidTransportDescriptor` is reported without descriptor payload.

This preserves the Phase 05/09 rule:

```text
descriptor parser/ABI success is descriptor evidence only and cannot imply execution authority.
```

The validator still returns structural validation evidence only. It does not grant execution, publication, commit, retire, or runtime legality authority.

## Negative gates added

- `DescriptorValidator_RejectsValidTransportDescriptorWithoutPayload`
- `RuntimeBridgeAcceptedReport_StillRequiresRuntimeLegalityAndPublicationStages`
- `TypedSlotFactsBridge_MissingCompatibilityIsWeakerThanValidatedFacts`
- `ProviderRegistry_UnknownContourFailsClosedWithoutScalarFallback`
- `ProviderRejectsCrossContourAnalysisWithoutFallback`

## Validation

Phase 09 targeted gate:

```text
dotnet test C:\Users\Yuriy Kurnosov\Desktop\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj --filter FullyQualifiedName~CompilerPhase09NegativeMatrixTests --no-restore
```

Result: passed, 5 tests.

Combined compiler/runtime gate:

```text
dotnet test C:\Users\Yuriy Kurnosov\Desktop\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj --filter "FullyQualifiedName~CompilerCoreAuthorityBoundaryNegativeTests|FullyQualifiedName~CompilerPhase09NegativeMatrixTests|FullyQualifiedName~CompilerNoEmissionBoundaryTests|FullyQualifiedName~CompilerContractHandshakeTests|FullyQualifiedName~L7SdcNativeCarrierValidationTests" --no-restore
```

Result: passed, 91 tests.

## Historical Next Task

The domain-specific negative matrix expansion listed here has since been
implemented across later Phase 09 slices. For the current open compiler work,
use:

- `PHASE09_REMAINING_COMPILER_WORK.md`
- `CONTINUATION_PROMPT_PHASE09.md`
