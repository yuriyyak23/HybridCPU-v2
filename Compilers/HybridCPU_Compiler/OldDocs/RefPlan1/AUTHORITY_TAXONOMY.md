# AUTHORITY_TAXONOMY - Phase 02 Compiler Core Types

Status: initial implementation slice, 2026-07-08.

Implemented source:

```text
HybridCPU_Compiler/Core/IR/Authority/CompilerAuthorityTaxonomy.cs
HybridCPU_Compiler/Core/IR/Authority/CompilerStructuralAuthorityQuarantine.cs
```

Namespace:

```text
HybridCPU.Compiler.Core.IR.Authority
```

## Implemented Types

```text
CompilerAuthorityClass
CompilerAuthoritySourceKind
CompilerRuntimeAuthorityDependency
CompilerEvidenceClass
CompilerPublicationClass
CompilerExecutionClaim
CompilerCoreResultHeader
CompilerStructuralBundleAdmissionResult
CompilerStructuralPlacementReport
CompilerStructuralCandidateBundleAnalysis
CompilerStructuralAuthorityQuarantine
```

## Forbidden Compiler-Side Claims

The initial taxonomy intentionally does not expose these enum values or header
states:

```text
Executable
RuntimeLegal
ExecutionReady
Committed
Retired
PublishedArchitecturalState
CapabilityAuthority
CanExecute
```

## Semantics

`CompilerCoreResultHeader` is a compiler-product header. It can describe
structural admission evidence, transport construction, descriptor ABI
construction, typed-slot fact production, evidence production, or runtime bridge
preparation.

It cannot describe final runtime legality, execution completion, architectural
publication, commit, or retire.

`CompilerRuntimeAuthorityDependency` is a dependency map. It records what the
runtime must still decide after a compiler product exists. It is not a grant of
authority.

`RuntimeOwnedPolicyReference` means the compiler observed a runtime-owned policy
surface. It is not permission to execute.

## Legacy Quarantine Rule

Existing compiler-side names below remain legacy structural vocabulary until
callers migrate through typed wrappers:

```text
IrBundleLegalityResult
IrCandidateBundleAnalysis.IsLegal
LegalSlots
HasLegalAssignment
EvaluateCandidateBundle
EvaluateClusterPreparedLegality
```

Migration targets:

```text
IrBundleLegalityResult -> CompilerStructuralBundleAdmissionResult
IrCandidateBundleAnalysis.IsLegal -> IsStructurallyAdmissible
LegalSlots -> StructurallyAllowedSlots
HasLegalAssignment -> HasStructuralPlacement
EvaluateCandidateBundle -> AnalyzeStructuralCandidateBundle
EvaluateClusterPreparedLegality -> AnalyzeClusterPreparedStructuralAdmission
```

Implemented quarantine adapters:

```text
IrBundleLegalityResult -> CompilerStructuralBundleAdmissionResult
IrSlotAssignmentAnalysis -> CompilerStructuralPlacementReport
IrCandidateBundleAnalysis -> CompilerStructuralCandidateBundleAnalysis
```

Adapter rules:

```text
IsLegal -> IsStructurallyAdmissible
HasLegalAssignment -> HasStructuralPlacement
LegalSlots / CombinedLegalSlots -> StructurallyAllowedSlots
```

The adapters set:

```text
authority.class = StructuralAdmissionEvidence | StructuralPlacementEvidence | StructuralAgreement
authority.source_kind = CompilerStructuralModel
evidence.class = StructuralAdmissionEvidence | StructuralPlacementEvidence | StructuralEvidence
publication.class = EvidenceOnly
execution.claim = NoExecutionClaim
runtime_dependency = RuntimeLegalityARequired | RuntimeLegalityBRequired | RuntimeExecutionRequired
```

This is an observe/wrap slice only. It does not migrate existing callers,
rename legacy APIs in place, or convert structural evidence into runtime
LegalityDecision.
