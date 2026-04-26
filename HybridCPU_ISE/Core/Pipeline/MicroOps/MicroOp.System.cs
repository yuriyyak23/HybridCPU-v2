
using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;


namespace YAKSys_Hybrid_CPU.Core
{
    public enum SystemEventKind : byte
    {
        /// <summary>FENCE вЂ” data-memory ordering barrier.</summary>
        Fence,
        /// <summary>FENCE.I вЂ” instruction-cache flush and memory ordering barrier.</summary>
        FenceI,
        /// <summary>ECALL вЂ” environment call trap.</summary>
        Ecall,
        /// <summary>EBREAK вЂ” debug breakpoint trap.</summary>
        Ebreak,
        /// <summary>MRET вЂ” return from machine-mode trap handler.</summary>
        Mret,
        /// <summary>SRET вЂ” return from supervisor-mode trap handler.</summary>
        Sret,
        /// <summary>WFI вЂ” wait-for-interrupt low-power stall.</summary>
        Wfi,
        /// <summary>WFE РІР‚вЂќ wait-for-event stall owned by the pipeline FSM.</summary>
        Wfe,
        /// <summary>SEV РІР‚вЂќ wake event owned by the pipeline FSM.</summary>
        Sev,
        /// <summary>YIELD РІР‚вЂќ cooperative SMT/VT yield point.</summary>
        Yield,
        /// <summary>POD_BARRIER РІР‚вЂќ pod-scoped SMT/VT barrier.</summary>
        PodBarrier,
        /// <summary>VT_BARRIER РІР‚вЂќ VT-scoped barrier.</summary>
        VtBarrier,
    }

    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    // C17 (Checklist): SystemEventOrderGuarantee вЂ” drain/flush/order contract
    // в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    /// <summary>
    /// Describes the pipeline drain / memory-order guarantee required by a system
    /// event instruction before it may complete.
    ///
    /// C17: System events carry mandatory drain/flush/order guarantees rather than
    /// being treated as ordinary ALU-like micro-ops.
    ///
    /// <para>Guarantees are additive: higher values subsume lower ones.</para>
    /// </summary>
    public enum SystemEventOrderGuarantee : byte
    {
        /// <summary>
        /// No ordering requirement beyond normal in-order commit.
        /// Used for instructions that carry no side effects (should be rare for system ops).
        /// </summary>
        None = 0,

        /// <summary>
        /// All preceding memory stores must be visible to all agents before the
        /// event completes.  Maps to the RISC-V FENCE predecessor/successor contract
        /// (FENCE W, W).
        /// C17: Memory-ordering drain for data fences.
        /// </summary>
        DrainStores = 1,

        /// <summary>
        /// All preceding memory operations (loads + stores) must complete before the
        /// event and no subsequent memory operations may start until after the event.
        /// Maps to FENCE R+W, R+W.
        /// C17: Full memory ordering barrier.
        /// </summary>
        DrainMemory = 2,

        /// <summary>
        /// All in-flight micro-ops must retire (pipeline fully drained) and the
        /// instruction cache must be flushed before any new instruction is fetched.
        /// Required for FENCE.I and all privilege-level transitions.
        /// C17: Full pipeline flush including I-cache.
        /// </summary>
        FlushPipeline = 3,

        /// <summary>
        /// Full pipeline flush plus architectural state serialisation.
        /// All writes visible; all preceding exceptions taken before the event handler
        /// takes effect.  Required for ECALL, EBREAK, MRET, SRET.
        /// C17: Strongest ordering вЂ” trap-entry / trap-return serialization contract.
        /// </summary>
        FullSerialTrapBoundary = 4,
    }

    /// <summary>
    /// System-event micro-operation for serialising instructions (FENCE, ECALL, EBREAK, MRET,
    /// SRET, WFI).
    ///
    /// The decoded micro-op stays immutable after decode; the live EX/MEM/WB lane owns
    /// the generated <see cref="Pipeline.PipelineEvent"/> payload that retire forwards
    /// into the FSM/control-flow seam.
    ///
    /// Replaces <c>SystemNopMicroOp</c> for canonical system and typed SMT/VT event opcodes
    /// (V6 Phase 3 вЂ” A5).
    /// </summary>
    public sealed class SysEventMicroOp : MicroOp
    {
        private const int EcallCodeRegister = 17;

        /// <summary>Architectural event kind this operation generates.</summary>
        public SystemEventKind EventKind { get; init; }

        /// <summary>
        /// Pipeline drain / memory-order guarantee required by this event.
        ///
        /// C17: Explicit ordering contract so that schedulers and hazard-checkers
        /// can enforce drain/flush requirements without implicit, opcode-specific logic.
        /// </summary>
        public SystemEventOrderGuarantee OrderGuarantee { get; init; }

