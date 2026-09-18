# Phase 14 — Bounded public reflection and metadata retention

## Goal

Add opt-in public reflection after the internal runtime type metadata required by objects/dispatch/generics/EH already exists.

## Prerequisites

Internal TypeDescriptor/type identity, generics, GC, EH and stable image metadata are complete.

## Critical distinction

This phase does **not** create runtime type identity. Phase 2 already established internal metadata for assignability, dispatch, GC, generics and EH. Phase 14 only adds retention policy and public reflection objects/APIs.

## Ownership

Compiler owns reachability/retention and deterministic emitted metadata. Managed Runtime owns `Type` objects, caches and member lookup. Kernel/ISE have no reflection semantics.

## V1 scope

- obtain runtime type identity as a public `Type` object;
- selected type/member metadata queries;
- explicitly rooted metadata via configuration/attributes.

Defer dynamic code generation, Reflection.Emit, unrestricted assembly loading and arbitrary metadata retention.

## Tests

Retained vs trimmed members, generic reflected identities, missing-metadata diagnostics, deterministic retained table ordering, cache/identity behavior, unsupported dynamic-code requests, and dependency tests proving ISE/kernel do not parse reflection sections.

## Closure

Reflection remains opt-in and cannot silently defeat static reachability or reproducibility.