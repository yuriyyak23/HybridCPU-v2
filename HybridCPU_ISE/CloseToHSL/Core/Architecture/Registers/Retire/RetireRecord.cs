namespace YAKSys_Hybrid_CPU.Core.Registers.Retire
{
    public enum RetireRecordKind
    {
        RegisterWrite,
        PcWrite,
    }

    /// <summary>
    /// Typed retire payload emitted by commit/writeback paths before architectural state is updated.
    /// </summary>
    public readonly struct RetireRecord
    {
        private RetireRecord(RetireRecordKind kind, int vtId, int archReg, ulong value)
        {
            Kind = kind;
            VtId = vtId;
            ArchReg = archReg;
            Value = value;
        }

        public RetireRecordKind Kind { get; }

        public int VtId { get; }

        public int ArchReg { get; }

        public ulong Value { get; }

        public bool IsRegisterWrite => Kind == RetireRecordKind.RegisterWrite;

        public bool IsPcWrite => Kind == RetireRecordKind.PcWrite;

        public static RetireRecord RegisterWrite(int vtId, int archReg, ulong value) =>
            new(RetireRecordKind.RegisterWrite, vtId, archReg, value);

        public static RetireRecord PcWrite(int vtId, ulong pc) =>
            new(RetireRecordKind.PcWrite, vtId, archReg: 0, pc);
    }
}
