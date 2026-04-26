using HybridCPU_ISE.Arch;

using System;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            /// <summary>
            /// Pipeline stage for instruction decode
            /// </summary>
            public struct DecodeStage
            {
                public bool Valid;
                public ulong PC;
                public VLIW_Instruction Instruction;
                public byte SlotIndex;          // Which slot in VLIW bundle (0-7)

                // Decoded operands
                public ushort Reg1ID, Reg2ID, Reg3ID;
                public ulong AuxData;
                public uint OpCode;

                // Control signals
                public bool IsMemoryOp;         // Needs memory access?
                public bool IsVectorOp;         // Is vector/stream operation?
                public bool IsBranchOp;         // Is control flow?
                public bool WritesRegister;     // Will write back to register?

                // MicroOp support
                public Core.MicroOp MicroOp;    // Decoded micro-operation
                internal Core.RuntimeClusterAdmissionExecutionMode AdmissionExecutionMode;

                public void Clear()
                {
                    Valid = false;
                    PC = 0;
                    Instruction = new VLIW_Instruction();
                    SlotIndex = 0;
                    Reg1ID = Reg2ID = Reg3ID = 0;
                    AuxData = 0;
                    OpCode = 0;
                    IsMemoryOp = IsVectorOp = IsBranchOp = WritesRegister = false;
                    MicroOp = null;
                    AdmissionExecutionMode = Core.RuntimeClusterAdmissionExecutionMode.Empty;
                }
            }
        }
    }
}

