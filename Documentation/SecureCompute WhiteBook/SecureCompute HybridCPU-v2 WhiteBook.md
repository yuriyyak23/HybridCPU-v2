# SecureCompute HybridCPU-v2 WhiteBook

This compatibility entrypoint replaces the former monolithic WhiteBook.

Start with [`00_README.md`](00_README.md).

Normative development truth:

`HybridCPU_ISE/docs/ref2/SecureComputeActivationPlan/`

The split WhiteBook explains the verified architecture and current activation
status. If wording here differs from the activation plan, production code or
tests, the activation plan plus code/test evidence wins.

Current status: the code provides policy/classification surfaces, a narrow
read-only VMX zero-authority projection and compiler no-emission enforcement.
It does not provide a canonical descriptor registry, SafetyVerifier-issued
SecureCompute certificate, grant ledger or proven production chain from decode
through backend result, completion and retire. Phase 18 remains
future/design-fenced, Phase 20 is pre-activation evidence classification,
Phase 21 is negative/future-gated conformance and Phase 22 is hard-denied.
Positive runtime execution and limited/production release remain forbidden.
