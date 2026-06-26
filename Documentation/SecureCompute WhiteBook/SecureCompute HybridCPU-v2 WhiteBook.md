# SecureCompute HybridCPU-v2 WhiteBook

This compatibility entrypoint replaces the former monolithic WhiteBook.

Start with [`00_README.md`](00_README.md).

Normative development truth:

`HybridCPU_ISE/docs/ref2/SecureComputeActivationPlan/`

The split WhiteBook explains the verified architecture and current activation
status. If wording here differs from the activation plan, production code or
tests, the activation plan plus code/test evidence wins.

Current status: the bounded gate classes through VMX boundary zero-authority,
plus the compiler no-emission decision gate, are verified. Nested child intent
remains future/design-fenced and does not authorize nested execution, mutable
nested state, Shadow VMCS authority or nested publication. Secure
migration/checkpoint/restore output-manifest classification, secure
debug/attestation visibility, VMX boundary zero-authority, and compiler
no-emission to controlled-emission remain fail-closed as described in the split
chapters. Secure backend execution and production activation remain closed. The
next production-oriented gate is positive runtime execution activation planning.