        public SysEventMicroOp()
        {
            IsStealable = false;
            HasSideEffects = true;
            Class = MicroOpClass.Other;
            ResourceMask = ResourceBitset.Zero;

            InstructionClass = Arch.InstructionClass.System;
            SerializationClass = Arch.SerializationClass.FullSerial;

            SetHardPinnedPlacement(SlotClass.SystemSingleton, 7);
        }

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.SelfPublishes;

        public override bool Execute(ref Processor.CPU_Core core)
        {
            return true;
        }

        /// <summary>
        /// Initializes canonical admission facts for the concrete system-event carrier.
        /// ECALL must truthfully publish its architectural a7/x17 source dependency;
        /// all other current system events remain register-neutral singleton carriers.
        /// </summary>
        public void InitializeMetadata()
        {
            ApplyCanonicalClassificationAndPlacement();

            ReadRegisters = EventKind == SystemEventKind.Ecall
                ? new[] { EcallCodeRegister }
                : Array.Empty<int>();
            WriteRegisters = Array.Empty<int>();
            ReadMemoryRanges = Array.Empty<(ulong, ulong)>();
            WriteMemoryRanges = Array.Empty<(ulong, ulong)>();

            ResourceMask = ResourceBitset.Zero;
            if (EventKind == SystemEventKind.Ecall)
            {
                ResourceMask |= ResourceMaskBuilder.ForRegisterRead(EcallCodeRegister);
            }

            PublishExplicitStructuralSafetyMask();
            RefreshAdmissionMetadata(this);
        }

        public override void RefreshWriteMetadata() => InitializeMetadata();

        /// <summary>
        /// Materialises the typed pipeline/FSM event for this instruction.
        /// The returned payload is carried by the pipeline lane, not by the micro-op.
        /// </summary>
        public Pipeline.PipelineEvent? CreatePipelineEvent(
            ref Processor.CPU_Core core,
            ulong bundleSerial = 0)
        {
            byte vtId = (byte)(VirtualThreadId & 0xFF);
            return EventKind switch
            {
                SystemEventKind.Fence => new Pipeline.FenceEvent { VtId = vtId, BundleSerial = bundleSerial, IsInstructionFence = false },
                SystemEventKind.FenceI => new Pipeline.FenceEvent { VtId = vtId, BundleSerial = bundleSerial, IsInstructionFence = true },
                SystemEventKind.Ecall => new Pipeline.EcallEvent { VtId = vtId, BundleSerial = bundleSerial, EcallCode = ReadEcallCode(ref core, vtId) },
                SystemEventKind.Ebreak => new Pipeline.EbreakEvent { VtId = vtId, BundleSerial = bundleSerial },
                SystemEventKind.Mret => new Pipeline.MretEvent { VtId = vtId, BundleSerial = bundleSerial },
                SystemEventKind.Sret => new Pipeline.SretEvent { VtId = vtId, BundleSerial = bundleSerial },
                SystemEventKind.Wfi => new Pipeline.WfiEvent { VtId = vtId, BundleSerial = bundleSerial },
                SystemEventKind.Wfe => new Pipeline.WfeEvent { VtId = vtId, BundleSerial = bundleSerial },
                SystemEventKind.Sev => new Pipeline.SevEvent { VtId = vtId, BundleSerial = bundleSerial },
                SystemEventKind.Yield => new Pipeline.YieldEvent { VtId = vtId, BundleSerial = bundleSerial },
                SystemEventKind.PodBarrier => new Pipeline.PodBarrierEvent { VtId = vtId, BundleSerial = bundleSerial },
                SystemEventKind.VtBarrier => new Pipeline.VtBarrierEvent { VtId = vtId, BundleSerial = bundleSerial },
                _ => null
            };
        }

        public override string GetDescription() => $"SysEvent({EventKind}, Order={OrderGuarantee}, OpCode={OpCode})";

        // в”Ђв”Ђв”Ђ Static factory helpers (C17: canonical order guarantees) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        /// <summary>
        /// Creates a <see cref="SysEventMicroOp"/> for <c>FENCE</c> with a
        /// <see cref="SystemEventOrderGuarantee.DrainMemory"/> order guarantee.
        /// </summary>
        public static SysEventMicroOp ForFence() =>
            Create(
                Processor.CPU_Core.IsaOpcodeValues.FENCE,
                SystemEventKind.Fence,
                SystemEventOrderGuarantee.DrainMemory);

        /// <summary>
        /// Creates a <see cref="SysEventMicroOp"/> for <c>FENCE.I</c> with a
        /// <see cref="SystemEventOrderGuarantee.FlushPipeline"/> order guarantee.
        /// </summary>
        public static SysEventMicroOp ForFenceI() =>
            Create(
                Processor.CPU_Core.IsaOpcodeValues.FENCE_I,
                SystemEventKind.FenceI,
                SystemEventOrderGuarantee.FlushPipeline);

