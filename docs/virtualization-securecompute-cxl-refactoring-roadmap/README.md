# HybridCPU-v2 / ISE / Compiler Virtualization + SecureCompute + CXL Refactoring Roadmap

Status: proposed repository-local roadmap derived from the 2026-09-13 SingNextOS ↔ HybridCPU-v2 ↔ ISE ↔ CXL end-to-end audit.

Baseline: `master` at `7a096d97a77abaa3208c0c371db0d6c3ac37f3f8` when this roadmap was created.

## Scope

This roadmap contains only work owned by `HybridCPU-v2`, including `HybridCPU_ISE`, `HybridCPU_ExternalRuntime*`, executable adapters and `Compilers`. SingNextOS kernel/runtime work is tracked separately in `yuriyyak23/SingNextOS/docs/virtualization-securecompute-cxl-refactoring-roadmap/`.

## Target architecture

```text
compiler semantic intent
 -> HybridCPU legality / GuardPlane / replay classification
 -> ExternalRuntime exact child + secure contracts
 -> SingNextOS authority/materialization
 -> provider-neutral external operation
 -> optional CXL substrate below SingNextOS
 -> visibility/publication receipt
 -> HybridCPU retirement / guest event
```

## Mandatory invariants

- CXL does not become an ISA lane, `Remote Lane`, `LegalityAuthoritySource`, replay authority or guest/application capability.
- Virtualization and SecureCompute remain separate authorities and compose by exact binding.
- evidence != authority; replay certificate != runtime permission.
- completion != visibility != publication.
- `ProviderUnavailable != ProviderClosed != ProviderEffectContained`.
- exact generation/epoch correlation is mandatory before provider effects and publication.
- compiler output expresses semantic requirements only; it cannot mint or carry live runtime authority.
- direct coherent writes and zero-copy remain FutureGated until alias exclusion + symmetric closure are proven.

## Phase index

1. `00-baseline-gaps-and-decisions.md`
2. `01-externalruntime-securecompute-v1.md`
3. `02-ise-secure-policy-hardening.md`
4. `03-child-secure-composition-events-and-publication.md`
5. `04-compiler-secure-virtual-semantic-intent.md`
6. `05-cxl-provider-boundary-replay-and-visibility.md`
7. `06-conformance-negative-tests-and-feature-promotion.md`
8. `07-migration-order-pr-slicing-and-exit-criteria.md`

## Completion definition

The roadmap is complete only when HybridCPU can receive compiler intent for a secure+virtualized external operation, preserve CPU-side legality/replay semantics, obtain exact owner-bound authorization through ExternalRuntime/SingNextOS, execute without learning CXL topology, revalidate security/generations before publication, publish a guest event only after publication, and prove exact external closure before authority is released.
