# SecureCompute HybridCPU-v2 WhiteBook

This compatibility entrypoint replaces the former monolithic WhiteBook.

Start with [`00_README.md`](00_README.md).

Normative development truth:

`HybridCPU_ISE/docs/ref2/SecureComputeActivationPlan/`

The split WhiteBook explains the verified architecture and current phase status. If wording here differs from the activation plan, production code or tests, the activation plan plus code/test evidence wins.

Current status: Phases 00-17 and Phase 19 are verified for their bounded gate classes. Phase 18 remains a future/design-fenced nested child-intent RFC and does not authorize nested execution, mutable nested state, Shadow VMCS authority or nested publication. Phase 15 closes secure migration/checkpoint/restore output-manifest classification fail-closed for future request/result/completion/guest-output/retire/recomputed entries. Phase 16 closes secure debug/attestation visibility fail-closed for debug trace, attestation report, telemetry snapshot, host-inspection metadata and compatibility-alias evidence. Phase 17 closes VMX boundary zero-authority fail-closed for named positive-looking paths. Phase 19 closes compiler no-emission to controlled-emission as an explicit no-compiler-change decision; controlled-emission work stays future-gated. Secure backend execution and production activation remain closed. The next production-oriented gate is Phase 20 planning.
