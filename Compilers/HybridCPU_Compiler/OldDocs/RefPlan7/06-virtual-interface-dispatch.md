# Phase 6 — Instance methods and virtual/interface dispatch

## Goal

Enable ordinary instance methods first, then `callvirt`, virtual/interface dispatch, casts and type tests using the existing generic indirect control-transfer ISA.

## Prerequisites

Instance-method CIL admission, `this` ABI and constructor invocation are proven; internal type identity/assignability, GC and final call/frame lowering are stable.

## Required compiler work

- admit supported instance methods/constructors and lower hidden `this` through the existing managed call ABI;
- distinguish direct non-virtual calls from virtual/interface resolution;
- lower resolved code pointers through generic indirect call (`JALR` baseline);
- preserve GC roots across resolver/helper and indirect-call sequences;
- emit deterministic vtable/interface-map metadata and relocations.

## Runtime work

Define virtual slot identity, inheritance/override rules, interface map lookup, null semantics for `callvirt`, `isinst`/`castclass` assignability and resolver slow paths.

## ISA policy

Current audited ISE implements register-valued `JALR`; therefore `CALLVIRT`, `CALLINTERFACE` or managed dispatch opcodes are forbidden unless an explicit conformance test later proves a generic architectural deficiency.

## Tests

Instance call/constructor with `this`; override chain; interface dispatch; null receiver; invalid cast; multiple implementations; resolver GC safety; indirect call under recursion/spill pressure; deterministic table ordering; negative metadata corruption.

## Closure

End-to-end instance/virtual/interface calls execute via normal machine indirect transfer and versioned runtime metadata, with no ISE knowledge of managed types.