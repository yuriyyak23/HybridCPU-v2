# 07. Migration Order, PR Slicing And Exit Criteria

## Recommended implementation order

1. Harden ISE secure policy dispatch to exhaustive deny-by-default behavior.
2. Add external feature classification capable of `ProductionSecure`.
3. Add SecureCompute ExternalRuntime V1 contracts and model implementation with exact closure/quarantine.
4. Add child + secure composition and secure guest mapping lineage.
5. Complete executable child virtual-event forwarding/publication ordering.
6. Extend compiler IR and lowering with provider-neutral secure/virtual semantic requirements.
7. Integrate external admission/visibility/closure receipts into GuardPlane/replay/retire paths where required.
8. Add local negative tests.
9. Integrate with SingNextOS secure-virtual/CXL roadmap.
10. Run cross-project conformance and only then promote feature manifests.

## Suggested PR slices

- PR A: ISE secure operation policy hardening.
- PR B: ExternalRuntime feature model + SecureCompute contracts.
- PR C: ExternalRuntime secure implementation + exact lifecycle/closure tests.
- PR D: child/secure composition + secure guest mapping contracts.
- PR E: executable adapter event path + publication ordering.
- PR F: compiler semantic IR/lowering + negative tests.
- PR G: GuardPlane/replay/external effect correlation hardening.
- PR H: SingNextOS integration/conformance fixes and feature promotion.

Each PR must preserve fail-closed behavior if later-layer support is absent.

## Explicit non-goals

Do not use this roadmap to add raw CXL topology to ISA/compiler contracts, create a CXL lane, grant guest CXL identities, make evidence an authority token, guarantee zero-copy, enable DirectCoherentWrite without alias exclusion, or claim confidential migration/nested secure domains without dedicated contracts.

## Exit criteria

All of the following must be true:

- `ProductionSecure` is backed by a versioned owner-bound external contract and enforcement implementation;
- all secure operation classes have explicit policy;
- exact child+secure composition exists and stale generations fail closed;
- guest events are injected only after external publication authorization;
- compiler carries semantic secure/virtual requirements without topology/runtime handles;
- replay cannot duplicate an uncontained external effect;
- HybridCPU consumes SingNextOS external closure/visibility semantics without treating CXL as authority;
- cross-project Type-3, CXL.io and Type-2 secure virtualization tests pass;
- ambiguous provider effects prevent release/reclaim;
- DirectCoherentWrite and other FutureGated items remain unavailable unless independently completed.

Only after these conditions are demonstrated may the roadmap status be changed from Proposed/In Progress to Complete.
