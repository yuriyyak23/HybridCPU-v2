using HybridCPU_ISE.Arch;

using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;


namespace YAKSys_Hybrid_CPU.Core
{
    public class PortIOMicroOp : MicroOp
    {
        public bool IsOutput { get; set; }
        public ushort PortId { get; set; }

        public PortIOMicroOp()
        {
            IsStealable = false;
            HasSideEffects = true;
            Class = MicroOpClass.Other;
            ResourceMask = ResourceBitset.Zero;

            // ISA v4 Phase 02: port I/O has system-level side effects, FullSerial serialization
            InstructionClass = Arch.InstructionClass.System;
            SerializationClass = Arch.SerializationClass.FullSerial;

            // Phase 01: Typed-slot taxonomy — system singleton pinned to lane 7
            SetHardPinnedPlacement(SlotClass.SystemSingleton, 7);
            PublishExplicitStructuralSafetyMask();
        }

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.ProjectorPublishes;

        public override bool Execute(ref Processor.CPU_Core core) => true;
        public override string GetDescription() => $"Port{(IsOutput ? "Output" : "Input")} #{PortId}";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TrapMicroOp — Blueprint §7 (replaces SystemNopMicroOp/NopMicroOp fallback)
    // ─────────────────────────────────────────────────────────────────────────



    /// <summary>
    /// Trap micro-operation: encodes an unrecognised or privileged-fault instruction.
    /// <para>
    /// Replaces the legacy <c>SystemNopMicroOp</c>/<c>NopMicroOp</c> fallback for
    /// unregistered opcodes. On <see cref="Execute"/> the trap is recorded; the pipeline
    /// FSM is responsible for redirecting control flow to the trap-handler vector.
    /// </para>
    /// Blueprint §7: "Удалить SystemNopMicroOp класс как placeholder,
    /// заменив его на TrapMicroOp или реальное исполнение системных инструкций."
    /// Blueprint §5 (decoder): "legacyOp (для незарегистрированных) переделать в нормальную
    /// ошибку/исключение, а не SystemNop."
    /// </summary>
    public sealed class TrapMicroOp : MicroOp
    {
        private const ulong IllegalInstructionCauseCode = 2;

        /// <summary>The original undecoded opcode that triggered the trap.</summary>
        public uint UndecodedOpCode { get; set; }

        /// <summary>Optional human-readable reason for the trap (e.g., decode exception message).</summary>
        public string TrapReason { get; set; }

        /// <summary>
        /// Projected memory-bank intent captured from the canonical memory materialization
        /// attempt before the carrier fail-closed trapped. Non-memory trap contours keep -1.
        /// </summary>
        internal int ProjectedMemoryBankIntent { get; set; } = -1;

        public TrapMicroOp()
        {
            // Traps redirect control flow to the exception/trap-handler vector.
            IsStealable = false;
            IsControlFlow = true;

            ReadRegisters = Array.Empty<int>();
            WriteRegisters = Array.Empty<int>();
            ReadMemoryRanges = Array.Empty<(ulong, ulong)>();
            WriteMemoryRanges = Array.Empty<(ulong, ulong)>();

            // Trap consumes no compute resources but occupies the system-singleton lane.
            ResourceMask = ResourceBitset.Zero;

            InstructionClass = Arch.InstructionClass.System;
            SerializationClass = Arch.SerializationClass.FullSerial;

            // Trap is fully serialised — must be routed to the system-singleton slot.
            SetHardPinnedPlacement(SlotClass.SystemSingleton, 7);

            // Conflicts with everything: traps must not be reordered or stolen.
            SafetyMask = SafetyMask128.All;
        }

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.ProjectorPublishes;

        public override bool Execute(ref Processor.CPU_Core core)
        {
            // Trap is recorded; the pipeline FSM handles redirection to the handler vector.
            return true;
        }

        public Pipeline.TrapEntryEvent CreatePipelineEvent(ulong bundleSerial = 0)
        {
            byte vtId = (byte)(VirtualThreadId & 0xFF);
            return new Pipeline.TrapEntryEvent
            {
                VtId = vtId,
                BundleSerial = bundleSerial,
                CauseCode = IllegalInstructionCauseCode,
                FaultAddress = 0
            };
        }

        public override string GetDescription() =>
            TrapReason != null
                ? $"TRAP: UndecodedOpCode=0x{UndecodedOpCode:X} ({TrapReason})"
                : $"TRAP: UndecodedOpCode=0x{UndecodedOpCode:X}";
    }

    // VmxMicroOp — Blueprint Step 2 (VMX instruction plane)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// MicroOp that executes a single VMX instruction through <see cref="VmxExecutionUnit"/>.
    /// <para>
    /// All VMX instructions are fully serialised (<see cref="Arch.SerializationClass.VmxSerial"/>)
    /// and pinned to the system-singleton slot. They require Machine-mode privilege.
    /// </para>
    /// <para>
    /// Blueprint Phase 6: <see cref="Execute"/> delegates to the per-core
    /// <see cref="Processor.CPU_Core.VmxUnit"/> via the scoped
    /// <see cref="Processor.CPU_Core.LiveCpuStateAdapter"/> seam.
    /// </para>
    /// </summary>
    public sealed class VmxMicroOp : MicroOp
    {
        private VmxRetireEffect _resolvedRetireEffect;

