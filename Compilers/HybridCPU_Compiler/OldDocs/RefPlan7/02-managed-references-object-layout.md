# Phase 2 — Managed references, type identity, object and static layout

## Goal

Make managed object references/byrefs first-class compiler/runtime concepts while keeping them ordinary machine values at ISA level; define the internal type system and static storage needed by GC/dispatch/EH.

## Prerequisites

Phase 1 managed ABI/bootstrap/code-manager contracts are executable.

## Compiler responsibilities

- preserve `ObjectReference`, `ManagedByRef` and `InteriorReference` through SSA/phi, scheduling, RA and spills;
- make root-relevant value kind available to final metadata generation;
- lower explicit null checks for V1;
- lower field/static-field address/access from runtime-defined layouts;
- emit deterministic TypeDescriptor, base/interface relation and static-storage metadata;
- either support `ldflda`, byref locals/returns and future `ldelema` with explicit provenance/lifetime, or fail closed with stable diagnostics.

## Managed Runtime responsibilities

Freeze internal, non-reflection TypeDescriptor identity and assignability model including:

- base type and implemented-interface identity;
- instance size/alignment;
- instance/reference field map;
- static field storage descriptor + static GC pointer map;
- dispatch table placeholders;
- array/generic identity extension points;
- cast/type-test identity semantics.

Conceptual object layout:

```text
Object
  TypeDescriptor*
  runtime/GC header word
  instance fields...
```

## Byref/interior reference contract

Current ABI vocabulary already names managed byrefs/interior roots while the current metadata finalizer only supports object-reference roots. This phase must choose one explicit V1 policy:

1. implement final root-map representation and lifetime/provenance rules for derived/interior references; or
2. define a bounded admitted subset that rejects all programs requiring unsupported interior/byref liveness across safepoints.

Silently treating byrefs as ordinary object roots is forbidden.

## Static storage and type initialization foundation

Define static storage allocation, static-root registration and a runtime-owned type/module initialization state machine with states such as uninitialized/running/initialized/failed. `.cctor` ordering and failure semantics must be specified before GC closure; executable type initialization is completed no later than Phase 4.

## Tests

Golden layouts for base/derived/reference/static fields; type identity/assignability; reference/byref values through loop phi/call/spill; malformed metadata; static storage/root maps; `.cctor` state-machine unit tests; deterministic descriptor and static-section ordering.

## Forbidden work

No managed-aware register file, object validity in ISE, reflection API surface, or implicit null-fault dependency.