# Phase 8 — Generics core: exact AOT instantiation first

## Goal

Support a bounded, statically reachable generic model without prematurely introducing sharing/dictionaries or dynamic code generation.

## Prerequisites

Internal type/method identity, object/value layouts, dispatch, GC and stable metadata/linker identities.

## V1 design

Start with exact AOT instantiation of every reachable supported closed generic type/method. Generic sharing, dictionaries and universal canonicalization are optional later optimizations, not correctness prerequisites.

Compiler owns generic reachability/instantiation, deterministic method/type IDs, code generation and symbol/relocation emission. Managed Runtime owns runtime TypeDescriptor identity/assignability for constructed generic types and any helper semantics.

Open generic execution, runtime code generation and unsupported constraints fail closed.

## Metadata dependency

Do not wait for Phase 14 reflection. The internal TypeDescriptor/method identity established in Phase 2 is the required authority for generic identity, dispatch and EH interactions.

## Tests

Reference/value type instantiations, nested closed generics, generic virtual/interface uses in admitted subset, static fields per constructed type, exact method identity, sharing explicitly absent where not implemented, unsupported open/constraint cases, deterministic instantiation order and byte-identical images.

## Closure

A statically reachable generic program executes end to end without reflection or new ISA support.