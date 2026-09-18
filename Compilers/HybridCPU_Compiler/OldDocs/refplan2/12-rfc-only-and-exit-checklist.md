# 12 - RFC-only contours, ADRs, and final exit checklist

## Status and scope

Complete as a documentation and exit-audit phase. The normal compiler
production-package track ends with the six bounded providers recorded in the
README. This phase does not approve a runtime backend, migrate public callers,
or remove legacy APIs.

The checked items below mean that the compiler package boundary is implemented
and verified. They do not mean that a compiler package has runtime execution,
publication, commit, retire, token, VMCS, or guest/domain authority.

## RFC-only contours

### VMX projection-only

VMX remains projection/no-emission. The normal provider track must not add:

- VMX carrier emission;
- compiler-owned VMCS state;
- `VmxCaps` as compiler capability authority;
- VMX execution, commit, retire, or publication claims;
- guest architectural state mutation from compiler evidence.

An RFC for a VMX runtime backend must define, before code is accepted:

1. runtime ownership of VMCS and all VMX backend state;
2. runtime legality stages and their authority source;
3. compiler-to-runtime bridge inputs that cannot mutate guest state;
4. evidence isolation, replay/certificate rules, and audit trail;
5. decoder/execution/commit/retire/publication conformance and rollback gates.

### SecureCompute policy/admission/evidence-only

SecureCompute remains policy/admission/evidence-only. The normal provider track
must not add:

- secure backend carrier emission;
- policy/admission success as execution authority;
- certificate, token, or evidence as execution rights;
- host-owned evidence in guest/domain architectural state.

An RFC for a SecureCompute runtime backend must define, before code is accepted:

1. runtime-owned admission, attestation, and backend authority;
2. domain/host evidence isolation and serialization rules;
3. runtime legality, publication, commit, retire, fault, and rollback rules;
4. negative authority tests proving policy/certificate success is not execution;
5. ISE execution and conformance coverage for the approved backend only.

### Non-production contour states

The following states remain non-production until a new RFC creates a
runtime-owned architecture and dedicated test matrix:

| State or contour | Required behavior |
| --- | --- |
| `ParserOnly` | Parser/ABI evidence only. |
| `NoEmission` | No carrier construction. |
| `FutureGated` | Fail closed until all explicit gates exist. |
| `UnknownRejected` | Fail closed; never choose a fallback contour. |
| MatrixTile helper-only | Helper ABI only unless an RFC defines a runtime backend contour. |

## ADR/RFC docket

These are recorded architectural decisions/work items, not approved runtime
authority grants. An implementation PR may not treat their presence in this
document as approval.

| Docket | Status | Required decision before implementation |
| --- | --- | --- |
| Production-package authority model | Implemented for normal provider track | Compiler package remains runtime-authority-pending. |
| Contour provider lifecycle | Implemented for normal provider track | Exact-contour registry and no-fallback responsibilities. |
| Artifact/gate/parity contract | Implemented for normal provider track | Separated envelopes, explicit gates, golden and ISE parity. |
| Runtime Legality A/B handoff | Declared, runtime-owned | Runtime must make the final legality decision. |
| VMX backend RFC | Open; RFC-only | VMCS ownership and full runtime lifecycle. |
| SecureCompute backend RFC | Open; RFC-only | Admission/attestation authority and domain isolation. |
| MatrixTile backend RFC | Open if production is requested | Explicit runtime contour, execution, and lifecycle contract. |
| Caller migration and legacy removal ADR | Open; not claimed by this plan | Migration order, compatibility window, and removal criteria. |

## Final exit checklist

### Verified for the normal provider/package track

- [x] Production-capable contours are explicitly listed and exact-contour gated.
- [x] Helper/parser/no-emission/future-gated contours are explicitly listed.
- [x] Production package construction is confined to the production-provider contract.
- [x] Every provider identifies its package, contour, gate result, telemetry, and no-fallback proof.
- [x] Every successful provider result retains Legality A/B, execution,
  publication, commit, and retire as runtime dependencies.
- [x] Carrier, sideband, descriptor, typed facts, evidence, and bridge remain
  separated envelopes.
- [x] Descriptor and helper success remain separate from execution authority.
- [x] Golden artifacts and compiler-to-ISE decode/encode/lane/slot parity exist
  for all implemented provider contours.
- [x] Negative matrices cover descriptorless submit, contour mixing, malformed
  descriptors, missing gates, fallback, VMX, and SecureCompute boundaries.
- [x] Production gates can be disabled by profile/contour gate without a runtime
  ABI change.

### Explicitly not claimed as complete

- [ ] Public caller migration to production-provider packages. No migration was
  authorized or performed by this plan.
- [ ] Legacy API deprecation/removal plan. This requires the separate caller
  migration and compatibility-window ADR.
- [ ] VMX runtime backend authority. RFC-only.
- [ ] SecureCompute runtime backend authority. RFC-only.
- [ ] MatrixTile production backend authority. RFC-only if requested.

Unchecked items are deliberate scope boundaries. They prevent this document
from being read as a claim that the whole compiler/runtime refactor, legacy
cleanup, or any RFC-only backend is complete.

## Verification performed for this exit audit

The audit verified that the documentation's completed phases have matching
implementation and test artefacts:

- Phase 01 source scanner: `ProductionLoweringReadinessScanner` and
  `CompilerPhase01ReadinessSourceScannerTests`.
- Phase 02 golden harness: `CompilerGoldenArtifactHarness`, manifests, and
  `CompilerPhase02GoldenArtifactHarnessTests`.
- Phase 03 gate model, Phase 04 provider split, and Phase 05 parity harness.
- Provider/test pairs for phases 06-11, including direct vector transfer, DSC
  lane6, and L7-SDC lane7.
- Registry resolution for exactly six normal providers; VMX and SecureCompute
  remain unresolved by the production-provider path.

Commands run during this Phase 12 audit:

```text
dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~CompilerPhase01|...|CompilerPhase11"

dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~CompilerTests"
```

Final results for this audit:

- cumulative Phase 01-11 suite: 213 passed, 0 failed, 0 skipped;
- full `CompilerTests` suite: 684 passed, 0 failed, 1 existing skipped test.

## Next work

There is no next ordinary production-provider phase. Any request to add a VMX,
SecureCompute, MatrixTile, parser-only, or no-emission runtime backend must
start with its corresponding RFC/ADR docket above, define runtime ownership,
and add a new fail-closed test matrix before implementation.
