
using System;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            public struct ScalarWriteBackLaneState
            {
                public bool IsOccupied;
                public byte LaneIndex;
                public byte SlotIndex;
                public ulong PC;
                public uint OpCode;
                public ulong ResultValue;
                public bool IsMemoryOp;
                public ulong MemoryAddress;
                public ulong MemoryData;
                public bool IsLoad;
                public byte MemoryAccessSize;
                public bool WritesRegister;
                public ushort DestRegID;
                public Core.MicroOp MicroOp;
                public Core.Pipeline.PipelineEvent? GeneratedEvent;
                public Core.CsrRetireEffect? GeneratedCsrEffect;
                public Core.AtomicRetireEffect? GeneratedAtomicEffect;
                public Core.VmxRetireEffect? GeneratedVmxEffect;
                public byte GeneratedRetireRecordCount;
                public RetireRecord GeneratedRetireRecord0;
                public RetireRecord GeneratedRetireRecord1;
                public ulong DomainTag;
                public int MshrScoreboardSlot;
                public int MshrVirtualThreadId;
                public Core.ResourceBitset ResourceMask;
                public ulong ResourceToken;
                public int OwnerThreadId;
                public int VirtualThreadId;
                /// <summary>
                /// Architectural privilege-domain identifier for this lane.
                /// Propagated from <c>MicroOp.OwnerContextId</c>;
                /// distinct from <c>VirtualThreadId</c> (SMT hardware-thread slot).
                /// </summary>
                public int OwnerContextId;
                public bool WasFspInjected;
                public int OriginalThreadId;
                public bool HasFault;
                public ulong FaultAddress;
                public bool FaultIsWrite;
                public bool DefersStoreCommitToWriteBack;

                public void Clear(byte laneIndex)
                {
                    IsOccupied = false;
                    LaneIndex = laneIndex;
                    SlotIndex = 0;
                    PC = 0;
                    OpCode = 0;
                    ResultValue = 0;
                    IsMemoryOp = false;
                    MemoryAddress = 0;
                    MemoryData = 0;
                    IsLoad = false;
                    MemoryAccessSize = 0;
                    WritesRegister = false;
                    DestRegID = 0;
                    MicroOp = null;
                    GeneratedEvent = null;
                    GeneratedCsrEffect = null;
                    GeneratedAtomicEffect = null;
                    GeneratedVmxEffect = null;
                    GeneratedRetireRecordCount = 0;
                    GeneratedRetireRecord0 = default;
                    GeneratedRetireRecord1 = default;
                    DomainTag = 0;
                    MshrScoreboardSlot = -1;
                    MshrVirtualThreadId = 0;
                    ResourceMask = Core.ResourceBitset.Zero;
                    ResourceToken = 0;
                    OwnerThreadId = 0;
                    VirtualThreadId = 0;
                    OwnerContextId = 0;
                    WasFspInjected = false;
                    OriginalThreadId = 0;
                    HasFault = false;
                    FaultAddress = 0;
                    FaultIsWrite = false;
                    DefersStoreCommitToWriteBack = false;
                }
            }
        }
    }
}
