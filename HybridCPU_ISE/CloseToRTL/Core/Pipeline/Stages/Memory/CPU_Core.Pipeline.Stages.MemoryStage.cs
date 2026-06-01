
using System;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            /// <summary>
            /// Pipeline stage for memory access
            /// </summary>
            public struct MemoryStage
            {
                public bool Valid;
                public byte ActiveLaneIndex;
                public ScalarMemoryLaneState Lane0;
                public ScalarMemoryLaneState Lane1;
                public ScalarMemoryLaneState Lane2;
                public ScalarMemoryLaneState Lane3;
                public ScalarMemoryLaneState Lane4;
                public ScalarMemoryLaneState Lane5;
                public ScalarMemoryLaneState Lane6;
                public ScalarMemoryLaneState Lane7;
                public byte PreparedScalarMask;
                public byte RefinedPreparedScalarMask;
                public int MaterializedScalarLaneCount;
                public int MaterializedPhysicalLaneCount;
                public bool UsesExplicitPacketLanes;
                public bool RetainsReferenceSequentialPath;
                public byte SelectedNonScalarSlotMask;
                public byte BlockedScalarCandidateMask;
                internal Core.RuntimeClusterAdmissionExecutionMode AdmissionExecutionMode;

                public ulong PC
                {
                    get => GetLane(ActiveLaneIndex).PC;
                    set => SetLanePC(ActiveLaneIndex, value);
                }

                public uint OpCode
                {
                    get => GetLane(ActiveLaneIndex).OpCode;
                    set => SetLaneOpCode(ActiveLaneIndex, value);
                }

                public ulong ResultValue
                {
                    get => GetLane(ActiveLaneIndex).ResultValue;
                    set => SetLaneResultValue(ActiveLaneIndex, value);
                }

                public bool ResultReady
                {
                    get => GetLane(ActiveLaneIndex).ResultReady;
                    set => SetLaneResultReady(ActiveLaneIndex, value);
                }

                public bool IsVectorOp
                {
                    get => GetLane(ActiveLaneIndex).IsVectorOp;
                    set => SetLaneIsVectorOp(ActiveLaneIndex, value);
                }

                public bool VectorComplete
                {
                    get => GetLane(ActiveLaneIndex).VectorComplete;
                    set => SetLaneVectorComplete(ActiveLaneIndex, value);
                }

                public bool WritesRegister
                {
                    get => GetLane(ActiveLaneIndex).WritesRegister;
                    set => SetLaneWritesRegister(ActiveLaneIndex, value);
                }

                public ushort DestRegID
                {
                    get => GetLane(ActiveLaneIndex).DestRegID;
                    set => SetLaneDestRegID(ActiveLaneIndex, value);
                }

                public ulong DomainTag
                {
                    get => GetLane(ActiveLaneIndex).DomainTag;
                    set => SetLaneDomainTag(ActiveLaneIndex, value);
                }

                public int MshrScoreboardSlot
                {
                    get => GetLane(ActiveLaneIndex).MshrScoreboardSlot;
                    set => SetLaneMshrScoreboardSlot(ActiveLaneIndex, value);
                }

                public int MshrVirtualThreadId
                {
                    get => GetLane(ActiveLaneIndex).MshrVirtualThreadId;
                    set => SetLaneMshrVirtualThreadId(ActiveLaneIndex, value);
                }

                public Core.ResourceBitset ResourceMask
                {
                    get => GetLane(ActiveLaneIndex).ResourceMask;
                    set => SetLaneResourceMask(ActiveLaneIndex, value);
                }

                public ulong ResourceToken
                {
                    get => GetLane(ActiveLaneIndex).ResourceToken;
                    set => SetLaneResourceToken(ActiveLaneIndex, value);
                }

                public int OwnerThreadId
                {
                    get => GetLane(ActiveLaneIndex).OwnerThreadId;
                    set => SetLaneOwnerThreadId(ActiveLaneIndex, value);
                }

                public int VirtualThreadId
                {
                    get => GetLane(ActiveLaneIndex).VirtualThreadId;
                    set => SetLaneVirtualThreadId(ActiveLaneIndex, value);
                }

                /// <summary>
                /// Architectural privilege-domain identifier.
                /// Distinct from <c>VirtualThreadId</c> (SMT hardware-thread slot).
                /// </summary>
                public int OwnerContextId
                {
                    get => GetLane(ActiveLaneIndex).OwnerContextId;
                    set => SetLaneOwnerContextId(ActiveLaneIndex, value);
                }

                public bool WasFspInjected
                {
                    get => GetLane(ActiveLaneIndex).WasFspInjected;
                    set => SetLaneWasFspInjected(ActiveLaneIndex, value);
                }

                public int OriginalThreadId
                {
                    get => GetLane(ActiveLaneIndex).OriginalThreadId;
                    set => SetLaneOriginalThreadId(ActiveLaneIndex, value);
                }

                public ScalarMemoryLaneState GetLane(byte laneIndex) =>
                    PipelineLaneResolver.ResolveLaneOrThrow(laneIndex, nameof(MemoryStage)) switch
                {
                    0 => Lane0,
                    1 => Lane1,
                    2 => Lane2,
                    3 => Lane3,
                    4 => Lane4,
                    5 => Lane5,
                    6 => Lane6,
                    7 => Lane7,
                    _ => throw new InvalidPipelineLaneException(nameof(MemoryStage), laneIndex)
                };

                public void SetLane(byte laneIndex, ScalarMemoryLaneState lane)
                {
                    switch (PipelineLaneResolver.ResolveLaneOrThrow(laneIndex, nameof(MemoryStage)))
                    {
                        case 0:
                            Lane0 = lane;
                            break;
                        case 1:
                            Lane1 = lane;
                            break;
                        case 2:
                            Lane2 = lane;
                            break;
                        case 3:
                            Lane3 = lane;
                            break;
                        case 4:
                            Lane4 = lane;
                            break;
                        case 5:
                            Lane5 = lane;
                            break;
                        case 6:
                            Lane6 = lane;
                            break;
                        case 7:
                            Lane7 = lane;
                            break;
                    }
                }

                public void Clear()
                {
                    Valid = false;
                    ActiveLaneIndex = 0;
                    Lane0.Clear(0);
                    Lane1.Clear(1);
                    Lane2.Clear(2);
                    Lane3.Clear(3);
                    Lane4.Clear(4);
                    Lane5.Clear(5);
                    Lane6.Clear(6);
                    Lane7.Clear(7);
                    PreparedScalarMask = 0;
                    RefinedPreparedScalarMask = 0;
                    MaterializedScalarLaneCount = 0;
                    MaterializedPhysicalLaneCount = 0;
                    UsesExplicitPacketLanes = false;
                    RetainsReferenceSequentialPath = true;
                    SelectedNonScalarSlotMask = 0;
                    BlockedScalarCandidateMask = 0;
                    AdmissionExecutionMode = Core.RuntimeClusterAdmissionExecutionMode.Empty;
                }

                private void SetLanePC(byte laneIndex, ulong value)
                {
                    ScalarMemoryLaneState lane = GetLane(laneIndex);
                    lane.PC = value;
                    SetLane(laneIndex, lane);
                }

                private void SetLaneOpCode(byte laneIndex, uint value)
                {
                    ScalarMemoryLaneState lane = GetLane(laneIndex);
                    lane.OpCode = value;
                    SetLane(laneIndex, lane);
                }

                private void SetLaneResultValue(byte laneIndex, ulong value)
                {
                    ScalarMemoryLaneState lane = GetLane(laneIndex);
                    lane.ResultValue = value;
                    SetLane(laneIndex, lane);
                }

                private void SetLaneResultReady(byte laneIndex, bool value)
                {
                    ScalarMemoryLaneState lane = GetLane(laneIndex);
                    lane.ResultReady = value;
                    SetLane(laneIndex, lane);
                }

                private void SetLaneIsVectorOp(byte laneIndex, bool value)
                {
                    ScalarMemoryLaneState lane = GetLane(laneIndex);
                    lane.IsVectorOp = value;
                    SetLane(laneIndex, lane);
                }

                private void SetLaneVectorComplete(byte laneIndex, bool value)
                {
                    ScalarMemoryLaneState lane = GetLane(laneIndex);
                    lane.VectorComplete = value;
                    SetLane(laneIndex, lane);
                }

                private void SetLaneWritesRegister(byte laneIndex, bool value)
                {
                    ScalarMemoryLaneState lane = GetLane(laneIndex);
                    lane.WritesRegister = value;
                    SetLane(laneIndex, lane);
                }

                private void SetLaneDestRegID(byte laneIndex, ushort value)
                {
                    ScalarMemoryLaneState lane = GetLane(laneIndex);
                    lane.DestRegID = value;
                    SetLane(laneIndex, lane);
                }

                private void SetLaneDomainTag(byte laneIndex, ulong value)
                {
                    ScalarMemoryLaneState lane = GetLane(laneIndex);
                    lane.DomainTag = value;
                    SetLane(laneIndex, lane);
                }

                private void SetLaneMshrScoreboardSlot(byte laneIndex, int value)
                {
                    ScalarMemoryLaneState lane = GetLane(laneIndex);
                    lane.MshrScoreboardSlot = value;
                    SetLane(laneIndex, lane);
                }

                private void SetLaneMshrVirtualThreadId(byte laneIndex, int value)
                {
                    ScalarMemoryLaneState lane = GetLane(laneIndex);
                    lane.MshrVirtualThreadId = value;
                    SetLane(laneIndex, lane);
                }

                private void SetLaneResourceMask(byte laneIndex, Core.ResourceBitset value)
                {
                    ScalarMemoryLaneState lane = GetLane(laneIndex);
                    lane.ResourceMask = value;
                    SetLane(laneIndex, lane);
                }

                private void SetLaneResourceToken(byte laneIndex, ulong value)
                {
                    ScalarMemoryLaneState lane = GetLane(laneIndex);
                    lane.ResourceToken = value;
                    SetLane(laneIndex, lane);
                }

                private void SetLaneOwnerThreadId(byte laneIndex, int value)
                {
                    ScalarMemoryLaneState lane = GetLane(laneIndex);
                    lane.OwnerThreadId = value;
                    SetLane(laneIndex, lane);
                }

                private void SetLaneVirtualThreadId(byte laneIndex, int value)
                {
                    ScalarMemoryLaneState lane = GetLane(laneIndex);
                    lane.VirtualThreadId = value;
                    SetLane(laneIndex, lane);
                }

                private void SetLaneOwnerContextId(byte laneIndex, int value)
                {
                    ScalarMemoryLaneState lane = GetLane(laneIndex);
                    lane.OwnerContextId = value;
                    SetLane(laneIndex, lane);
                }

                private void SetLaneWasFspInjected(byte laneIndex, bool value)
                {
                    ScalarMemoryLaneState lane = GetLane(laneIndex);
                    lane.WasFspInjected = value;
                    SetLane(laneIndex, lane);
                }

                private void SetLaneOriginalThreadId(byte laneIndex, int value)
                {
                    ScalarMemoryLaneState lane = GetLane(laneIndex);
                    lane.OriginalThreadId = value;
                    SetLane(laneIndex, lane);
                }
            }
        }
    }
}
