# 06. Conformance, Negative Tests And Feature Promotion

## Required HybridCPU-local tests

### Secure ExternalRuntime

- stale secure-domain/region/execution handles cannot close current generations;
- malformed successful creation with failed compensation remains tracked/quarantined;
- exact parent mismatch is rejected;
- evidence receipt cannot be passed where authority is required;
- ambiguous closure prevents adapter state release.

### Secure policy

- every `SecureDomainOperationClass` has explicit allow/deny coverage;
- missing evidence/migration policy reaches dedicated denial;
- unknown operation class denies;
- compatibility projection cannot mutate authoritative state;
- stale measurement/memory/policy generation denies.

### Child + secure composition

- child authority remains a parent subset;
- secure composition requires exact child + secure + parent generations;
- guest mapping epochs are exact;
- VirtualIo cannot amplify parent device rights;
- event injection remains sequence-safe and publication-ordered.

### Compiler/replay

- semantic secure/virtual intent survives lowering;
- raw CXL topology is absent from ordinary IR/ABI;
- non-idempotent external replay without containment is rejected;
- direct coherent required operations fail when runtime proof is unavailable.

## Cross-project tests required before promotion

Coordinate with SingNextOS for Type-3-backed guest memory, secure overlay on the same mapping lineage, CXL.io -> VirtualIo, secure+virtual Type-2 execution, FM reconfiguration, hot-remove, security reset after completion before publication, ambiguous provider closure and process teardown.

## Feature promotion

Do not promote based on interface presence alone.

- child virtualization: `Executable` only when exact artifact/execution/VirtualIo path is proven;
- secure domains: `ProductionSecure` only when external secure enforcement, exact closure, evidence classification and cross-project negative tests pass;
- secure+virtual execution: requires both positive claims and exact composition; neither implies the other.

CXL hardware-looking security evidence, IDE/TSP flags or model measurements must not promote a feature level.

## CI evidence

Roadmap completion requires recorded test execution for the relevant HybridCPU solution/projects and cross-project conformance. Documentation claims alone are insufficient.
