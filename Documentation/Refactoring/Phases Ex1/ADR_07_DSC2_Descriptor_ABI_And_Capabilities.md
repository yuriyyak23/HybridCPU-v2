# ADR 07: DSC2 Descriptor ABI And Capabilities Gate

## Status

Accepted for parser-only foundation. Executable DSC2 remains gated.

This ADR is implementation-oriented, but it does not approve CPU/ISE execution changes. Parser-only DSC2 extension work is staged only as non-executable descriptor handling. Executable use remains blocked by the executable DSC gate, token lifecycle, precise fault publication, ordering/conflict service, backend/addressing, cache protocol, compiler contract, and conformance migration.

## Context

Phase 07 covers TASK-006: preserve DSC1 and define the future DSC2 or capability-gated extension mechanism for stride, tile, scatter-gather, address-space selection, and deterministic normalized footprints.

The current DSC1 parser/model is intentionally narrow. That narrowness is architectural compatibility evidence, not a gap to patch through reserved fields.

## Current Contract

- Lane6 `DmaStreamComputeMicroOp` remains fail-closed and is not executable DSC ISA.
- `DmaStreamComputeDescriptorParser.ExecutionEnabled == false`.
- `DmaStreamComputeRuntime.ExecuteToCommitPending` remains an explicit helper/model path, not ISA execution.
- DSC1 uses magic `DSC1`, ABI version `1`, and current header size `128`.
- DSC1 range encoding is `InlineContiguous`.
- DSC1 partial completion policy is `AllOrNone`.
- DSC1 supported operations are fixed to the current parser/model set: `Copy`, `Add`, `Mul`, `Fma`, and `Reduce`.
- DSC1 supported element types are fixed to the current parser/model set: `UInt8`, `UInt16`, `UInt32`, `UInt64`, `Float32`, and `Float64`.
- DSC1 supported shapes are fixed to `Contiguous1D` and `FixedReduce`.
- DSC1 reserved fields, dirty flags, mask ranges, accumulator ranges, rounding mode, and lane carrier reserved bits are rejected.
- DSC1 does not support stride, tile, 2D descriptors, scatter-gather, executable IOMMU address spaces, capability blocks, or successful partial completion.
- DSC2 parser-only support uses distinct `DSC2` magic, ABI 2.0, fixed header size 128, typed extension blocks, explicit capability stages, and execution-disabled descriptor results.
- DSC2 parser-only validation may produce normalized exact or conservative footprints for strided range, tile/2D, and scatter-gather extensions, or reject unsafe/under-approximated forms.
- DSC2 parser-only acceptance does not allocate tokens, does not issue through the normal pipeline, does not publish memory, does not call the runtime/helper path, and does not grant compiler/backend lowering.
- `DmaStreamComputeMicroOp` may carry normalized read/write footprint evidence, footprint hashes, serialization metadata, and resource masks, but execution remains fail-closed.
- Current compiler/backend production lowering must not rely on DSC2, extension blocks, stride/tile/scatter-gather, IOMMU-translated DSC addresses, partial success, or executable DSC.

## Decision Under Review

The decision under review is whether future DSC descriptor capabilities should be added by changing DSC1 in place or by defining a separate DSC2/capability-gated ABI.

### Recommended Decision

Keep DSC1 immutable and strict. Add new descriptor behavior only through DSC2 or explicit capability-gated extension blocks.

The recommended position is:

- DSC1 remains frozen and must continue rejecting reserved fields.
- DSC2 must have a distinct versioned parser path.
- Extension blocks must be explicitly typed, length-bounded, versioned, and capability-gated.
- Parser-only DSC2 support may exist without executable DSC support.
- Parser acceptance, capability metadata, and footprint normalization do not authorize execution.
- Executable DSC2 use requires all dependent gates and tests.
- Compiler/backend production lowering to DSC2 is forbidden until capability discovery, executable semantics, and conformance tests exist.

## Accepted Direction

### DSC2 ABI Boundary

Future DSC2 must define a new typed descriptor model rather than mutating DSC1.

The DSC2 header must define, at minimum:

- magic or ABI marker distinct enough to avoid DSC1 ambiguity;
- major and minor ABI version;
- header size;
- total descriptor size;
- descriptor flags with defined compatibility behavior;
- extension table offset, count, and byte size;
- descriptor identity hash;
- capability set hash or capability profile id;
- owner virtual thread, context, core, pod, domain, and device binding;
- address-space summary or pointer to an address-space extension;
- normalized footprint hash;
- required parser status: parser-only, execution-disabled, or execution-eligible after gates.

