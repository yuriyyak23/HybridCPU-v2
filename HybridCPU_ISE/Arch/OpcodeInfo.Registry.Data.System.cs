namespace YAKSys_Hybrid_CPU.Arch
{
    public static partial class OpcodeRegistry
    {
        private static OpcodeInfo[] CreateSystemOpcodes() =>
        [
            // ========== CSR (Control & Status Register) ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.CSR_CLEAR, "CSRCLR", OpcodeCategory.System, 0, InstructionFlags.None, 1, 0, InstructionClass.Csr, SerializationClass.CsrOrdered),

            // ========== ISA v2: Atomic Extension — LR/SC + AMO ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.LR_W, "LR.W", OpcodeCategory.Atomic, 1, InstructionFlags.Atomic | InstructionFlags.MemoryRead, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.SC_W, "SC.W", OpcodeCategory.Atomic, 2, InstructionFlags.Atomic | InstructionFlags.MemoryWrite, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.LR_D, "LR.D", OpcodeCategory.Atomic, 1, InstructionFlags.Atomic | InstructionFlags.MemoryRead, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.SC_D, "SC.D", OpcodeCategory.Atomic, 2, InstructionFlags.Atomic | InstructionFlags.MemoryWrite, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.AMOADD_W, "AMOADD.W", OpcodeCategory.Atomic, 2, InstructionFlags.Atomic | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.AMOSWAP_W, "AMOSWAP.W", OpcodeCategory.Atomic, 2, InstructionFlags.Atomic | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.AMOOR_W, "AMOOR.W", OpcodeCategory.Atomic, 2, InstructionFlags.Atomic | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.AMOAND_W, "AMOAND.W", OpcodeCategory.Atomic, 2, InstructionFlags.Atomic | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.AMOXOR_W, "AMOXOR.W", OpcodeCategory.Atomic, 2, InstructionFlags.Atomic | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.AMOMIN_W, "AMOMIN.W", OpcodeCategory.Atomic, 2, InstructionFlags.Atomic | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.AMOMAX_W, "AMOMAX.W", OpcodeCategory.Atomic, 2, InstructionFlags.Atomic | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.AMOMINU_W, "AMOMINU.W", OpcodeCategory.Atomic, 2, InstructionFlags.Atomic | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.AMOMAXU_W, "AMOMAXU.W", OpcodeCategory.Atomic, 2, InstructionFlags.Atomic | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 1),

            // ========== ISA v4: AMO Doubleword Extension ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.AMOADD_D, "AMOADD.D", OpcodeCategory.Atomic, 2, InstructionFlags.Atomic | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.AMOSWAP_D, "AMOSWAP.D", OpcodeCategory.Atomic, 2, InstructionFlags.Atomic | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.AMOOR_D, "AMOOR.D", OpcodeCategory.Atomic, 2, InstructionFlags.Atomic | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.AMOAND_D, "AMOAND.D", OpcodeCategory.Atomic, 2, InstructionFlags.Atomic | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.AMOXOR_D, "AMOXOR.D", OpcodeCategory.Atomic, 2, InstructionFlags.Atomic | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.AMOMIN_D, "AMOMIN.D", OpcodeCategory.Atomic, 2, InstructionFlags.Atomic | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.AMOMAX_D, "AMOMAX.D", OpcodeCategory.Atomic, 2, InstructionFlags.Atomic | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.AMOMINU_D, "AMOMINU.D", OpcodeCategory.Atomic, 2, InstructionFlags.Atomic | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.AMOMAXU_D, "AMOMAXU.D", OpcodeCategory.Atomic, 2, InstructionFlags.Atomic | InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite, 4, 1),

            // ========== ISA v4: SMT / VT Synchronisation ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.YIELD, "YIELD", OpcodeCategory.System, 0, InstructionFlags.None, 1, 0, InstructionClass.SmtVt, SerializationClass.Free),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.WFE, "WFE", OpcodeCategory.System, 0, InstructionFlags.Privileged, 1, 0, InstructionClass.SmtVt, SerializationClass.FullSerial),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.SEV, "SEV", OpcodeCategory.System, 0, InstructionFlags.Privileged, 1, 0, InstructionClass.SmtVt, SerializationClass.FullSerial),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.POD_BARRIER, "POD_BARRIER", OpcodeCategory.System, 0, InstructionFlags.Privileged, 1, 0, InstructionClass.SmtVt, SerializationClass.FullSerial),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VT_BARRIER, "VT_BARRIER", OpcodeCategory.System, 1, InstructionFlags.Privileged, 1, 0, InstructionClass.SmtVt, SerializationClass.FullSerial),

            // ========== ISA v4: VMX Instruction Plane ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VMXON, "VMXON", OpcodeCategory.Privileged, 1, InstructionFlags.Privileged, 1, 0, InstructionClass.Vmx, SerializationClass.VmxSerial),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VMXOFF, "VMXOFF", OpcodeCategory.Privileged, 0, InstructionFlags.Privileged, 1, 0, InstructionClass.Vmx, SerializationClass.VmxSerial),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VMLAUNCH, "VMLAUNCH", OpcodeCategory.Privileged, 0, InstructionFlags.Privileged, 1, 0, InstructionClass.Vmx, SerializationClass.VmxSerial),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VMRESUME, "VMRESUME", OpcodeCategory.Privileged, 0, InstructionFlags.Privileged, 1, 0, InstructionClass.Vmx, SerializationClass.VmxSerial),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VMREAD, "VMREAD", OpcodeCategory.Privileged, 2, InstructionFlags.Privileged | InstructionFlags.UsesImmediate, 1, 0, InstructionClass.Vmx, SerializationClass.VmxSerial),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VMWRITE, "VMWRITE", OpcodeCategory.Privileged, 2, InstructionFlags.Privileged | InstructionFlags.UsesImmediate, 1, 0, InstructionClass.Vmx, SerializationClass.VmxSerial),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VMCLEAR, "VMCLEAR", OpcodeCategory.Privileged, 1, InstructionFlags.Privileged | InstructionFlags.MemoryWrite, 1, 0, InstructionClass.Vmx, SerializationClass.VmxSerial),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VMPTRLD, "VMPTRLD", OpcodeCategory.Privileged, 1, InstructionFlags.Privileged | InstructionFlags.MemoryRead, 1, 0, InstructionClass.Vmx, SerializationClass.VmxSerial),

            // ========== ISA v2: Memory Ordering ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.FENCE, "FENCE", OpcodeCategory.System, 0, InstructionFlags.UsesImmediate, 1, 0, InstructionClass.System, SerializationClass.MemoryOrdered),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.FENCE_I, "FENCE.I", OpcodeCategory.System, 0, InstructionFlags.None, 1, 0, InstructionClass.System, SerializationClass.FullSerial),

            // ========== ISA v2: Trap / Privileged ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.ECALL, "ECALL", OpcodeCategory.Privileged, 0, InstructionFlags.Privileged, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.EBREAK, "EBREAK", OpcodeCategory.Privileged, 0, InstructionFlags.Privileged, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.MRET, "MRET", OpcodeCategory.Privileged, 0, InstructionFlags.Privileged, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.SRET, "SRET", OpcodeCategory.Privileged, 0, InstructionFlags.Privileged, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.WFI, "WFI", OpcodeCategory.Privileged, 0, InstructionFlags.Privileged, 1, 0),

            // ========== ISA v2: CSR Extension ==========
            // Assembler-style csrr/csrw lower to the canonical CSRRS/CSRRW hardware opcodes.
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.CSRRW, "CSRRW", OpcodeCategory.System, 2, InstructionFlags.UsesImmediate, 1, 0, InstructionClass.Csr, SerializationClass.CsrOrdered),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.CSRRS, "CSRRS", OpcodeCategory.System, 2, InstructionFlags.UsesImmediate, 1, 0, InstructionClass.Csr, SerializationClass.CsrOrdered),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.CSRRC, "CSRRC", OpcodeCategory.System, 2, InstructionFlags.UsesImmediate, 1, 0, InstructionClass.Csr, SerializationClass.CsrOrdered),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.CSRRWI, "CSRRWI", OpcodeCategory.System, 1, InstructionFlags.UsesImmediate, 1, 0, InstructionClass.Csr, SerializationClass.CsrOrdered),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.CSRRSI, "CSRRSI", OpcodeCategory.System, 1, InstructionFlags.UsesImmediate, 1, 0, InstructionClass.Csr, SerializationClass.CsrOrdered),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.CSRRCI, "CSRRCI", OpcodeCategory.System, 1, InstructionFlags.UsesImmediate, 1, 0, InstructionClass.Csr, SerializationClass.CsrOrdered),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VSETVEXCPMASK, "VSETVEXCPMASK", OpcodeCategory.System, 1, InstructionFlags.None, 1, 0, InstructionClass.Csr, SerializationClass.FullSerial),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.VSETVEXCPPRI, "VSETVEXCPPRI", OpcodeCategory.System, 1, InstructionFlags.None, 1, 0, InstructionClass.Csr, SerializationClass.FullSerial),

            // ========== ISA v2: Stream Engine ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.STREAM_SETUP, "STREAM_SETUP", OpcodeCategory.System, 2, InstructionFlags.None, 1, 0, InstructionClass.System, SerializationClass.FullSerial),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.STREAM_START, "STREAM_START", OpcodeCategory.System, 1, InstructionFlags.None, 1, 0, InstructionClass.System, SerializationClass.FullSerial),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.STREAM_WAIT, "STREAM_WAIT", OpcodeCategory.System, 1, InstructionFlags.None, 1, 0, InstructionClass.SmtVt, SerializationClass.FullSerial),
        ];
    }
}
