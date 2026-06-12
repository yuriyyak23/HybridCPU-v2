# Future Gates 13-23

## Exact Next Gate

Phase 13, `secure_hypercall_backend_owner_rfc.md`, is the exact next sequential gate.

It may define an RFC/ADR and typed request/result vocabulary. It must not open execution merely because secure hypercall recognition, shared-buffer policy or a publication fence exists.

## Remaining Sequence

| Phase | Future responsibility |
| --- | --- |
| 13 | neutral secure hypercall backend owner RFC |
| 14 | completion and retire publication separation |
| 15 | migration/checkpoint/restore activation-grade classification |
| 16 | stable debug and attestation visibility API |
| 17 | continued VMX zero-authority proof |
| 18 | nested child-intent owner RFC; execution remains separately gated |
| 19 | compiler no-emission to controlled-emission gate |
| 20 | first positive secure runtime execution activation plan |
| 21 | conformance matrix |
| 22 | limited release gate |
| 23 | open-decision quarantine |

## Conditions Before Positive Backend Work

A future backend path requires:

- named neutral owner;
- accepted owner-specific RFC/ADR;
- typed request/result contract;
- capability and evidence gates;
- migration classification;
- negative tests for every denied shortcut;
- explicit completion and retire policy;
- VMX zero-authority proof;
- compiler boundary decision;
- release-gate approval for one named path.

None of these requirements can be replaced by WhiteBook wording.
