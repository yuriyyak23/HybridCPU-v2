# Phase 9 — Managed EH core and stack unwinding

## Goal

Implement managed `throw`, `try/catch/finally` and stack unwinding independently of CPU fault/trap mapping.

## Prerequisites

General per-method frame lowering, code-manager registration, architecture-neutral unwind records, internal type identity/assignability and GC are proven.

## Compiler ownership

- admit supported CIL EH regions;
- emit deterministic EH clause tables and native-PC ranges after final lowering;
- emit unwind data describing CFA/SP, saved registers, return PC and frame kind;
- lower `throw`/`rethrow` to runtime helpers;
- preserve GC roots and exception object liveness through handlers/finally.

## Managed Runtime ownership

- personality/dispatcher algorithm;
- native-PC -> method/code-manager lookup;
- catch type matching using internal TypeDescriptor assignability;
- frame unwinding and finally execution;
- exception object lifetime and unhandled-exception policy.

CPU faults are **not** managed throws in this phase. RuntimeKernel only supplies ordinary execution context/stack/trap separation; ISE has no EH-clause knowledge.

## V1 exclusions

Explicitly document unsupported filters, fault clauses, unusual IL forms or cross-native-transition unwind until their contracts are implemented.

## Tests

Nested catch/finally, throw across multiple frames, rethrow, throw in finally, recursion + unwind, GC during EH, invalid/corrupt EH/unwind tables fail closed, unhandled exception termination, deterministic metadata.

## Closure

A pure managed exception can unwind multiple compiled frames using only compiler-produced final-PC metadata and runtime logic, with no CPU trap dependency.