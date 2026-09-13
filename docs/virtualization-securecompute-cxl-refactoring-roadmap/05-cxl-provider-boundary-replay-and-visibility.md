# 05. CXL Provider Boundary, Replay And Visibility

## Goal

Keep CXL below HybridCPU authority while making external CXL-backed effects participate correctly in legality, replay, visibility and publication.

## Provider boundary

HybridCPU consumes provider-neutral SingNextOS receipts. CXL-specific facts are allowed only as opaque evidence/status material necessary to detect stale external state; normal ISA/compiler/runtime authority must not depend on physical topology.

Do not add CXL to `LegalityAuthoritySource`. CPU legality may depend on a valid external admission receipt, but the authority source remains the runtime/platform authority that issued it.

## Required semantic receipts

HybridCPU may need opaque correlation values for:

- external operation identity/generation;
- provider generation;
- virtual/secure admission generation;
- memory/device mapping generation;
- visibility/publication state;
- closure/containment state.

The exact CXL fabric/backing/security generations remain owned/interpreted by SingNextOS. HybridCPU treats a changed/stale external receipt as a fail-closed runtime condition.

## Replay

- replay certificate != permission to resubmit external work;
- a non-idempotent external effect cannot be replayed until the previous effect is proven closed/contained;
- provider unavailable is not sufficient proof;
- rollback of CPU-local speculative state must not imply rollback of external device effects;
- external effect state participates in GuardPlane checks before reuse/materialization.

## Visibility/publication

Preserve the chain:

```text
Submitted -> DeviceComplete -> Visible -> Published -> Released
```

Architectural retire/publication may occur only when CPU legality and the external lifecycle both allow it. Guest-visible event injection is later than publication authorization.

## CXL security evidence

CXL IDE/TSP/device-authentication/measurement facts are evidence predicates. They do not create child, secure, memory or I/O authority and do not independently justify `ProductionSecure`.

## Fault/reconfiguration

A stale provider/virtual/secure receipt before the next external effect rejects the effect. A stale receipt after effect but before publication requires close/containment or quarantine and suppresses architectural/guest publication.

## FutureGated

Direct coherent write, secure writable multi-host memory, secure P2P and transparent confidential migration remain outside this roadmap's positive path unless explicit enforcement contracts are added.
