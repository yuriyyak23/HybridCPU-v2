namespace YAKSys_Hybrid_CPU.Arch
{
    /// <summary>
    /// Serialization class determines the ordering and side-effect isolation
    /// requirements for an instruction in the pipeline.
    /// Introduced in Phase 02 of the ISA v4 refactoring.
    /// </summary>
    public enum SerializationClass : byte
    {
        /// <summary>
        /// No serialization required. Instruction can be freely reordered
        /// within its bundle slot subject only to data hazard constraints.
        /// Applies to pure ALU, load, and unconditional jump instructions.
        /// </summary>
        Free            = 0,

        /// <summary>
        /// Memory ordering: instruction observes store-load ordering.
        /// Applies to FENCE and typed loads/stores in ordered contexts.
        /// </summary>
        MemoryOrdered   = 1,

        /// <summary>
        /// Full serialization: no instruction may issue before this completes.
        /// Applies to ECALL, EBREAK, MRET, SRET, WFI, and synchronization barriers.
        /// </summary>
        FullSerial      = 2,

        /// <summary>
        /// Atomic: occupies the full memory bus for its duration.
        /// Applies to all LR/SC and AMO (word and doubleword) instructions.
        /// </summary>
        AtomicSerial    = 3,

        /// <summary>
        /// CSR-ordered: serializes CSR state reads and writes.
        /// Allows out-of-order issue only if no CSR hazard exists.
        /// Applies to CSRRW, CSRRS, CSRRC, and their immediate variants.
        /// </summary>
        CsrOrdered      = 4,

        /// <summary>
        /// VMX-serial: instruction transitions the pipeline FSM.
        /// All VMX instructions are fully serialized.
        /// Applies to VMXON, VMXOFF, VMLAUNCH, VMRESUME, VMREAD, VMWRITE, VMCLEAR, VMPTRLD.
        /// </summary>
        VmxSerial       = 5,
    }
}
