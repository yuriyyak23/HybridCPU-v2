# PHASE04_LOWERING_DECISION_IMPLEMENTATION

Status: initial implementation slice, 2026-07-08.

Implemented source:

```text
HybridCPU_Compiler/Core/IR/Lowering/CompilerLoweringDecision.cs
```

Implemented concepts:

```text
CompilerLoweringDecision
CompilerLoweringDecisionKind
CompilerEmissionClass
CompilerProductionLoweringStatus
NoFallbackProof
FallbackPolicy
FallbackPolicyKind
LegacyApiTranslation
CompilerRejectReason
CompilerProducedArtifactKind
CompilerRequiredArtifactKind
```

## Slice Boundary

This is a typed decision vocabulary and guard slice only.

It does not:

```text
migrate public lowering entrypoints
grant production backend lowering
convert helper/parser/descriptor success into production lowering
convert carrier emission into publication/commit/retire
convert typed-slot facts or structural admission into runtime legality
route fallback through contour providers
```

## Decision Payload

`CompilerLoweringDecision` carries:

```text
decision kind
intent kind
contour kind
authority class
authority source kind
evidence class
execution claim
publication class
emission class
production lowering status
runtime authority dependency
no-fallback proof
fallback policy
produced artifacts
required artifacts
reject reasons
legacy translation, if present
reason
```

## Legacy Translation Guard

`LegacyApiTranslation.Create(...)` rejects any conversion where:

```text
StrengthensAuthority == true
```

The initial legacy structural adapter:

```text
CompilerLoweringDecision.FromLegacyStructuralBool(...)
```

sets:

```text
decision.kind = StructuralOnly
production_lowering.status = NotProductionLowering
emission.class = EvidenceOnly
authority.class = StructuralAdmissionEvidence
execution.claim = NoExecutionClaim
publication.class = EvidenceOnly
legacy_translation.StrengthensAuthority = false
runtime_dependency = RuntimeLegalityARequired | RuntimeLegalityBRequired | RuntimeExecutionRequired
fallback.policy = Forbidden
```

## Negative Gates

Covered by:

```text
HybridCPU_ISE.Tests/CompilerTests/CompilerCoreAuthorityBoundaryNegativeTests.cs
```

The tests assert:

```text
reject decisions carry typed authority and no-fallback proof
legacy bool translation cannot strengthen authority
legacy structural bool decisions do not export runtime legality
legacy structural bool decisions do not claim production lowering
```