DSC2 may reuse concepts from DSC1, but it must not rely on DSC1 reserved fields or lane carrier transport hints for new semantics.

### Extension Block Format

Every extension block must be self-describing:

- extension type;
- extension version;
- byte length;
- required versus optional or criticality flag;
- alignment requirement;
- payload checksum, hash, or deterministic validation rule when needed;
- capability id or feature bit that authorizes interpretation;
- minimum parser version;
- compatibility behavior if the block is unknown.

Unknown required extensions must reject. Unknown optional extensions may be ignored only if the extension class is explicitly defined as non-semantic metadata and cannot change memory ranges, operation semantics, address-space selection, completion policy, ordering, faults, or compiler-visible behavior.

### Capability Negotiation

Capability negotiation must be separate from execution enablement.

A future DSC capability model must distinguish:

- parser-known capability;
- descriptor validation capability;
- footprint-normalization capability;
- backend/address-space capability;
- execution capability;
- compiler-lowering capability.

Parser-known and validation-known capabilities do not imply execution. Execution capability can be true only after the relevant phase gates are approved and conformance tests pass.

Existing L7 external accelerator capability metadata may inform shape and registry style, but it is model-only for L7 and does not authorize DSC2 execution or compiler lowering.

### Required Future Extension Families

The first DSC2 design should reserve or define extension families for:

- `AddressSpace`: physical versus IOMMU-translated selection, tied to Phase 06.
- `StridedRange`: base, element count, element size, stride, access kind, and overflow rules.
- `Tile2D`: base, rows, columns, row stride, column stride or layout, element size, and tile bounds.
- `ScatterGather`: bounded segment count, segment table format, index size, access kind, and normalization limits.
- `CapabilityProfile`: capability id, version, required feature bits, and compatibility mode.
- `FootprintSummary`: normalized exact or conservative footprint summary plus hash.

Partial-success descriptors are not defined by this ADR. If partial success is ever approved, it belongs to Phase 08 plus a separate future ADR and must be encoded only in DSC2 or a capability-gated extension.

### Normalized Footprint Rules

Every future memory-affecting extension must normalize before issue/admission.

The normalizer must produce one of:

- exact read/write ranges;
- bounded conservative read/write ranges with a clear over-approximation rule;
- rejection when the footprint cannot be represented safely.

The normalizer must validate:

- integer overflow;
- zero-length elements or ranges;
- element-size alignment;
- stride and tile overflow;
- scatter/gather segment table bounds;
- maximum range count;
- maximum normalized byte span;
- read/write alias policy;
- owner/domain/device/address-space consistency;
- deterministic footprint hashing.

Under-approximated footprints are forbidden because they would break scheduling, conflict service, backend selection, cache protocol, and compiler assumptions.

### Parser-Only Semantics

Parser-only DSC2 support means:

- descriptor bytes may be decoded and validated;
- capabilities may be reported as metadata;
- normalized footprints may be produced for diagnostics or scheduling design;
- execution remains disabled;
- no token is allocated by normal pipeline issue;
- no memory effects are produced;
- compiler/backend must not treat parser acceptance as production lowering permission.

Parser-only descriptors must carry an explicit execution-disabled state or validation result so tests and documentation cannot confuse parsing with execution.

## Rejected Alternatives

### Alternative 1: Reuse DSC1 Reserved Fields

Rejected.

DSC1 reserved fields are currently rejected. Reusing them for stride, tile, scatter-gather, address-space, or partial-success behavior would silently break compatibility and parser truth.

### Alternative 2: Treat Parser Availability As Execution Approval

Rejected.

A parser can validate bytes and build footprints. It does not define issue/admission tokens, precise faults, ordering, backend selection, cache visibility, completion, retire, replay, squash, trap, or compiler semantics.

### Alternative 3: Encode Stride/Tile/Scatter As InlineContiguous

Rejected.

Complex addressing must either normalize into exact or conservative read/write footprints or reject. Pretending it is `InlineContiguous` would hide memory effects from scheduling and conflict checks.

### Alternative 4: Capability Metadata Without Feature Gates

Rejected.

Capabilities must say what stage they authorize. Metadata-only capability is not parser authority, execution authority, or compiler-lowering authority.

