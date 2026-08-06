# Release Conformance And Static Guards

## Required Proof Types

A gate is not closed by narrative alone. Closure requires:

- production source behavior where runtime semantics change;
- focused positive and negative tests;
- source scans for forbidden shortcuts;
- conformance matrix updates;
- release-gate assertions;
- status wording consistent with the actual closure class.

## Evidence Classes

| Evidence | Valid use | Not valid for |
| --- | --- | --- |
| executable named production-path test | behavior and reachability of that exact path | unrelated owners or paths |
| direct policy unit test | policy semantics | production call graph or side effects |
| source/path scan | forbidden dependency and candidate discovery | executability or authority |
| documentation/string test | wording consistency | runtime conformance or release approval |

Documentation/source-string checks are excluded from the execution-proof numerator. Caller-supplied booleans named `Validated`, `Authorized` or `...Proven` are inputs, not evidence.

## Mandatory Negative Boundaries

- ordinary absent/disabled/unmaterialized descriptor remains no-effect;
- non-ordinary absent/disabled/unmaterialized descriptor fails closed in direct generic admission tests; production CPU routing remains open;
- proof-only owner does not execute;
- admitted-denied hypercall does not execute or publish;
- projection does not mutate or publish;
- memory/I/O admission does not execute or publish;
- VMX/VMCS/`VmxCaps` do not own SecureCompute authority;
- host evidence, raw secrets and active pointers do not migrate as authority;
- output manifests do not create backend execution, completion publication or retire publication authority;
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
- output-manifest runtime/publication authority;
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

Release reproduction additionally requires an immutable reachable SHA, clean-tree disclosure, exact SDK and build properties, generator commands, test filters/counts/results and hashes for generated artifacts. The current checkout does not satisfy that release-evidence standard.
