using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core
{
    [System.Flags]
    public enum TrapPolicyClass : ulong
    {
        None = 0,
        Instruction = 1UL << 0,
        Csr = 1UL << 1,
        Memory = 1UL << 2,
        CompatibilityOperation = 1UL << 3,
        LaneOperation = 1UL << 4,
    }

    public sealed partial class TrapPolicyBitmap
    {
        private readonly HashSet<ushort> _instructionOpcodes = new();
        private readonly HashSet<ushort> _csrAddresses = new();
        private readonly List<MemoryTrapRange> _memoryRanges = new();
        private readonly HashSet<uint> _laneOperations = new();
        private ulong _enabledClasses;
        private ulong _compatibilityOperationMask;
        private uint _laneWideMask;

        public ulong PolicyEpoch { get; private set; }

        public TrapPolicyClass EnabledClasses => (TrapPolicyClass)_enabledClasses;

        public void Clear()
        {
            _instructionOpcodes.Clear();
            _csrAddresses.Clear();
            _memoryRanges.Clear();
            _laneOperations.Clear();
            _enabledClasses = 0;
            _compatibilityOperationMask = 0;
            _laneWideMask = 0;
            AdvanceEpoch();
        }

        public void EnableInstruction(ushort opcode)
        {
            if (_instructionOpcodes.Add(opcode))
            {
                _enabledClasses |= (ulong)TrapPolicyClass.Instruction;
                AdvanceEpoch();
            }
        }

        public void EnableCsr(ushort csrAddress)
        {
            if (_csrAddresses.Add(csrAddress))
            {
                _enabledClasses |= (ulong)TrapPolicyClass.Csr;
                AdvanceEpoch();
            }
        }

        public void EnableMemoryRange(
            ulong baseAddress,
            ulong length,
            TrapAccessMask accessMask)
        {
            MemoryTrapRange range = new(baseAddress, length, accessMask);
            if (!range.IsValid)
            {
                return;
            }

            _memoryRanges.Add(range);
            _enabledClasses |= (ulong)TrapPolicyClass.Memory;
            AdvanceEpoch();
        }

        public void EnableCompatibilityOperation(byte operation)
        {
            ulong mask = OperationBit(operation);
            if (mask == 0 || (_compatibilityOperationMask & mask) != 0)
            {
                return;
            }

            _compatibilityOperationMask |= mask;
            _enabledClasses |= (ulong)TrapPolicyClass.CompatibilityOperation;
            AdvanceEpoch();
        }

        public bool InterceptsCompatibilityOperation(byte operation)
        {
            ulong mask = OperationBit(operation);
            return mask != 0 && (_compatibilityOperationMask & mask) != 0;
        }

        public void EnableLaneOperation(byte laneId, ushort opcode)
        {
            if (laneId is not (6 or 7))
            {
                return;
            }

            uint key = LaneOperationKey(laneId, opcode);
            if (_laneOperations.Add(key))
            {
                _enabledClasses |= (ulong)TrapPolicyClass.LaneOperation;
                AdvanceEpoch();
            }
        }

        public void EnableLane(byte laneId)
        {
            if (laneId is not (6 or 7))
            {
                return;
            }

            uint mask = 1u << laneId;
            if ((_laneWideMask & mask) != 0)
            {
                return;
            }

            _laneWideMask |= mask;
            _enabledClasses |= (ulong)TrapPolicyClass.LaneOperation;
            AdvanceEpoch();
        }

        public NeutralTrapResult Evaluate(in TrapRequest request)
        {
            return request.TargetKind switch
            {
                TrapTargetKind.InstructionOpcode
                    when (_enabledClasses & (ulong)TrapPolicyClass.Instruction) != 0 &&
                         _instructionOpcodes.Contains(unchecked((ushort)request.Target)) =>
                    NeutralTrapResult.Trap(request, NeutralTrapResultKind.InstructionIntercept),

                TrapTargetKind.CsrAddress
                    when (_enabledClasses & (ulong)TrapPolicyClass.Csr) != 0 &&
                         _csrAddresses.Contains(unchecked((ushort)request.Target)) =>
                    NeutralTrapResult.Trap(request, NeutralTrapResultKind.CsrIntercept),

                TrapTargetKind.CompatibilityOperation
                    when (_enabledClasses & (ulong)TrapPolicyClass.CompatibilityOperation) != 0 &&
                         InterceptsCompatibilityOperation(unchecked((byte)request.Target)) =>
                    NeutralTrapResult.Trap(request, NeutralTrapResultKind.CompatibilityOperationIntercept),

                TrapTargetKind.MemoryRange
                    when (_enabledClasses & (ulong)TrapPolicyClass.Memory) != 0 &&
                         InterceptsMemory(request.Target, request.Auxiliary, request.AccessType) =>
                    NeutralTrapResult.Trap(request, NeutralTrapResultKind.MemoryIntercept),

                TrapTargetKind.LaneOperation
                    when (_enabledClasses & (ulong)TrapPolicyClass.LaneOperation) != 0 &&
                         InterceptsLaneOperation(unchecked((byte)request.Target), unchecked((ushort)request.Auxiliary)) =>
                    NeutralTrapResult.Trap(request, NeutralTrapResultKind.LaneOperationIntercept),

                _ => NeutralTrapResult.Continue(request),
            };
        }

        private void AdvanceEpoch()
        {
            unchecked
            {
                PolicyEpoch++;
            }
        }

        private static ulong OperationBit(byte operation)
        {
            return operation is 0 or >= 64
                ? 0
                : 1UL << operation;
        }

        private bool InterceptsMemory(
            ulong address,
            ulong length,
            TrapAccessType accessType)
        {
            for (int index = 0; index < _memoryRanges.Count; index++)
            {
                if (_memoryRanges[index].Matches(address, length, accessType))
                {
                    return true;
                }
            }

            return false;
        }

        private bool InterceptsLaneOperation(byte laneId, ushort opcode)
        {
            if (laneId is not (6 or 7))
            {
                return false;
            }

            return (_laneWideMask & (1u << laneId)) != 0 ||
                   _laneOperations.Contains(LaneOperationKey(laneId, opcode));
        }

        private static uint LaneOperationKey(byte laneId, ushort opcode) =>
            ((uint)laneId << 16) | opcode;
    }
}
