# 00. Baseline Gaps And Decisions

## Current strengths to preserve

- ExternalRuntime V3 already has exact child domain, guest mapping, artifact, execution generation and bounded VirtualIo contracts.
- ISE already has neutral SecureCompute descriptors, admission services, secure memory policy and domain-tag/address-space checks.
- Runtime legality and replay have explicit GuardPlane/certificate/effect separation.
- compiler typed-slot/bundle admission already provides a suitable carrier for semantic external-operation requirements.

## Gaps found by the audit

1. ExternalRuntime has no SecureCompute feature family or owner-bound secure ABI.
2. External feature availability has no `ProductionSecure` class.
3. child virtualization and SecureCompute have no exact composition binding.
4. external child event support exists below the adapter, but the executable adapter path does not yet own/forward guest event injection.
5. several `SecureDomainOperationClass` values fall through generic secure admission instead of explicit policy.
6. compiler/runtime planning can express `RequiresVirtualizedDomain` / secure evidence as booleans, but an actual provider effect is not thereby correlated with an exact virtual/secure domain.
7. CXL security evidence can satisfy predicates but must not be interpreted as SecureCompute authority.
8. CXL/provider topology must remain below SingNextOS and outside normal HybridCPU/compiler ABI.

## Architectural decisions

- introduce a separate versioned SecureCompute ExternalRuntime contract instead of overloading V3 child contracts;
- add `ProductionSecure` to the external feature classification or an equivalent strictly stronger class;
- compose child and secure authority through exact opaque bindings rather than a `SecureVm` root object;
- keep CXL absent from ISA/IR topology and legality authority enums;
- use immutable semantic compiler requirements, then resolve exact runtime authority at execution;
- preserve staged publication as safe default;
- keep direct coherent writes FutureGated;
- deny unknown/unsupported secure operation classes by default;
- exact terminal close or proven containment is required before release/reclaim.

## New external dependency boundary

HybridCPU may consume opaque SingNextOS receipts containing semantic status and generations. It must not depend on HDM decoder IDs, DPA, LD IDs, CXL switch routes, FM internal bindings or physical topology.

## Exit criterion

No implementation phase may weaken existing ordinary child virtualization or non-secure execution behavior while adding the secure contour.
