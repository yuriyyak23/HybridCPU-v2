using HybridCPU_ISE.Arch;

using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// MicroOp classification for resource quota tracking
    /// </summary>
    public enum MicroOpClass
    {
        /// <summary>
        /// ALU operation (arithmetic, logical, shift, etc.)
        /// </summary>
        Alu,

        /// <summary>
        /// LSU operation (load, store, atomic)
        /// </summary>
        Lsu,

        /// <summary>
        /// DMA operation (block transfer)
        /// </summary>
        Dma,

        /// <summary>
        /// Vector/Stream operation
        /// </summary>
        Vector,

        /// <summary>
        /// Control flow operation
        /// </summary>
        Control,

        /// <summary>
        /// Other/unclassified operation
        /// </summary>
        Other
    }

    /// <summary>
    /// Declares which layer is authoritative for canonical decode-fact publication.
    /// <see cref="Unspecified"/> is fail-closed: decode/projector paths must reject
    /// materialized MicroOps that never made an explicit publication decision.
    /// </summary>
    public enum CanonicalDecodePublicationMode
    {
        /// <summary>
        /// No explicit canonical decode-publication policy has been declared.
        /// This is illegal on decoder/projector materialization paths.
        /// </summary>
        Unspecified = 0,

        /// <summary>
        /// Canonical decode facts must be projected by the decode-to-runtime projector.
        /// The materialized MicroOp may still publish execution-specific metadata.
        /// </summary>
        ProjectorPublishes = 1,

        /// <summary>
        /// The MicroOp family/factory already publishes authoritative canonical decode facts.
        /// The projector must not overlay a second manual compatibility projection.
        /// </summary>
        SelfPublishes = 2,
    }

    /// <summary>
    /// Helper class for building Global Resource Lock Bitset (GRLB) masks - Phase 8.
    /// Provides static methods to construct resource masks for different operation types.
    /// Now supports both 64-bit (legacy) and 128-bit (extended) safety masks.
    /// </summary>
    public static class ResourceMaskBuilder
    {
        // Resource bit layout constants (Low 64 bits)
        private const int REG_READ_BASE = 0;      // Bits 0-15: Register read groups
        private const int REG_WRITE_BASE = 16;    // Bits 16-31: Register write groups
        private const int MEM_DOMAIN_BASE = 32;   // Bits 32-47: Memory domain IDs
        private const int LSU_LOAD_BIT = 48;      // Bit 48: Load operation
        private const int LSU_STORE_BIT = 49;     // Bit 49: Store operation
        private const int LSU_ATOMIC_BIT = 50;    // Bit 50: Atomic operation
        private const int DMA_CHANNEL_BASE = 51;  // Bits 51-54: DMA channels (4 channels)
        private const int STREAM_ENGINE_BASE = 55;// Bits 55-58: Stream engines (4 engines)
        private const int ACCEL_BASE = 59;        // Bits 59-62: Custom accelerators (4 accelerators)

        // Extended resource bit layout constants (High 64 bits, bits 64-127)
        private const int EXT_GRLB_BASE = 0;      // High bits 0-31: Extended GRLB channels
        private const int EXT_MEM_DOMAIN_BASE = 32; // High bits 32-47: Extended memory domains
        private const int EXT_MEM_BANK_BASE = 48;   // High bits 48-63: Memory Banks occupancy (Phase Refactoring Pt. 1)

        /// <summary>
        /// Build resource mask for memory bank occupancy.
        /// </summary>
        public static ResourceBitset ForMemoryBank(int bankId)
        {
            if (bankId < 0 || bankId >= 16) bankId = 0;
            return new ResourceBitset(0, 1UL << (EXT_MEM_BANK_BASE + bankId));
        }

        /// <summary>
        /// Build resource mask for register read operations with virtual-thread isolation.
        ///
        /// Group layout (per-VT bank): <c>group = vtId * 4 + regId / 8</c>.
        /// With 4 VTs (0–3) and 4 groups per VT this maps onto the same 16-group
        /// window as the non-VT overload but keeps each thread's register traffic
        /// in a distinct bank slot — preventing false resource conflicts across VTs.
        ///
        /// Blueprint §1 / §15: "Ввести банки: ForRegisterRead(regId, vtId)".
        /// </summary>
        public static ResourceBitset ForRegisterRead(int regId, int vtId)
        {
            if (vtId < 0 || vtId >= Processor.CPU_Core.SmtWays) vtId = 0;
            int group = (vtId * 4) + (regId / 8);
            if (group >= 16) group = 15;
            return new ResourceBitset(1UL << (REG_READ_BASE + group), 0);
        }

        /// <summary>
        /// Build resource mask for register write operations with virtual-thread isolation.
        ///
        /// Group layout (per-VT bank): <c>group = vtId * 4 + regId / 8</c>.
        /// Mirrors <see cref="ForRegisterRead(int, int)"/> for the write path.
        ///
        /// Blueprint §1 / §15: "Ввести банки: ForRegisterWrite(regId, vtId)".
        /// </summary>
        public static ResourceBitset ForRegisterWrite(int regId, int vtId)
        {
            if (vtId < 0 || vtId >= Processor.CPU_Core.SmtWays) vtId = 0;
            int group = (vtId * 4) + (regId / 8);
            if (group >= 16) group = 15;
            return new ResourceBitset(1UL << (REG_WRITE_BASE + group), 0);
        }


        /// <summary>
        /// Build resource mask for register read operations.
        /// Maps register ID to group ID (4 registers per group).
        /// Returns a ResourceBitset with the appropriate bit set in the Low component.
        /// </summary>
        public static ResourceBitset ForRegisterRead(int regId)
        {
            int group = regId / 4;
            if (group >= 16) group = 15; // Clamp to 16 groups
            return new ResourceBitset(1UL << (REG_READ_BASE + group), 0);
        }

        /// <summary>
        /// Build resource mask for register write operations.
        /// Maps register ID to group ID (4 registers per group).
        /// Returns a ResourceBitset with the appropriate bit set in the Low component.
        /// </summary>
        public static ResourceBitset ForRegisterWrite(int regId)
        {
            int group = regId / 4;
            if (group >= 16) group = 15; // Clamp to 16 groups
            return new ResourceBitset(1UL << (REG_WRITE_BASE + group), 0);
        }

        /// <summary>
        /// Build resource mask for memory domain access.
        /// </summary>
        public static ResourceBitset ForMemoryDomain(int domainId)
        {
            if (domainId < 0 || domainId >= 16) domainId = 0;
            return new ResourceBitset(1UL << (MEM_DOMAIN_BASE + domainId), 0);
        }

        /// <summary>
        /// Build resource mask for load operation (LSU read channel).
        /// </summary>
        public static ResourceBitset ForLoad()
        {
            return new ResourceBitset(1UL << LSU_LOAD_BIT, 0);
        }

        /// <summary>
        /// Build resource mask for store operation (LSU write channel).
        /// </summary>
        public static ResourceBitset ForStore()
        {
            return new ResourceBitset(1UL << LSU_STORE_BIT, 0);
        }

        /// <summary>
        /// Build resource mask for atomic operation (LSU atomic channel).
        /// </summary>
        public static ResourceBitset ForAtomic()
        {
            return new ResourceBitset(1UL << LSU_ATOMIC_BIT, 0);
        }

        /// <summary>
        /// Build resource mask for DMA channel.
        /// </summary>
        public static ResourceBitset ForDMAChannel(int channelId)
        {
            if (channelId < 0 || channelId >= 4) channelId = 0;
            return new ResourceBitset(1UL << (DMA_CHANNEL_BASE + channelId), 0);
        }

        /// <summary>
        /// Build resource mask for stream engine.
        /// </summary>
        public static ResourceBitset ForStreamEngine(int engineId)
        {
            if (engineId < 0 || engineId >= 4) engineId = 0;
            return new ResourceBitset(1UL << (STREAM_ENGINE_BASE + engineId), 0);
        }

        /// <summary>
        /// Build resource mask for custom accelerator.
        /// </summary>
        public static ResourceBitset ForAccelerator(int accelId)
        {
            if (accelId < 0 || accelId >= 4) accelId = 0;
            return new ResourceBitset(1UL << (ACCEL_BASE + accelId), 0);
        }

        // ===== 128-bit Safety Mask Builders (Phase: Safety Tags & Certificates) =====

        /// <summary>
        /// Build 128-bit safety mask for register read operations.
        /// </summary>
        public static SafetyMask128 ForRegisterRead128(int regId)
        {
            var res = ForRegisterRead(regId);
            return new SafetyMask128(res.Low, res.High);
        }

        /// <summary>
        /// Build 128-bit safety mask for register write operations.
        /// </summary>
        public static SafetyMask128 ForRegisterWrite128(int regId)
        {
            var res = ForRegisterWrite(regId);
            return new SafetyMask128(res.Low, res.High);
        }

        /// <summary>
        /// Build 128-bit safety mask for memory domain access.
        /// </summary>
        public static SafetyMask128 ForMemoryDomain128(int domainId)
        {
            var res = ForMemoryDomain(domainId);
            return new SafetyMask128(res.Low, res.High);
        }

        /// <summary>
        /// Build 128-bit safety mask for memory bank occupancy (Refactoring: Hardware MSHR/Banks).
        /// </summary>
        public static SafetyMask128 ForMemoryBank128(int bankId)
        {
            var res = ForMemoryBank(bankId);
            return new SafetyMask128(res.Low, res.High);
        }

        /// <summary>
        /// Build 128-bit safety mask for load operation.
        /// </summary>
        public static SafetyMask128 ForLoad128()
        {
            var res = ForLoad();
            return new SafetyMask128(res.Low, res.High);
        }

        /// <summary>
        /// Build 128-bit safety mask for store operation.
        /// </summary>
        public static SafetyMask128 ForStore128()
        {
            var res = ForStore();
            return new SafetyMask128(res.Low, res.High);
        }

        /// <summary>
        /// Build 128-bit safety mask for atomic operation.
        /// </summary>
        public static SafetyMask128 ForAtomic128()
        {
            var res = ForAtomic();
            return new SafetyMask128(res.Low, res.High);
        }

        /// <summary>
        /// Build 128-bit safety mask for DMA channel.
        /// </summary>
        public static SafetyMask128 ForDMAChannel128(int channelId)
        {
            var res = ForDMAChannel(channelId);
            return new SafetyMask128(res.Low, res.High);
        }

        /// <summary>
        /// Build 128-bit safety mask for stream engine.
        /// </summary>
        public static SafetyMask128 ForStreamEngine128(int engineId)
        {
            var res = ForStreamEngine(engineId);
            return new SafetyMask128(res.Low, res.High);
        }

        /// <summary>
        /// Build 128-bit safety mask for custom accelerator.
        /// </summary>
        public static SafetyMask128 ForAccelerator128(int accelId)
        {
            var res = ForAccelerator(accelId);
            return new SafetyMask128(res.Low, res.High);
        }

        /// <summary>
        /// Build 128-bit safety mask for extended GRLB channel (bits 64-95).
        /// Supports up to 32 additional channels for future hardware expansions.
        /// </summary>
        public static SafetyMask128 ForExtendedGRLBChannel(int channelId)
        {
            if (channelId < 0 || channelId >= 32) channelId = 0;
            return new SafetyMask128(0, 1UL << (EXT_GRLB_BASE + channelId));
        }

        /// <summary>
        /// Build 128-bit safety mask for extended memory domain (bits 96-111).
        /// Supports 16 additional memory domains beyond the base 16.
        /// </summary>
        public static SafetyMask128 ForExtendedMemoryDomain(int domainId)
        {
            if (domainId < 0 || domainId >= 16) domainId = 0;
            return new SafetyMask128(0, 1UL << (EXT_MEM_DOMAIN_BASE + domainId));
        }
    }

    /// </summary>
    public abstract class MicroOp
    {
        private MicroOpAdmissionMetadata _admissionMetadata;
        private bool _hasAdmissionMetadata;
        private bool _isStealable = true;

        protected static void RefreshAdmissionMetadata(MicroOp microOp)
        {
            ArgumentNullException.ThrowIfNull(microOp);
            microOp._admissionMetadata = MicroOpAdmissionMetadata.Create(microOp, microOp.SafetyMask);
            microOp._hasAdmissionMetadata = true;
        }

        /// <summary>
        /// Opcode of the original instruction
        /// </summary>
        public uint OpCode { get; set; }

        /// <summary>
        /// Predicate mask for conditional execution (0 = all lanes active)
        /// </summary>
        public ushort PredicateMask { get; set; }

        /// <summary>
        /// Destination register ID (if applicable)
        /// </summary>
        public ushort DestRegID { get; set; }

        /// <summary>
        /// Does this MicroOp write to a register?
        /// </summary>
        public bool WritesRegister { get; set; }

        /// <summary>
        /// Execution latency in cycles (minimum, may vary by operands)
        /// </summary>
        public byte Latency { get; set; }

        /// <summary>
        /// Is this a memory operation?
        /// </summary>
        public bool IsMemoryOp { get; set; }

        /// <summary>
        /// Is this a control flow operation?
        /// </summary>
        public bool IsControlFlow { get; set; }

        // ===== ISA v4 Classification (Phase 02) =====

        /// <summary>
        /// Canonical ISA v4 instruction class for this micro-operation.
        /// Determines pipeline routing, slot class assignment, and serialization behavior.
        /// Populated from <see cref="Arch.InstructionClassifier"/> during construction.
        /// </summary>
        public virtual Arch.InstructionClass InstructionClass { get; protected set; } = Arch.InstructionClass.ScalarAlu;

        /// <summary>
        /// Canonical ISA v4 serialization class for this micro-operation.
        /// Determines ordering and side-effect isolation requirements in the pipeline.
        /// Populated from <see cref="Arch.InstructionClassifier"/> during construction.
        /// </summary>
        public virtual Arch.SerializationClass SerializationClass { get; protected set; } = Arch.SerializationClass.Free;

        /// <summary>
        /// Declares whether canonical decode facts are published by the materialized
        /// MicroOp itself or must be projected by the decode-to-runtime transport layer.
        /// New MicroOp families must override this explicitly; <see cref="Unspecified"/>
        /// is a fail-closed contract rather than a permissive default.
        /// </summary>
        public virtual CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.Unspecified;

        /// <summary>
        /// Explicit compatibility/test opt-in for the retired structural fallback surface.
        /// Canonical runtime micro-ops must publish authoritative structural safety masks
        /// and leave this disabled.
        /// </summary>
        protected internal virtual bool AllowsStructuralSafetyFallback => false;

        // ===== Legacy FSP Compatibility Metadata =====
        // These properties retain compatibility with older slot-admission terminology
        // while the canonical runtime story speaks in typed-slot bundle densification,
        // donor provenance, and assist legality.

        /// <summary>
        /// Explicit producer-side stealability fact for this materialized micro-op.
        /// Runtime admission and legacy FSP-compatibility candidate checks must
        /// consume the cached <see cref="AdmissionMetadata"/> snapshot instead of
        /// recovering stealability from legacy per-slot shells.
        /// </summary>
        public bool IsStealable
        {
            get => _isStealable;
            set
            {
                _isStealable = value;
                _hasAdmissionMetadata = false;
            }
        }

        /// <summary>
        /// Explicit producer-side locality hint carried on the materialized micro-op.
        /// Runtime assist-seed shaping must consume this field directly instead of
        /// recovering locality policy from detached slot metadata containers.
        /// </summary>
        public LocalityHint MemoryLocalityHint { get; set; } = LocalityHint.None;

        /// <summary>
        /// Snapshot of correctness-relevant admission facts extracted from the producer-side
        /// micro-op state. This is the authoritative legality input for decoded-slot and
        /// certificate consumers; <see cref="SafetyMask"/> remains only a structural primitive.
        /// </summary>
        public MicroOpAdmissionMetadata AdmissionMetadata =>
            _hasAdmissionMetadata
                ? _admissionMetadata
                : MicroOpAdmissionMetadata.Create(this, SafetyMask);

        /// <summary>
        /// Re-synchronises the cached producer-side admission snapshot after the micro-op's
        /// scheduling or hazard-relevant fields are updated.
        /// </summary>
        public void RefreshAdmissionMetadata()
        {
            RefreshAdmissionMetadata(this);
        }

        /// <summary>
        /// Publishes the explicit structural safety mask for the micro-op's current
        /// producer-side resource, placement, and serialization shape.
        /// Canonical runtime producers must call this before refreshing admission
        /// metadata on high-risk contours.
        /// </summary>
        protected void PublishExplicitStructuralSafetyMask()
        {
            SafetyMask = InstructionRegistry.BuildExplicitStructuralSafetyMask(this);
        }

        /// <summary>
        /// Retained compatibility flag for a micro-operation injected by typed-slot
        /// bundle densification / slot admission rather than by its owning VT in
        /// normal program order.
        /// Used at ID stage to distinguish silent speculative squash from
        /// architectural domain fault (precise exception). See §5.4.
        /// </summary>
        public bool IsFspInjected { get; set; } = false;

        /// <summary>
        /// True only for the post-phase-08 assist plane.
        /// Assist micro-ops are architecturally invisible, retire-invisible,
        /// replay-discardable, and non-retiring.
        /// They remain boundedly observable through carrier-memory effects plus
        /// replay and telemetry evidence.
        /// </summary>
        public virtual bool IsAssist => false;

        /// <summary>
        /// True when this micro-op participates in architectural retire truth.
        /// Assist micro-ops override this to keep retire authority explicit.
        /// </summary>
        public virtual bool IsRetireVisible => true;

        /// <summary>
        /// True when this micro-op may be discarded across replay / trap / fence / VM boundaries
        /// without changing ISA-visible state.
        /// </summary>
        public virtual bool IsReplayDiscardable => false;

        /// <summary>
        /// True when runtime faults must be suppressed and converted into a deterministic kill.
        /// Used by the assist plane to prevent ISA-visible traps or results.
        /// </summary>
        public virtual bool SuppressesArchitecturalFaults => false;

        /// <summary>
        /// Register IDs from which this micro-operation reads.
        /// Used by scheduler to detect RAW/WAW hazards.
        /// </summary>
        public virtual IReadOnlyList<int> ReadRegisters { get; protected set; } = Array.Empty<int>();

        /// <summary>
        /// Register IDs to which this micro-operation writes.
        /// Used by scheduler to detect WAW/WAR hazards.
        /// </summary>
        public virtual IReadOnlyList<int> WriteRegisters { get; protected set; } = Array.Empty<int>();

        /// <summary>
        /// Memory address ranges from which this operation reads.
        /// Format: (Address, Length) tuples after IOMMU translation.
        /// Populated during construction or in Execute stage for stream operations.
        /// </summary>
        public virtual IReadOnlyList<(ulong Address, ulong Length)> ReadMemoryRanges { get; protected set; } = Array.Empty<(ulong, ulong)>();

        /// <summary>
        /// Memory address ranges to which this operation writes.
        /// Format: (Address, Length) tuples after IOMMU translation.
        /// Populated during construction or in Execute stage.
        /// </summary>
        public virtual IReadOnlyList<(ulong Address, ulong Length)> WriteMemoryRanges { get; protected set; } = Array.Empty<(ulong, ulong)>();

        /// <summary>
        /// Owner thread/process ID for this micro-operation.
        /// Used during Commit stage to ensure correct register/memory updates.
        /// Set by decoder when creating the MicroOp.
        /// </summary>
        public int OwnerThreadId { get; set; } = 0;

        /// <summary>
        /// Architectural owner context identifier — the privilege domain (OS task, VM guest, hypervisor)
        /// that architecturally owns this micro-operation.
        /// <para>
        /// Distinct from <see cref="VirtualThreadId"/>, which is the SMT hardware-thread slot (0–3).
        /// A single architectural context may execute on any VT slot via FSP slot-stealing; conversely,
        /// a single VT slot may host multiple guest contexts across VM transitions.
        /// </para>
        /// Set independently by decoder; must NOT default to VirtualThreadId.
        /// Cross-domain injection is forbidden at serialising event boundaries.
        /// </summary>
        public int OwnerContextId { get; set; } = 0;

        /// <summary>
        /// Virtual thread ID for intra-core 4-way SMT.
        /// Identifies which hardware thread context (0–3) within a physical core
        /// owns this micro-operation. Used by BundleResourceCertificate4Way to
        /// isolate register-group conflict checks per virtual thread while sharing
        /// common resource checks (memory domains, DMA, LSU, Accel).
        /// Set by decoder based on the active hardware thread context.
        /// </summary>
        public int VirtualThreadId { get; set; } = 0;

        /// <summary>
        /// Does this operation have side effects that require rollback support?
        /// True for: memory writes, CSR writes, I/O operations
        /// False for: pure ALU operations, loads (non-destructive)
        /// </summary>
        public virtual bool HasSideEffects { get; protected set; } = false;

        /// <summary>
        /// Safety mask for parallel execution checking (Phase 6: Nomination Ports).
        /// Used by scheduler for fast parallel candidate selection.
        /// Bit mask encoding resource conflicts (e.g., execution units, memory banks).
        ///
        /// Bit layout:
        /// - Bits 0-15:  Register read groups (16 groups of 4 registers each)
        /// - Bits 16-31: Register write groups (16 groups of 4 registers each)
        /// - Bits 32-47: Memory domain IDs (16 possible domains)
        /// - Bits 48-50: LSU class (Load=bit48, Store=bit49, Atomic=bit50)
        /// - Bits 51-63: Reserved for future use
        /// </summary>
        public virtual SafetyMask128 SafetyMask { get; set; } = new SafetyMask128(0, 0);

        /// <summary>
        /// Architectural legality mask — ISA-level RAW/WAW/WAR hazards.
        /// Blueprint §7: "Развести ArchLegalityMask и BackendHazardMask."
        /// </summary>
        public ArchLegalityMask ArchLegality { get; set; } = ArchLegalityMask.Zero;

        /// <summary>
        /// Backend hazard mask — physical register bank and execution-unit contention.
        /// Blueprint §7: "Развести ArchLegalityMask и BackendHazardMask."
        /// </summary>
        public BackendHazardMask BackendHazard { get; set; } = BackendHazardMask.Zero;

        /// <summary>
        /// Queue index for ordering micro-operations with equal priority (Phase 6: Nomination Ports).
        /// Used for tie-breaking during candidate selection.
        /// Lower values indicate higher priority.
        /// </summary>
        public int QueueIndex { get; set; } = 0;

        /// <summary>
        /// MicroOp classification for resource quota tracking.
        /// Used by scheduler to enforce per-bundle resource limits (e.g., max 4 ALU, 2 LSU, 1 DMA).
        /// </summary>
        public virtual MicroOpClass Class { get; protected set; } = MicroOpClass.Other;

        // ===== Typed-Slot Taxonomy (Phase 01) =====

        /// <summary>
        /// Unified slot placement metadata for this micro-operation.
        /// Blueprint §8 / Checklist P2: canonical placement/admission authority.
        /// Slot class, pinning, and domain ownership now flow exclusively through this struct.
        /// </summary>
        public SlotPlacementMetadata Placement { get; set; } = SlotPlacementMetadata.Default;

        internal void ApplyCanonicalDecodeProjection(
            Arch.InstructionClass instructionClass,
            Arch.SerializationClass serializationClass,
            SlotPlacementMetadata placement,
            bool isMemoryOp,
            bool isControlFlow,
            bool writesRegister,
            IReadOnlyList<int> readRegisters,
            IReadOnlyList<int> writeRegisters)
        {
            InstructionClass = instructionClass;
            SerializationClass = serializationClass;
            Placement = placement;
            IsMemoryOp = isMemoryOp;
            IsControlFlow = isControlFlow;
            WritesRegister = writesRegister;
            ReadRegisters = readRegisters ?? Array.Empty<int>();
            WriteRegisters = writeRegisters ?? Array.Empty<int>();
            _hasAdmissionMetadata = false;
        }

        protected void SetPlacement(
            SlotClass requiredSlotClass,
            SlotPinningKind pinningKind,
            byte pinnedLaneId = 0)
        {
            Placement = new SlotPlacementMetadata
            {
                RequiredSlotClass = requiredSlotClass,
                PinningKind = pinningKind,
                PinnedLaneId = pinningKind == SlotPinningKind.HardPinned ? pinnedLaneId : (byte)0,
                DomainTag = Placement.DomainTag
            };
        }

        protected void SetClassFlexiblePlacement(SlotClass requiredSlotClass)
        {
            SetPlacement(requiredSlotClass, SlotPinningKind.ClassFlexible);
        }

        protected void SetHardPinnedPlacement(SlotClass requiredSlotClass, byte pinnedLaneId)
        {
            SetPlacement(requiredSlotClass, SlotPinningKind.HardPinned, pinnedLaneId);
        }

        // ===== Global Resource Lock Bitset (GRLB) - Phase 8 =====

        /// <summary>
        /// Resource mask for Global Resource Lock Bitset (GRLB) - Phase 8 Extended.
        /// Each bit represents a resource required for execution:
        /// Low 64 bits (0-63):
        /// - Bits 0-15:  Register read groups (16 groups of 4 registers each)
        /// - Bits 16-31: Register write groups (16 groups of 4 registers each)
        /// - Bits 32-47: Memory domain IDs (16 possible domains)
        /// - Bits 48:    Load operation (LSU read channel)
        /// - Bits 49:    Store operation (LSU write channel)
        /// - Bits 50:    Atomic operation (LSU atomic channel)
        /// - Bits 51-54: DMA channels 0-3 (4 channels)
        /// - Bits 55-58: Stream engines 0-3 (4 engines)
        /// - Bits 59-62: Custom accelerators 0-3 (4 accelerators)
        /// - Bit 63:     Reserved
        ///
        /// High 64 bits (64-127):
        /// - Bits 64-67: DMA channels 4-7 (4 additional channels)
        /// - Bits 68-71: Stream engines 4-7 (4 additional engines)
        /// - Bits 72-75: Custom accelerators 4-7 (4 additional accelerators)
        /// - Bits 76-79: Additional LSU channels
        /// - Bits 80-95: Extended memory domains (16 additional domains)
        /// - Bits 96-127: Reserved for future resource types
        ///
        /// When this operation executes, these resources must be locked.
        /// If any resource conflicts with globalResourceLocks, operation must wait.
        /// </summary>
        public ResourceBitset ResourceMask { get; set; } = ResourceBitset.Zero;

        /// <summary>
        /// Token value assigned when resources were acquired (Phase 8 enhancement).
        /// Used to prevent ABA problem: ensures we only release resources that we actually own.
        /// Set by CPU_Core when AcquireResources succeeds.
        /// </summary>
        public ulong ResourceToken { get; set; } = 0;

        /// <summary>
        /// Original resource mask before speculative modification (Phase 7: Silent Squash).
        /// Used to restore the mask when a faulted operation is returned to owner queue.
        /// </summary>
        public ResourceBitset OriginalResourceMask { get; set; } = ResourceBitset.Zero;

        // ===== Singularity-Style Domain Isolation (Phase 03, tech.md §3) =====

        /// <summary>
        // ===== Scoreboard Pending Bit (Phase 06, STR.PULL DMA integration) =====

        /// <summary>
        /// Scoreboard pending flag: when true, this op's target register/domain is awaiting
        /// completion of a DMA (STR.PULL) transaction. SafetyVerifier blocks dependent
        /// instructions until this flag is cleared by the DMA completion event.
        /// </summary>
        public bool ScoreboardPending { get; set; } = false;

        // ===== Speculative FSP with Silent Squash (Phase 7) =====

        /// <summary>
        /// Indicates whether this operation is being executed speculatively
        /// (stolen from another thread and might fault without causing interrupts).
        /// </summary>
        public bool IsSpeculative { get; set; } = false;

        /// <summary>
        /// Indicates whether this speculative operation has faulted during execution.
        /// When true, the operation will not commit and will be returned to the owner's queue.
        /// </summary>
        public bool Faulted { get; set; } = false;

        // ===== Pipeline Event Generation (V6 Phase 3 — A5) =====

        /// <summary>
        /// Pipeline event generated during <see cref="Execute"/>.
        /// Non-null for system-event micro-operations (FENCE, ECALL, EBREAK, MRET, SRET, WFI).
        /// The pipeline FSM consumer reads this after Execute() and routes the event through
        /// <see cref="IPipelineEventQueue"/> in Phase 4 (D20/G33).
        /// </summary>
        /// <summary>
        /// Create a replay token capturing pre-execution state for rollback.
        /// Only called for operations with HasSideEffects = true.
        /// Derived classes should override to capture specific state.
        /// </summary>
        public virtual HybridCPU_ISE.Core.ReplayToken CreateRollbackToken(
            int ownerThreadId,
            Processor.MainMemoryArea? mainMemory = null)
        {
            return new HybridCPU_ISE.Core.ReplayToken(mainMemory)
            {
                OwnerThreadId = ownerThreadId,
                HasSideEffects = this.HasSideEffects,
                // Derived classes will capture additional state
            };
        }

        /// <summary>
        /// Execute this micro-operation.
        /// Returns true if execution completed successfully, false if needs to stall/retry.
        /// </summary>
        public abstract bool Execute(ref Processor.CPU_Core core);

        /// <summary>
        /// Commit results of this micro-operation (write-back stage).
        /// Called after Execute() succeeds and pipeline is ready to retire the instruction.
        /// Default is a no-op — production retire is driven by
        /// <see cref="EmitWriteBackRetireRecords"/> via the WB-local typed retire packet path.
        /// </summary>
        public virtual void Commit(ref Processor.CPU_Core core) { }

        /// <summary>
        /// Emits typed retire records for the WB-side authority path without directly mutating
        /// architectural state. Production writeback drains these records as one packet-local
        /// batch for the current live lane0..5 window.
        /// </summary>
        public virtual void EmitWriteBackRetireRecords(
            ref Processor.CPU_Core core,
            Span<RetireRecord> retireRecords,
            ref int retireRecordCount)
        {
        }

        /// <summary>
        /// Projects the primary scalar writeback value that should travel through EX/MEM/WB
        /// carrier state for forwarding and WB-side typed retirement.
        /// Returns <see langword="false"/> when this micro-op does not produce a scalar
        /// register value on the current path.
        /// </summary>
        public virtual bool TryGetPrimaryWriteBackResult(out ulong value)
        {
            value = 0;
            return false;
        }

        /// <summary>
        /// Captures the primary scalar value carried by the live WB lane.
        /// Used when the runtime authoritative value was produced by lane-local carrier state
        /// rather than by the micro-op's legacy internal field.
        /// </summary>
        public virtual void CapturePrimaryWriteBackResult(ulong value)
        {
        }

        /// <summary>
        /// Appends one WB retire record into the caller-provided packet-local buffer.
        /// </summary>
        protected static void AppendWriteBackRetireRecord(
            Span<RetireRecord> retireRecords,
            ref int retireRecordCount,
            in RetireRecord retireRecord)
        {
            if ((uint)retireRecordCount >= (uint)retireRecords.Length)
                throw new InvalidOperationException("WB retire record buffer exhausted.");

            retireRecords[retireRecordCount++] = retireRecord;
        }

        /// <summary>
        /// Get human-readable description of this MicroOp (for debugging/logging)
        /// </summary>
        public abstract string GetDescription();

        /// <summary>
        /// Re-synchronises <see cref="WriteRegisters"/> and <see cref="ResourceMask"/>
        /// after <see cref="WritesRegister"/> is promoted from <c>false</c> to <c>true</c>
        /// by a descriptor override in <see cref="InstructionRegistry.CreateMicroOp"/>.
        ///
        /// <para>
        /// Problem: subclasses that call their own <c>InitializeMetadata()</c> from
        /// inside the factory lambda do so before the descriptor is applied, so
        /// <see cref="WriteRegisters"/> is left empty when <see cref="WritesRegister"/>
        /// is still false.  Without this hook those subclasses would produce incorrect
        /// <c>WriteRegisters</c> lists, breaking RAW dependency detection in
        /// <see cref="Core.Decoder.DecodedBundleSlotDescriptor"/> and the
        /// <c>WideReadyScalarMask</c> computed by the issue-window scheduler.
        /// </para>
        ///
        /// Blueprint Phase 4 fix: override in every concrete class whose
        /// <c>InitializeMetadata()</c> depends on <see cref="WritesRegister"/>.
        /// </summary>
        public virtual void RefreshWriteMetadata() { }

        internal void ValidatePublishedWriteRegisterContract(string producerSurface)
        {
            ValidatePublishedWriteRegisterContract(
                producerSurface,
                AdmissionMetadata);
        }

        internal void ValidatePublishedWriteRegisterContract(
            string producerSurface,
            MicroOpAdmissionMetadata admissionMetadata)
        {
            IReadOnlyList<int> writeRegisters =
                admissionMetadata.WriteRegisters ?? Array.Empty<int>();

            if (admissionMetadata.WritesRegister)
            {
                if (writeRegisters.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"{producerSurface} observed {GetType().Name} (opcode 0x{OpCode:X}) with " +
                        "WritesRegister=true but an empty canonical WriteRegisters list. " +
                        "DestRegID is convenience only; decode/registration must publish authoritative " +
                        "write-register facts before runtime transport.");
                }

                if (HasPublishedDestinationRegister(DestRegID) &&
                    !ContainsPublishedRegister(writeRegisters, DestRegID))
                {
                    throw new InvalidOperationException(
                        $"{producerSurface} observed {GetType().Name} (opcode 0x{OpCode:X}) with " +
                        $"DestRegID={DestRegID} missing from canonical WriteRegisters [{string.Join(", ", writeRegisters)}]. " +
                        "DestRegID is convenience only; canonical dependency truth must live in WriteRegisters.");
                }

                return;
            }

            if (writeRegisters.Count > 0)
            {
                throw new InvalidOperationException(
                    $"{producerSurface} observed {GetType().Name} (opcode 0x{OpCode:X}) with " +
                    $"WritesRegister=false but canonical WriteRegisters [{string.Join(", ", writeRegisters)}].");
            }
        }

        private static bool HasPublishedDestinationRegister(ushort destinationRegisterId) =>
            destinationRegisterId != 0 &&
            destinationRegisterId != VLIW_Instruction.NoReg &&
            destinationRegisterId != ArchRegisterTripletEncoding.NoArchReg;

        private static bool ContainsPublishedRegister(
            IReadOnlyList<int> writeRegisters,
            ushort destinationRegisterId)
        {
            for (int i = 0; i < writeRegisters.Count; i++)
            {
                if (writeRegisters[i] == destinationRegisterId)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Normalizes an owner-thread identifier to the live SMT VT range.
        /// </summary>
        protected static int NormalizeExecutionVtId(int ownerThreadId) =>
            (uint)ownerThreadId < (uint)Processor.CPU_Core.SmtWays ? ownerThreadId : 0;

        /// <summary>
        /// Reads an architectural scalar value through the unified PRF/rename path.
        /// Returns <see langword="false"/> when the live core state is not initialized
        /// (for example in default-core tests) or when the architectural id is invalid.
        /// </summary>
        protected static bool TryReadUnifiedArchValue(
            ref Processor.CPU_Core core,
            int vtId,
            int archReg,
            out ulong value)
        {
            value = 0;

            if (archReg == 0)
                return true;

            if ((uint)archReg >= (uint)RenameMap.ArchRegs)
                return false;

            if (core.ArchRenameMap is null || core.PhysicalRegisters is null || core.ArchContexts is null)
                return false;

            value = core.ReadArch(vtId, archReg);
            return true;
        }

        /// <summary>
        /// Validates that a scalar source operand id is already a flat architectural
        /// register id in the canonical x0-x31 space.
        /// Legacy/global register encodings are intentionally rejected here so the
        /// execution fallback cannot reopen the removed compatibility path.
        /// </summary>
        protected static bool TryNormalizeFlatArchRegId(ushort rawRegId, out int archRegId)
        {
            archRegId = default;

            if (!ArchRegId.TryCreate(rawRegId, out ArchRegId regId))
                return false;

            archRegId = regId.Value;
            return true;
        }

        /// <summary>
        /// Reads a scalar source operand from the unified architectural-state model.
        /// Only canonical flat architectural register ids participate in this path.
        /// </summary>
        protected static ulong ReadUnifiedScalarSourceOperand(
            ref Processor.CPU_Core core,
            int vtId,
            ushort rawRegId)
        {
            if (rawRegId == VLIW_Instruction.NoReg)
                return 0;

            return TryNormalizeFlatArchRegId(rawRegId, out int archRegId) &&
                   TryReadUnifiedArchValue(ref core, vtId, archRegId, out ulong value)
                ? value
                : 0;
        }
    }
}