        /// <summary>
        /// Creates a <see cref="SysEventMicroOp"/> for <c>ECALL</c> with a
        /// <see cref="SystemEventOrderGuarantee.FullSerialTrapBoundary"/> order guarantee.
        /// </summary>
        public static SysEventMicroOp ForEcall() =>
            Create(
                Processor.CPU_Core.IsaOpcodeValues.ECALL,
                SystemEventKind.Ecall,
                SystemEventOrderGuarantee.FullSerialTrapBoundary);

        /// <summary>
        /// Creates a <see cref="SysEventMicroOp"/> for <c>EBREAK</c> with a
        /// <see cref="SystemEventOrderGuarantee.FullSerialTrapBoundary"/> order guarantee.
        /// </summary>
        public static SysEventMicroOp ForEbreak() =>
            Create(
                Processor.CPU_Core.IsaOpcodeValues.EBREAK,
                SystemEventKind.Ebreak,
                SystemEventOrderGuarantee.FullSerialTrapBoundary);

        /// <summary>
        /// Creates a <see cref="SysEventMicroOp"/> for <c>MRET</c> with a
        /// <see cref="SystemEventOrderGuarantee.FullSerialTrapBoundary"/> order guarantee.
        /// </summary>
        public static SysEventMicroOp ForMret() =>
            Create(
                Processor.CPU_Core.IsaOpcodeValues.MRET,
                SystemEventKind.Mret,
                SystemEventOrderGuarantee.FullSerialTrapBoundary);

        /// <summary>
        /// Creates a <see cref="SysEventMicroOp"/> for <c>SRET</c> with a
        /// <see cref="SystemEventOrderGuarantee.FullSerialTrapBoundary"/> order guarantee.
        /// </summary>
        public static SysEventMicroOp ForSret() =>
            Create(
                Processor.CPU_Core.IsaOpcodeValues.SRET,
                SystemEventKind.Sret,
                SystemEventOrderGuarantee.FullSerialTrapBoundary);

        /// <summary>
        /// Creates a <see cref="SysEventMicroOp"/> for <c>WFI</c> with a
        /// <see cref="SystemEventOrderGuarantee.DrainMemory"/> order guarantee.
        /// </summary>
        public static SysEventMicroOp ForWfi() =>
            Create(
                Processor.CPU_Core.IsaOpcodeValues.WFI,
                SystemEventKind.Wfi,
                SystemEventOrderGuarantee.DrainMemory);

        /// <summary>
        /// Creates a <see cref="SysEventMicroOp"/> for <c>WFE</c>.
        /// Wait-state semantics are owned by the FSM when the retired event is consumed.
        /// </summary>
        public static SysEventMicroOp ForWfe() =>
            Create(
                Processor.CPU_Core.IsaOpcodeValues.WFE,
                SystemEventKind.Wfe,
                SystemEventOrderGuarantee.DrainMemory);

        /// <summary>
        /// Creates a <see cref="SysEventMicroOp"/> for <c>SEV</c>.
        /// Wake semantics are owned by the FSM when the retired event is consumed.
        /// </summary>
        public static SysEventMicroOp ForSev() =>
            Create(
                Processor.CPU_Core.IsaOpcodeValues.SEV,
                SystemEventKind.Sev,
                SystemEventOrderGuarantee.DrainMemory);

        /// <summary>
        /// Creates a <see cref="SysEventMicroOp"/> for <c>YIELD</c>.
        /// Current runtime semantics are carried by the retired event/FSM plane
        /// without an additional serializing retire boundary.
        /// </summary>
        public static SysEventMicroOp ForYield() =>
            Create(
                Processor.CPU_Core.IsaOpcodeValues.YIELD,
                SystemEventKind.Yield,
                SystemEventOrderGuarantee.None);

        /// <summary>
        /// Creates a <see cref="SysEventMicroOp"/> for <c>POD_BARRIER</c>.
        /// Current runtime semantics are carried by the retired event/FSM plane
        /// without an additional serializing retire boundary.
        /// </summary>
        public static SysEventMicroOp ForPodBarrier() =>
            Create(
                Processor.CPU_Core.IsaOpcodeValues.POD_BARRIER,
                SystemEventKind.PodBarrier,
                SystemEventOrderGuarantee.None);

        /// <summary>
        /// Creates a <see cref="SysEventMicroOp"/> for <c>VT_BARRIER</c>.
        /// Current runtime semantics are carried by the retired event/FSM plane
        /// without an additional serializing retire boundary.
        /// </summary>
        public static SysEventMicroOp ForVtBarrier() =>
            Create(
                Processor.CPU_Core.IsaOpcodeValues.VT_BARRIER,
                SystemEventKind.VtBarrier,
                SystemEventOrderGuarantee.None);

