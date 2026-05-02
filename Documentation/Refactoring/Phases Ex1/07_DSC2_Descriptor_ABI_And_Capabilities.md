# Phase 07 - DSC2 Descriptor ABI And Capabilities

Status:
Parser-only foundation implemented. Executable use remains gated and is implementation-ready only after the dependent execution gates.

Scope:
Cover TASK-006: preserve DSC1 and define the future DSC2 or capability-gated extension mechanism for stride, tile, scatter-gather, address-space selection, and normalized footprints.

Current code evidence:
- `DmaStreamComputeDescriptorParser` accepts DSC1 ABI version 1 and rejects reserved fields.
- DSC1 range encoding is `InlineContiguous`.
- DSC1 partial completion policy is `AllOrNone`.
- Supported DSC1 shapes and operations are fixed by the current parser/model.
- `DmaStreamComputeMicroOp` uses normalized read/write range evidence, but executable use is fail-closed.
- `DmaStreamComputeDescriptorParser.Dsc2` defines a distinct DSC2 parser-only ABI surface with `DSC2` magic, ABI 2.0, fixed header size 128, typed extension blocks, explicit capability stages, and execution-disabled descriptor results.
- DSC2 parser-only footprint normalization supports strided range, tile/2D, scatter-gather, address-space, capability profile, footprint summary, and non-semantic metadata extension families as validation/model surfaces only.

Architecture decision:
Current contract:
- DSC1 is immutable.
- DSC1 reserved fields are not available for opportunistic v2 behavior.
- DSC1 does not support stride, tile, scatter-gather, successful partial completion, or executable IOMMU address-space semantics.
- DSC2 parser-only acceptance does not authorize issue, token allocation, runtime execution, memory publication, compiler lowering, or L7 execution.

Future gated:
- New capabilities must use DSC2 or explicit capability-gated extension blocks.
- Executable use of DSC2 is blocked by executable lane6 DSC, token lifecycle, ordering/conflict, backend/addressing, cache protocol, and compiler contracts.

Non-goals:
- Do not reinterpret DSC1 reserved fields.
- Do not make DSC2 executable by parser availability alone.
- Do not grant compiler/backend production lowering to new shapes before execution semantics exist.
- Do not define partial success here; phase 08 and a separate ADR govern that.

Required design gates:
- DSC2 magic/version/header and compatibility policy.
- Extension block format: length, type, version, required/optional bit, checksum or validation rules if needed.
- Capability discovery and negotiation path.
- Address-space extension integration with phase 06.
- Shape extensions: stride, tile, 2D, scatter-gather.
- Normalized footprint generation for all new forms.
- Overflow, alignment, alias, range-count, and domain checks.
- Parser-only status and feature flags.

Implementation plan:
1. Done: define DSC2 extension-block schema without modifying DSC1.
2. Done: add parser-only tests that reject unknown required/semantic capabilities and accept known non-executable descriptors only as parser-only/model-only results.
3. Done: add a normalized footprint builder that turns stride/tile/scatter-gather into exact read/write ranges or bounded conservative footprints, rejecting unsafe or under-approximated cases.
4. Done: add capability negotiation separate from execution enablement.
5. Still gated: keep execution disabled until phases 02 through 06, 08, 09, 11, and 12 approve executable use.

Affected files/classes/methods:
- `DmaStreamComputeDescriptor`
- `DmaStreamComputeDescriptorParser`
- `DmaStreamComputeValidationResult`
- `DmaStreamComputeDescriptorParser.Dsc2`
- DSC2 parser-only descriptor/result/capability/extension/footprint model types
- compiler/backend descriptor emission paths

Testing requirements:
- DSC1 compatibility acceptance remains unchanged.
- DSC1 rejects non-zero reserved fields and v2 encodings.
- DSC2 parser rejects malformed headers, overflow, bad alignment, unknown required extensions, and unsupported capabilities.
- DSC2 parser accepts parser-only known capabilities without implying execution.
- Footprint normalizer produces deterministic exact or conservative footprints.
- Compiler/backend tests refuse production lowering when capability is absent or execution gate is closed.

Documentation updates:
DSC1 is documented as immutable. DSC2/capability extensions are Current Implemented Contract only as parser-only/model-only behavior. Executable DSC2 remains Future gated.

Compiler/backend impact:
Current backend is forbidden from assuming stride/tile/scatter-gather in DSC1. Future backend may emit DSC2 only after capability discovery and conformance tests. Parser-only capability does not authorize executable lowering.

Compatibility risks:
Breaking DSC1 would invalidate existing descriptors. Conservative footprints can reduce parallelism but are safe; under-approximated footprints are unsafe and must be rejected.

Exit criteria:
- DSC1 immutability is locked.
- DSC2/extension schema is specified.
- Capability and footprint rules are specified.
- Executable use remains blocked until the executable gates are satisfied.
- Phase07 parser-only tests cover DSC1 compatibility, malformed DSC2 rejection, capability gates, footprint normalization, fail-closed execution boundaries, compiler lowering guards, and Phase06 non-wiring.

Blocked by:
No blocker for design. Executable use is blocked by phases 02, 03, 04, 05, 06, 08, 09, 11, and 12.

Enables:
Future stride/tile/scatter-gather, explicit address-space descriptors, capability-gated compiler lowering, and richer memory-footprint analysis.