### Alternative 5: Put Partial Success In Phase 07

Rejected.

Phase 07 may reserve a future extension point, but successful partial completion is governed by Phase 08 and requires a separate ADR.

## Exact Non-Goals

- Do not implement CPU/ISE code in this ADR.
- Do not change DSC1 parser behavior.
- Do not reinterpret DSC1 reserved fields.
- Do not make DSC2 executable by parser availability.
- Do not approve production compiler/backend lowering to DSC2.
- Do not define successful partial completion.
- Do not define IOMMU executable behavior beyond reserving the DSC2 address-space ABI connection to Phase 06.
- Do not claim cache coherence or non-coherent flush/invalidate behavior; Phase 09 owns that protocol.
- Do not use StreamEngine/DMAController helper paths as DSC2 execution evidence.
- Do not treat L7 capability metadata as DSC2 execution authority.

## Required Prerequisites Before Executable Use

- Phase 02 executable lane6 DSC approval.
- Phase 03 token store and issue/admission allocation boundary.
- Phase 04 precise fault publication and priority.
- Phase 05 global conflict service and ordering semantics.
- Phase 06 address-space/backend resolver and no-fallback policy.
- Phase 08 all-or-none and any future partial-success decision.
- Phase 09 cache/prefetch/non-coherent protocol.
- Phase 11 compiler/backend lowering contract.
- Phase 12 conformance and documentation migration tests.
- DSC2 parser and extension schema approval.
- Capability discovery and negotiation approval.
- Deterministic footprint normalizer approval.
- Negative tests for unsupported or unknown capabilities.

## Compatibility Impact

DSC1 compatibility is preserved by freezing DSC1 and continuing to reject reserved fields.

DSC2 is a new ABI surface. It is not backward-compatible by accident; compatibility must be explicit through capability discovery, versioning, and parser behavior.

Parser-only DSC2 support may be compatible with the current fail-closed baseline if it produces no memory effects, does not allocate tokens through pipeline issue, and does not enable compiler production lowering.

## Implementation Phases Enabled Only After Approval

Approval of this ADR would allow follow-on documentation and parser-only design work only:

- define a DSC2 header schema;
- define extension block layout and compatibility rules;
- define capability ids and negotiation states;
- define parser-only validation results;
- define exact and conservative footprint normalization rules;
- define maximum range, table, and normalized footprint limits;
- define address-space extension interaction with Phase 06;
- define negative tests for malformed, unknown, unsupported, and execution-disabled descriptors;
- define documentation migration criteria for parser-only versus executable status.

Executable use remains blocked until the dependent gates are complete.

## Required Tests Before Any Executable Claim

- DSC1 valid descriptors remain accepted exactly as before.
- DSC1 non-zero reserved fields still reject.
- DSC1 unknown ABI versions reject.
- DSC1 does not accept stride, tile, scatter-gather, address-space, or partial-success encodings.
- DSC2 malformed header rejects.
- DSC2 bad length, offset, count, alignment, or checksum rejects.
- Unknown required extension rejects.
- Unknown optional semantic extension rejects.
- Known parser-only extension validates without enabling execution.
- Capability absent rejects the dependent extension.
- Capability present for parser-only mode does not enable token issue or memory effects.
- Strided footprint normalization is deterministic and overflow-safe.
- Tile/2D footprint normalization is deterministic and overflow-safe.
- Scatter-gather footprint normalization enforces segment limits and table bounds.
- Conservative footprints over-approximate and never under-approximate.
- Alias, alignment, domain, device, and address-space mismatches reject.
- Compiler/backend tests refuse production lowering when execution gate is closed.

## Documentation Migration Rule

Documentation must keep three states separate:

- Current implemented DSC1 contract.
- Current implemented parser-only DSC2/capability model.
- Executable DSC2 architecture after all dependent gates and tests.

Current-contract documents may state that DSC1 is implemented, strict, and fail-closed for execution. They must not state that DSC2 is executable, that parser-only capability implies execution, or that compiler/backend may production-lower DSC2.

Future-design documents may describe executable DSC2, issue/admission, backend selection, ordering, cache behavior, partial success, and compiler lowering only as gated work. Executable claims may move to Current Implemented Contract only after code, tests, and documentation migration pass Phase 12.

## Code Evidence

