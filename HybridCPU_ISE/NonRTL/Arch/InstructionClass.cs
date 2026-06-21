namespace YAKSys_Hybrid_CPU.Arch
{
    /// <summary>
    /// Canonical instruction class for HybridCPU ISA v4.
    /// Determines pipeline routing, slot class assignment, and serialization behavior.
    /// Introduced in Phase 02 of the ISA v4 refactoring.
    /// </summary>
    public enum InstructionClass : byte
    {
        /// <summary>Register-register and immediate integer ALU operations.</summary>
        ScalarAlu       = 0,

        /// <summary>Typed scalar load and store operations.</summary>
        Memory          = 1,

        /// <summary>Jumps and conditional branches.</summary>
        ControlFlow     = 2,

        /// <summary>LR/SC and AMO (word and doubleword).</summary>
        Atomic          = 3,

        /// <summary>FENCE, ECALL, EBREAK, MRET, SRET, WFI.</summary>
        System          = 4,

        /// <summary>CSRRW/CSRRS/CSRRC and immediate variants.</summary>
        Csr             = 5,

        /// <summary>YIELD, WFE, SEV, POD_BARRIER, VT_BARRIER.</summary>
        SmtVt           = 6,

        /// <summary>VMXON/VMXOFF/VMLAUNCH/VMRESUME/VMREAD/VMWRITE/VMCLEAR/VMPTRLD.</summary>
        Vmx             = 7,
    }
}
