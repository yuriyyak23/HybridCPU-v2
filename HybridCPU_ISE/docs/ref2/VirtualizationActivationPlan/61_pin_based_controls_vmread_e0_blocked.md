# Phase 61 — `PinBasedControls` exact read-only VMREAD E0

Status: `BLOCKED E0 / NO D2 / NO PRODUCTION COMPOSITION`.

The repository owner authorized one post-Phase-60 governance-only E0/D2 pool
for exactly `VmcsField.PinBasedControls`. The only candidate neutral owner was
the existing `CompatibilityControlDescriptor`; the only candidate value source
was its materialized neutral `CompatibilityEventRoutingPolicy`.

E0 fails before D2. In canonical production source, the descriptor and event
routing policy occur only in their own type definition. No production
bootstrap, domain construction, platform configuration, scheduler, admission,
execution, completion, or retire caller constructs a materialized descriptor or
consumes its event-routing policy. The generated VMCS projection schema names
the descriptor only as compatibility vocabulary, while the generic VMREAD
projection service continues to deny every compatibility-control value.

Exact bit coverage is also absent. The neutral event-routing policy contains
three owner-language facts:

- `RuntimeTrapPolicyRequired`;
- `NeutralTrapResultRequired`;
- `PublicationFenceRequired`.

Canonical production source contains no owner-approved frozen mapping from any
of those facts to any bit of the `PinBasedControls` compatibility value. It also
contains no accepted per-bit presence, legal-zero, unsupported-bit, or semantic
mismatch contract. Enum ordinals, schema membership, adjacency to other control
fields, and the fail-closed default cannot supply that missing equivalence.

## 2026-06-11 Audit Contract

- File name: `61_pin_based_controls_vmread_e0_blocked.md`.
- Purpose: prove or deny production construction/reachability and complete
  owner-approved per-bit equivalence for exactly `VmcsField.PinBasedControls`.
- Status: blocked E0; no D2 or production composition.
- Scope: governance E0 and, only after successful E0, immutable D2 materialization.
- No-goals: no production owner construction, mapper, delivery, other control
  field, VMCALL, VMWRITE, nested, SecureCompute, compiler, or adjacent subsystem.
- Code anchors: `CompatibilityControlDescriptor.cs`,
  `VmcsFieldProjectionSchema.cs`, and `VmcsReadOnlyValueProjectionService.cs`.
- Authority owner candidate: `CompatibilityControlDescriptor` only.
- Value-source candidate: materialized `CompatibilityEventRoutingPolicy` only.
- Authority owner: existing neutral `CompatibilityControlDescriptor` candidate
  only; VMX/VMCS and the mapper have no runtime authority.
- Required RFC/ADR: a separate neutral-owner construction correction and an
  attributable exact per-bit mapping decision are required before E0 retry.
- Acceptance criteria: real production construction and consumption, complete
  per-bit neutral equivalence, explicit presence/legal-zero/unsupported/mismatch
  rules, and continued adjacent denial.
- Tests/static scans: canonical caller/reference inventory, exact semantic-map
  denial, status/record guards, VMX/SecureCompute matrices, hook-free Release,
  forbidden scans, clean-source verification, and `git diff --check`.
- Risks: schema or enum reinterpretation, zero fallback, test-only construction,
  and accidental admission of adjacent compatibility controls.
- Next-gate dependency: no pool opens until the missing neutral construction and
  exact semantic coverage are separately accepted and E0 is repeated.
- Result: blocked because both production reachability and exact bit semantics
  are absent.
- D2: not materialized.
- Production composition/delivery: not opened.
- Activation: disabled; no profile exists.
- Rollback/kill switch: not materialized because no profile or receipt issuer is
  authorized by this blocked E0.
- Migration: projection-only candidate; no state or receipt is serialized.
- Runtime authority: unchanged neutral runtime owners only; VMX/VMCS remains a
  frozen compatibility vocabulary.

Owner map completeness remains unresolved at: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.

## Forbidden substitutions

The following cannot close E0:

- `VmcsFieldProjectionSchema` membership or `VmcsField.PinBasedControls = 64`;
- zero fallback, enum reinterpretation, compatibility adjacency, or raw VMCS
  backing state;
- `SecondaryProcControls`, any other control field, host alias, VMCALL, VMWRITE,
  nested, SecureCompute, compiler, memory/IOMMU/device/lane/stream sources;
- frontend, trap, completion, or diagnostics evidence as runtime authority.

## Exit and next candidate

The pool is closed `BLOCKED`. `NextOpenPool` returns to `None`. A future retry
requires a separately accepted neutral-owner construction correction that makes
the descriptor and immutable policy production-reachable, plus an attributable
per-bit semantic map proving exact `PinBasedControls` equivalence. E0 must then
be repeated from current production callers. No adjacent field opens
automatically.

## Final provenance

The immutable blocked-E0 subject is
`275c433d11a979856a4f84063d7114e200b6176f`, tree
`951ec4735882976b7b76b3ca01d67d7e0ea66fc0`. A later non-self-referential
evidence record binds that subject without naming its own containing commit.

Final gates were focused `332/332`, worktree VMX `580/580`, worktree
SecureCompute `406/406`, clean-staged VMX `562/562`, clean-staged SecureCompute
`395/395`, and hook-free Release at zero warnings and zero errors. No production
file changed in the subject.
