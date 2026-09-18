# Phase 4 — Arrays, strings, value types and type initialization completion

## Goal

Add core managed data shapes and finish static/type initialization required by usable programs and Phase 5 static-root correctness.

## Prerequisites

Allocator, internal type identity, static storage and runtime helper/image bootstrap are stable.

## Arrays

Start with SZARRAY. Define deterministic layout: TypeDescriptor/header, length and elements. Lower `newarr`, `ldlen`, supported `ldelem`/`stelem`, explicit null/bounds checks and element address calculation.

Specify V1 policy for:

- reference-element store type checks / array covariance;
- `ldelema` and interior-reference lifetime/root-map behavior;
- unsupported ranks/lower bounds.

If `ldelema` cannot satisfy Phase 2 byref/interior rules, reject it explicitly.

## Strings

Freeze a UTF-16-compatible immutable representation unless deliberately justified otherwise. Define literal materialization, length/char access and literal/static-root registration. Interning is optional and must not be assumed.

## Value types and boxing

Start with fixed-layout scalar/blittable structs. Reference-bearing structs require explicit GC pointer maps for stack/object/array/boxed locations before admission. Boxing/unboxing is enabled only for shapes with proven layout and GC maps.

## Static/type initialization completion

Compiler/runtime must now execute supported `.cctor`/module initialization through the Phase 2 state machine and Phase 1 startup/helper contracts. Define exactly-once semantics in the current single-context model, initialization ordering, recursive initialization behavior and failed-initializer state.

## Tests

Primitive/reference arrays, null/bounds/overflow negatives, array store type checks, `ldelema` support or fail-closed diagnostics, deterministic string literals, reference-bearing struct policy, boxing round-trip + GC map golden tests, static field access and `.cctor` order/failure tests.

## Closure

Static roots and executable type initialization are complete enough for the Phase 5 collector; unsupported shapes remain explicit diagnostics.