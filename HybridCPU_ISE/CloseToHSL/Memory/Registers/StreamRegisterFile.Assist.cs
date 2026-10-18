using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Memory
{
    public partial class StreamRegisterFile
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanAllocateAssistRegister(
            ulong sourceAddr,
            byte elementSize,
            AssistStreamRegisterPartitionPolicy policy,
            out AssistStreamRegisterRejectKind rejectKind)
        {
            rejectKind = AssistStreamRegisterRejectKind.None;
            if (TryFindAssistReuseRegister(sourceAddr, elementSize, elementCount: 1, out _))
            {
                return true;
            }

            return TrySelectAssistRegisterSlot(policy, out _, out rejectKind);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAllocateAssistRegister(
            ulong sourceAddr,
            byte elementSize,
            uint elementCount,
            AssistStreamRegisterPartitionPolicy policy,
            out int registerIndex,
            out AssistStreamRegisterRejectKind rejectKind)
        {
            rejectKind = AssistStreamRegisterRejectKind.None;
            if (TryFindAssistReuseRegister(sourceAddr, elementSize, elementCount, out registerIndex))
            {
                return true;
            }

            if (!TrySelectAssistRegisterSlot(policy, out registerIndex, out rejectKind))
            {
                return false;
            }

            ref RegisterEntry reg = ref registers[registerIndex];
            if (reg.State == RegisterState.Dirty)
            {
                totalEvictions++;
            }

            reg.State = RegisterState.Invalid;
            reg.SourceAddress = sourceAddr;
            reg.ElementSize = elementSize;
            reg.ElementCount = elementCount;
            reg.ValidBytes = 0;
            reg.LastAccessTime = ++accessCounter;
            reg.AssistOwned = true;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkAssistLoading(int regIndex)
        {
            if (regIndex < 0 || regIndex >= registerCount)
            {
                return;
            }

            if (registers[regIndex].State == RegisterState.Invalid)
            {
                registers[regIndex].State = RegisterState.Loading;
            }

            registers[regIndex].AssistOwned = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CountAssistOwnedRegisters()
        {
            int count = 0;
            for (int i = 0; i < registerCount; i++)
            {
                if (registers[i].AssistOwned && registers[i].State != RegisterState.Invalid)
                {
                    count++;
                }
            }

            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CountAssistOwnedRegisters(RegisterState state)
        {
            int count = 0;
            for (int i = 0; i < registerCount; i++)
            {
                if (registers[i].AssistOwned && registers[i].State == state)
                {
                    count++;
                }
            }

            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryFindAssistReuseRegister(
            ulong sourceAddr,
            byte elementSize,
            uint elementCount,
            out int registerIndex)
        {
            registerIndex = -1;
            uint requestedBytes = ComputeRequestedByteCount(elementSize, elementCount);

            for (int i = 0; i < registerCount; i++)
            {
                uint trackedBytes = GetTrackedByteCount(registers[i]);
                if ((registers[i].State == RegisterState.Valid ||
                     registers[i].State == RegisterState.Loading) &&
                    registers[i].SourceAddress == sourceAddr &&
                    registers[i].ElementSize == elementSize &&
                    trackedBytes >= requestedBytes)
                {
                    registerIndex = i;
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TrySelectAssistRegisterSlot(
            AssistStreamRegisterPartitionPolicy policy,
            out int registerIndex,
            out AssistStreamRegisterRejectKind rejectKind)
        {
            registerIndex = -1;
            rejectKind = AssistStreamRegisterRejectKind.None;

            if (CountAssistOwnedRegisters(RegisterState.Loading) >= policy.LoadingRegisterBudget)
            {
                rejectKind = AssistStreamRegisterRejectKind.LoadingBudget;
                return false;
            }

            int invalidIndex = FindEmptyRegisterSlot();
            if (invalidIndex >= 0)
            {
                if (CountAssistOwnedRegisters() >= policy.ResidentRegisterBudget)
                {
                    rejectKind = AssistStreamRegisterRejectKind.ResidentBudget;
                    return false;
                }

                registerIndex = invalidIndex;
                return true;
            }

            int victimIndex = FindAssistVictimRegister();
            if (victimIndex >= 0)
            {
                registerIndex = victimIndex;
                return true;
            }

            rejectKind = CountAssistOwnedRegisters() >= policy.ResidentRegisterBudget
                ? AssistStreamRegisterRejectKind.ResidentBudget
                : AssistStreamRegisterRejectKind.NoAssistVictim;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindEmptyRegisterSlot()
        {
            for (int i = 0; i < registerCount; i++)
            {
                if (registers[i].State == RegisterState.Invalid)
                {
                    return i;
                }
            }

            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindAssistVictimRegister()
        {
            int victimIndex = -1;
            ulong oldestTime = ulong.MaxValue;

            for (int i = 0; i < registerCount; i++)
            {
                if (!registers[i].AssistOwned || registers[i].State == RegisterState.Loading)
                {
                    continue;
                }

                if (registers[i].State != RegisterState.Invalid &&
                    registers[i].LastAccessTime < oldestTime)
                {
                    oldestTime = registers[i].LastAccessTime;
                    victimIndex = i;
                }
            }

            return victimIndex;
        }
    }
}
