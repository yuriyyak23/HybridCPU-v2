# Release Conformance And Static Guards

## Required Proof Types

A gate is not closed by narrative alone. Closure requires:

- production source behavior where runtime semantics change;
- focused positive and negative tests;
- source scans for forbidden shortcuts;
- conformance matrix updates;
- release-gate assertions;
- status wording consistent with the actual closure class.

## Mandatory Negative Boundaries

- ordinary absent/disabled/unmaterialized descriptor remains no-effect;
- non-ordinary absent/disabled/unmaterialized descriptor fails closed through Stage B;
- proof-only owner does not execute;
- admitted-denied hypercall does not execute or publish;
- projection does not mutate or publish;
- memory/I/O admission does not execute or publish;
- VMX/VMCS/`VmxCaps` do not own SecureCompute authority;
- host evidence, raw secrets and active pointers do not migrate as authority;
- nested child intent does not execute;
- compiler secure emission remains closed.

## Static Guards

The release suite scans for:

- former Stage B enabled-descriptor bypass;
- VMX activation or secure VMCS claims;
- `VmxCaps` grant claims;
- ID-only shared-buffer authority;
- fence-derived backend/completion/retire authority;
- tagged-memory or capability-aware ISA imports;
- host evidence or compatibility metadata migration;
- proof-only/admitted-denied execution overclaim;
- product-ready or feature-complete overclaim.

## Publication Ladder

The release gate keeps these states distinct:

1. admission;
2. owner proof;
3. internal backend result;
4. completion record;
5. completion publication;
6. retire publication;
7. named limited activation.

The current SecureCompute implementation does not reach a positive backend execution step.
