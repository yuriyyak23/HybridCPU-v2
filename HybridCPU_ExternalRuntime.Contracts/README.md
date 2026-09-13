# HybridCPU external runtime contracts 1.1

Versioned, semantic and hardware-opaque contracts for fail-closed HybridCPU
runtime integration. Consumers must validate contract version, manifest
generation, lease epoch and exact operation receipts.

Version 1.1 adds the additive `IHybridCpuChildDomainRuntimeV1` contract. It
binds every opaque child lease to an exact ordinary parent lease, admits only
an authority subset, and provides generation-bound child lifecycle, bounded
guest-memory mapping/unmapping, semantic event and trap receipts, and a
definitive terminal child-close receipt. No public type exposes VMX/VMCS,
physical addresses, provider-internal identities, or mutable hardware state.

The v1.1 feature families are advertised as `RuntimeAdmission`. Interface
presence does not claim production execution; a backend must independently
qualify before any family is advertised as `Executable`.
