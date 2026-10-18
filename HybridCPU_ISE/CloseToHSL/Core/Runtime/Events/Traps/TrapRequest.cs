namespace YAKSys_Hybrid_CPU.Core
{
    public enum TrapTargetKind : byte
    {
        None = 0,
        InstructionOpcode = 1,
        CsrAddress = 2,
        MemoryRange = 3,
        CompatibilityOperation = 4,
        LaneOperation = 5,
        VirtualTimer = 6,
        PreemptionTimer = 7,
    }

    public enum TrapAccessType : byte
    {
        None = 0,
        Read = 1,
        Write = 2,
        Execute = 3,
        CompatibilityOperation = 4,
        LaneOperation = 5,
        TimerExpiry = 6,
    }

    [System.Flags]
    public enum TrapAccessMask : byte
    {
        None = 0,
        Read = 1 << 0,
        Write = 1 << 1,
        Execute = 1 << 2,
        CompatibilityOperation = 1 << 3,
        LaneOperation = 1 << 4,
        TimerExpiry = 1 << 5,
        All = Read | Write | Execute | CompatibilityOperation | LaneOperation | TimerExpiry,
    }

    public readonly record struct MemoryTrapRange(
        ulong BaseAddress,
        ulong Length,
        TrapAccessMask AccessMask)
    {
        public bool IsValid => Length != 0 && AccessMask != TrapAccessMask.None;

        public bool Matches(ulong address, ulong length, TrapAccessType accessType)
        {
            if (!IsValid || (AccessMask & ToMask(accessType)) == 0)
            {
                return false;
            }

            ulong end = SaturatingEnd(address, length);
            ulong rangeEnd = SaturatingEnd(BaseAddress, Length);
            return address < rangeEnd && end > BaseAddress;
        }

        private static ulong SaturatingEnd(ulong address, ulong length)
        {
            if (length == 0)
            {
                length = 1;
            }

            ulong end = address + length;
            return end < address ? ulong.MaxValue : end;
        }

        private static TrapAccessMask ToMask(TrapAccessType accessType) =>
            accessType switch
            {
                TrapAccessType.Read => TrapAccessMask.Read,
                TrapAccessType.Write => TrapAccessMask.Write,
                TrapAccessType.Execute => TrapAccessMask.Execute,
                TrapAccessType.CompatibilityOperation => TrapAccessMask.CompatibilityOperation,
                TrapAccessType.LaneOperation => TrapAccessMask.LaneOperation,
                TrapAccessType.TimerExpiry => TrapAccessMask.TimerExpiry,
                _ => TrapAccessMask.None,
            };
    }

    public readonly partial record struct TrapRequest(
        TrapTargetKind TargetKind,
        ulong Target,
        ulong Auxiliary,
        TrapAccessType AccessType,
        byte VtId,
        ushort ExecutionDomainTag,
        ushort AddressSpaceTag)
    {
        public static TrapRequest ForInstruction(
            ushort opcode,
            byte vtId,
            ushort executionDomainTag = 0,
            ushort addressSpaceTag = 0) =>
            new(
                TrapTargetKind.InstructionOpcode,
                opcode,
                0,
                TrapAccessType.Execute,
                vtId,
                executionDomainTag,
                addressSpaceTag);

        public static TrapRequest ForCsr(
            ushort csrAddress,
            TrapAccessType accessType,
            byte vtId,
            ushort executionDomainTag = 0,
            ushort addressSpaceTag = 0) =>
            new(
                TrapTargetKind.CsrAddress,
                csrAddress,
                0,
                accessType,
                vtId,
                executionDomainTag,
                addressSpaceTag);

        public static TrapRequest ForCompatibilityOperation(
            byte operation,
            ushort opcode,
            byte vtId,
            ushort executionDomainTag = 0,
            ushort addressSpaceTag = 0) =>
            new(
                TrapTargetKind.CompatibilityOperation,
                operation,
                opcode,
                TrapAccessType.CompatibilityOperation,
                vtId,
                executionDomainTag,
                addressSpaceTag);

        public static TrapRequest ForMemory(
            ulong address,
            ulong length,
            TrapAccessType accessType,
            byte vtId,
            ushort executionDomainTag = 0,
            ushort addressSpaceTag = 0) =>
            new(
                TrapTargetKind.MemoryRange,
                address,
                length,
                accessType,
                vtId,
                executionDomainTag,
                addressSpaceTag);

        public static TrapRequest ForLaneOperation(
            byte laneId,
            ushort opcode,
            byte vtId,
            ushort executionDomainTag = 0,
            ushort addressSpaceTag = 0) =>
            new(
                TrapTargetKind.LaneOperation,
                laneId,
                opcode,
                TrapAccessType.LaneOperation,
                vtId,
                executionDomainTag,
                addressSpaceTag);

        public static TrapRequest ForPreemptionTimer(
            byte vtId,
            ushort executionDomainTag,
            ushort addressSpaceTag,
            ulong deadline) =>
            new(
                TrapTargetKind.PreemptionTimer,
                deadline,
                0,
                TrapAccessType.TimerExpiry,
                vtId,
                executionDomainTag,
                addressSpaceTag);
    }
}