        /// <summary>Decoded instruction IR forwarded to the VMX execution unit.</summary>
        public Pipeline.MicroOps.InstructionIR? Instruction { get; set; }

        /// <summary>Destination register index (Rd) — used for VMREAD result writeback.</summary>
        public byte Rd { get; set; }

        /// <summary>Source register 1 index (Rs1) — VMCS field selector / pointer register.</summary>
        public byte Rs1 { get; set; }

        /// <summary>Source register 2 index (Rs2) — VMWRITE value register.</summary>
        public byte Rs2 { get; set; }

        public VmxMicroOp()
        {
            Class = MicroOpClass.Other;
            ResourceMask = ResourceBitset.Zero;

            InstructionClass = Arch.InstructionClass.Vmx;
            SerializationClass = Arch.SerializationClass.VmxSerial;

            // VMX instructions are pinned to the system-singleton slot (lane 7)
            SetHardPinnedPlacement(SlotClass.SystemSingleton, 7);
        }

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.SelfPublishes;

        public override void RefreshWriteMetadata() => InitializeMetadata();

        public override bool Execute(ref Processor.CPU_Core core)
        {
            var instr = Instruction ?? BuildInstructionIR();

            int vmxVtId = Math.Clamp(OwnerThreadId, 0, Processor.CPU_Core.SmtWays - 1);
            var liveState = core.CreateLiveCpuStateAdapter(vmxVtId);
            _resolvedRetireEffect = core.VmxUnit.Resolve(
                instr,
                liveState,
                PrivilegeLevel.Machine,
                (byte)vmxVtId);
            return true;
        }

        public override string GetDescription() =>
            $"VMX: OpCode=0x{OpCode:X} ({Arch.OpcodeRegistry.GetMnemonicOrHex(OpCode)})";

        // ── Helper ────────────────────────────────────────────────────────────

        public VmxRetireEffect CreateRetireEffect() => _resolvedRetireEffect;

        private static bool HasArchitecturalRegister(byte registerId) =>
            registerId != 0 &&
            registerId != VLIW_Instruction.NoArchReg;

        private static byte NormalizeInstructionIrRegister(byte registerId) =>
            registerId == VLIW_Instruction.NoArchReg
                ? (byte)0
                : registerId;

        private void InitializeMetadata()
        {
            var readRegs = new List<int>();
            ResourceMask = ResourceBitset.Zero;
            ushort opcode = unchecked((ushort)OpCode);
            WritesRegister =
                opcode == Processor.CPU_Core.IsaOpcodeValues.VMREAD &&
                HasArchitecturalRegister(Rd);

            switch (opcode)
            {
                case Processor.CPU_Core.IsaOpcodeValues.VMREAD:
                    if (HasArchitecturalRegister(Rs1))
                    {
                        readRegs.Add(Rs1);
                        ResourceMask |= ResourceMaskBuilder.ForRegisterRead(Rs1);
                    }
                    break;

                case Processor.CPU_Core.IsaOpcodeValues.VMWRITE:
                    if (HasArchitecturalRegister(Rs1))
                    {
                        readRegs.Add(Rs1);
                        ResourceMask |= ResourceMaskBuilder.ForRegisterRead(Rs1);
                    }

                    if (HasArchitecturalRegister(Rs2))
                    {
                        readRegs.Add(Rs2);
                        ResourceMask |= ResourceMaskBuilder.ForRegisterRead(Rs2);
                    }
                    break;

                case Processor.CPU_Core.IsaOpcodeValues.VMCLEAR:
                case Processor.CPU_Core.IsaOpcodeValues.VMPTRLD:
                    if (HasArchitecturalRegister(Rs1))
                    {
                        readRegs.Add(Rs1);
                        ResourceMask |= ResourceMaskBuilder.ForRegisterRead(Rs1);
                    }
                    break;
            }

            ReadRegisters = readRegs;
            WriteRegisters = WritesRegister
                ? new[] { (int)Rd }
                : Array.Empty<int>();

            if (WritesRegister)
            {
                ResourceMask |= ResourceMaskBuilder.ForRegisterWrite(Rd);
            }

            PublishExplicitStructuralSafetyMask();
            RefreshAdmissionMetadata(this);
        }

        private Pipeline.MicroOps.InstructionIR BuildInstructionIR()
        {
            ushort opcode = unchecked((ushort)OpCode);
            return new Pipeline.MicroOps.InstructionIR
            {
                CanonicalOpcode = new Processor.CPU_Core.IsaOpcode(opcode),
                Class = Arch.InstructionClass.Vmx,
                SerializationClass = Arch.SerializationClass.VmxSerial,
                Rd = NormalizeInstructionIrRegister(Rd),
                Rs1 = NormalizeInstructionIrRegister(Rs1),
                Rs2 = NormalizeInstructionIrRegister(Rs2),
                Imm = 0,
            };
        }
    }
}
