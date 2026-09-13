# Activation Evidence Ledger

## Audit Result

Revalidated on 2026-08-06 against current production sources and tests. The table classifies bounded evidence only; it does not infer production reachability from a policy type or a test that calls the policy directly.

| Gate | Verified status | Primary proof | Remains closed |
| --- | --- | --- | --- |
| 00 | index and ordering established | activation index and release corpus | runtime authority |
| 01 | current-state/gap matrix verified | code/test anchor matrix | activation claim |
| 02 | documented guard set; executable scope partial | source/doc guards | guards as runtime proof |
| 03 | process document only | RFC/ADR template | RFC as runtime authority |
| 04 | admission-level ordinary no-effect verified; end-to-end equivalence open | descriptor/admission tests | pipeline/SMT/FSP/memory/I/O/exception/retire equivalence |
| 05 | descriptor validation exists; lifecycle owner open | root/subdescriptor tests | public construction, two carriers, missing revoke owner |
| 06 | generic admission service exists; CPU Stage-B integration open | direct service tests | decode/SafetyVerifier/certificate/issue reachability |
| 07 | caller-supplied grant validation exists; ledger open | policy tests | mint/revoke/consume owner |
| 08 | measurement/evidence classifiers exist; publisher open | direct policy tests | production evidence publication path |
| 09 | privileged execution-state owner proof implemented | descriptor/policy tests and source guard | projection from owner alone |
| 10 | `GuestCr0`/`GuestCr4` read-only projection implemented | all-gate positive/negative tests | VMWRITE, broad fields, side effects |
| 11 | memory policy class exists; enforcement path and canonical map open | direct policy tests | production memory/DMA path, overlap rejection |
| 12 | I/O policy class exists; device/IOMMU path and canonical map open | direct policy tests | device effect owner, IOMMU revoke, overlap/duplicate rejection |
| 13 | proof-only identifiers and contract vocabulary | registry and direct policy tests | executable hypercall owner/result |
| 14 | negative publication classifier | direct policy tests/source guards | production backend-result/completion/retire owners |
| 15 | partial migration/output classifier; unknown classes default allow | direct policy tests | serializer/key/anti-replay/atomic restore protocol |
| 16 | visibility classifier only | direct policy tests/source guards | single production evidence publisher |
| 17 | narrow VMX zero-authority boundary verified | projection and denial tests | any VMX authority |
| 18 | unconditionally future/design-fenced | negative design-fence tests | all nested execution |
| 19 | current compiler no-emission enforced; reproducible generated-artifact proof open | contour/lowering rejection tests | controlled emission before limited runtime release |
| 20 | pre-activation evidence classifier only | negative classifier tests | runtime execution |
| 21 | negative/future-gated matrix only | mixed executable/source/doc checks | positive conformance or release proof |
| 22 | hard-denied classifier | denial tests | limited/production release |

## Audit Corrections Applied

- generic admission is no longer described as closed CPU Stage-B enforcement;
- ordinary no-effect and non-ordinary fail-closed behavior are separated;
- `GuestCr0` / `GuestCr4` status is updated from blanket denial to gated read-only projection;
- descriptor validation is separated from lifecycle ownership;
- grant validation is separated from mint/revoke authority;
- memory, I/O, migration, evidence and publication policy classes are separated from effect-path enforcement;
- executable behavior tests, source guards and documentation-string tests are reported separately;
- phases 18, 20, 21 and 22 retain their required future/negative/hard-denied classifications.

The detailed confirmed/partial/overestimated/open table and ranked C0/C1/C2 register are maintained in ActivationPlan phase `24`.

## Residual Risk

The repository worktree contains unrelated parallel changes. Verification for this ledger must use focused SecureCompute and VMX boundary slices and must not treat unrelated suite failures as SecureCompute evidence.