        private static SysEventMicroOp Create(
            ushort opcode,
            SystemEventKind eventKind,
            SystemEventOrderGuarantee orderGuarantee)
        {
            var microOp = new SysEventMicroOp
            {
                OpCode = (uint)opcode,
                EventKind = eventKind,
                OrderGuarantee = orderGuarantee
            };
            microOp.InitializeMetadata();
            return microOp;
        }

        private void ApplyCanonicalClassificationAndPlacement()
        {
            if (Arch.OpcodeRegistry.TryGetPublishedSemantics(
                    OpCode,
                    out Arch.InstructionClass publishedInstructionClass,
                    out Arch.SerializationClass publishedSerializationClass))
            {
                InstructionClass = publishedInstructionClass;
                SerializationClass = publishedSerializationClass;
            }
            else
            {
                // Compatibility fallback for helper-constructed carriers that do not
                // yet carry an authoritative published opcode identity.
                InstructionClass = EventKind switch
                {
                    SystemEventKind.Yield or
                    SystemEventKind.Wfe or
                    SystemEventKind.Sev or
                    SystemEventKind.PodBarrier or
                    SystemEventKind.VtBarrier => Arch.InstructionClass.SmtVt,
                    _ => Arch.InstructionClass.System
                };

                SerializationClass = EventKind switch
                {
                    SystemEventKind.Fence => Arch.SerializationClass.MemoryOrdered,
                    SystemEventKind.Yield => Arch.SerializationClass.Free,
                    _ => Arch.SerializationClass.FullSerial
                };
            }

            SetHardPinnedPlacement(SlotClass.SystemSingleton, 7);
        }

        /// <summary>
        /// Read the ECALL code from the authoritative a7 (x17) architectural source.
        /// Fails closed when the current execution contour cannot publish that value.
        /// </summary>
        private static long ReadEcallCode(ref Processor.CPU_Core core, int ownerThreadId)
        {
            // RISC-V ABI: a7 = x17 (register index 17 in the local core window).
            int vtId = NormalizeExecutionVtId(ownerThreadId);
            if (!TryReadUnifiedArchValue(ref core, vtId, EcallCodeRegister, out ulong value))
            {
                throw new InvalidOperationException(
                    "ECALL carrier could not resolve authoritative a7/x17 on the current mainline system-event contour; refusing hidden zero-code fallback.");
            }

            return (long)value;
        }
    }

    /// <summary>
    /// Stream-control micro-operation for STREAM_SETUP, STREAM_START, and STREAM_WAIT.
    ///
    /// These carriers preserve canonical decode/materializer facts for the
    /// stream-control opcodes without granting them an implicit execution surface.
    /// <c>STREAM_WAIT</c> currently follows through the serializing-boundary retire
    /// contour, while <c>STREAM_SETUP</c> and <c>STREAM_START</c> are rejected by the
    /// execution-surface contract until an explicit mainline retire/apply path lands.
    ///
    /// Replaces <c>SystemNopMicroOp</c> for the three stream-engine opcodes
    /// (V6 Phase 3 вЂ” A5).
    /// </summary>
        public sealed class StreamControlMicroOp : MicroOp
        {
            public StreamControlMicroOp()
            {
                IsStealable = false;
                // Stream-control carriers do not themselves perform a typed data-memory
                // transaction. STREAM_WAIT still follows the lane-7 serializing-boundary
                // contour; unsupported setup/start surfaces are rejected before execute.
                IsMemoryOp = false;
                HasSideEffects = true;
                Class = MicroOpClass.Other;
                ResourceMask = ResourceBitset.Zero;

            InstructionClass = Arch.InstructionClass.System;
            SerializationClass = Arch.SerializationClass.FullSerial;

              SetPlacement(SlotClass.Unclassified, SlotPinningKind.HardPinned);
          }

          public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
              CanonicalDecodePublicationMode.SelfPublishes;

        public void InitializeMetadata()
        {
            ushort opcode = unchecked((ushort)OpCode);

            ReadRegisters = Array.Empty<int>();
            WriteRegisters = Array.Empty<int>();
            ResourceMask = ResourceBitset.Zero;

            InstructionClass = Arch.InstructionClassifier.GetClass(opcode);
            SerializationClass = Arch.InstructionClassifier.GetSerializationClass(opcode);
            SetHardPinnedPlacement(SlotClass.SystemSingleton, 7);

            PublishExplicitStructuralSafetyMask();
            RefreshAdmissionMetadata(this);
        }

        public override bool Execute(ref Processor.CPU_Core core) => true;
        public override string GetDescription() => $"StreamCtrl (OpCode={OpCode})";
    }

    /// <summary>
    /// Port I/O micro-operation (PortInput/PortOutput).
}