- `HybridCPU_ISE\Core\Execution\DmaStreamCompute\DmaStreamComputeDescriptor.cs`
  - Defines current operations: `Copy`, `Add`, `Mul`, `Fma`, `Reduce`.
  - Defines current element types: `UInt8`, `UInt16`, `UInt32`, `UInt64`, `Float32`, `Float64`.
  - Defines current shapes: `Contiguous1D`, `FixedReduce`.
  - Defines only `InlineContiguous` range encoding.
  - Defines only `AllOrNone` partial completion.
  - Carries normalized read/write ranges and a normalized footprint hash.
- `HybridCPU_ISE\Core\Execution\DmaStreamCompute\DmaStreamComputeDescriptorParser.cs`
  - Uses `Magic = 0x31435344`, `CurrentAbiVersion = 1`, and `CurrentHeaderSize = 128`.
  - `ExecutionEnabled => false`.
  - Rejects unsupported ABI versions.
  - Rejects dirty flags and reserved fields.
  - Accepts only `InlineContiguous` range encoding.
  - Accepts only `AllOrNone` partial completion.
  - Validates range alignment, zero length, overflow, aliasing, and normalized footprints for current forms.
  - Rejects lane carrier reserved bits as an unversioned descriptor ABI carrier.
- `HybridCPU_ISE\Core\Execution\DmaStreamCompute\DmaStreamComputeDescriptorParser.Dsc2.cs`
  - Defines DSC2 parser-only constants: `Dsc2Magic`, ABI 2.0, `Dsc2HeaderSize = 128`, and 32-byte extension block headers.
  - Defines parser/validation/footprint/backend-address-space/execution/compiler-lowering capability stages while keeping descriptor execution disabled.
  - Validates extension type, version, length, alignment, required/semantic/non-semantic compatibility, capability id, minimum parser major, and deterministic checksum/reserved-field rules.
  - Parses `AddressSpace`, `StridedRange`, `Tile2D`, `ScatterGather`, `CapabilityProfile`, `FootprintSummary`, and non-semantic metadata as parser-only extension families.
  - Produces deterministic normalized footprints as exact or bounded conservative ranges, and rejects overflow, zero length, misalignment, malformed segment tables, missing capabilities, domain/device/address-space mismatches, and under-approximated summaries.
- `HybridCPU_ISE\Core\Execution\DmaStreamCompute\DmaStreamComputeValidationResult.cs`
  - Provides explicit rejection surfaces such as `UnsupportedAbiVersion`, `UnsupportedOperation`, `UnsupportedElementType`, `UnsupportedShape`, `ReservedFieldFault`, `RangeOverflow`, `AlignmentFault`, `ZeroLengthFault`, `AliasOverlapFault`, `OwnerDomainFault`, and `ExecutionDisabled`.
  - Adds DSC2 parser-only rejection surfaces for unsupported capabilities, malformed extensions, footprint normalization faults, address-space faults, and under-approximated footprints.
- `HybridCPU_ISE\Core\Pipeline\MicroOps\DmaStreamComputeMicroOp.cs`
  - Carries normalized read/write ranges and resource masks.
  - `Execute` remains fail-closed and states that no StreamEngine or DMAController fallback is implied.
- `HybridCPU_ISE\Core\Execution\ExternalAccelerators\Capabilities\*`
  - Provides metadata-only L7 capability patterns.
  - These surfaces may inform future capability style, but they are not DSC2 parser, execution, or compiler-lowering authority.
- `HybridCPU_ISE.Tests\tests\DmaStreamComputeDsc2Phase07Tests.cs`
  - Locks DSC1 compatibility and reserved-field rejection.
  - Covers malformed DSC2 headers/extension tables, unknown extension compatibility, missing capabilities, address-space binding mismatches, deterministic exact/conservative/reject footprint outcomes, fail-closed DSC/L7 execution boundaries, compiler lowering guards, and Phase06 resolver non-wiring.

## Strict Prohibitions

This ADR must not be used to claim:

- lane6 DSC is executable;
- executable/full DSC2 is currently implemented;
- parser-only DSC2 support is executable DSC;
- DSC1 supports stride, tile, scatter-gather, address-space selection, or partial success;
- DSC1 reserved fields may carry v2 behavior;
- capability metadata authorizes production execution;
- compiler/backend may production-lower to DSC2;
- IOMMU-translated DSC addresses are current behavior;
- cache/prefetch surfaces provide coherent DMA/cache behavior;
- partial completion is a successful architectural mode.
