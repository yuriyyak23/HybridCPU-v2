using System;

namespace YAKSys_Hybrid_CPU.Core.Registers.Retire
{
    /// <summary>
    /// Applies retire records to the unified committed architectural state.
    /// </summary>
    public sealed class RetireCoordinator
    {
        private readonly PhysicalRegisterFile? _physicalRegisters;
        private readonly RenameMap? _archRenameMap;
        private readonly CommitMap? _archCommitMap;
        private readonly ArchContextState[]? _archContexts;

        public RetireCoordinator(
            PhysicalRegisterFile physicalRegisters,
            RenameMap archRenameMap,
            CommitMap archCommitMap,
            ArchContextState[] archContexts)
        {
            _physicalRegisters = physicalRegisters ?? throw new ArgumentNullException(nameof(physicalRegisters));
            _archRenameMap = archRenameMap ?? throw new ArgumentNullException(nameof(archRenameMap));
            _archCommitMap = archCommitMap ?? throw new ArgumentNullException(nameof(archCommitMap));
            _archContexts = archContexts ?? throw new ArgumentNullException(nameof(archContexts));
            VtCount = _archContexts.Length;
        }

        public int VtCount { get; }

        public void Retire(in RetireRecord record)
        {
            ValidateRecord(record);
            ApplyRetireRecord(record);
        }

        public void Retire(ReadOnlySpan<RetireRecord> records)
        {
            for (int i = 0; i < records.Length; i++)
                Retire(records[i]);
        }

        private void ApplyRetireRecord(in RetireRecord record)
        {
            switch (record.Kind)
            {
                case RetireRecordKind.RegisterWrite:
                    ApplyRegisterWrite(record);
                    break;

                case RetireRecordKind.PcWrite:
                    ApplyPcWrite(record);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(record), record.Kind, "Unsupported retire record kind.");
            }
        }

        private void ApplyRegisterWrite(in RetireRecord record)
        {
            if (_archContexts == null || _archRenameMap == null || _archCommitMap == null || _physicalRegisters == null)
                return;

            if (record.ArchReg == 0)
                return;

            int vtId = record.VtId;
            ArchContextState archContext = _archContexts[vtId];
            archContext.CommittedRegs[record.ArchReg] = record.Value;

            int physReg = _archRenameMap.Lookup(vtId, record.ArchReg);
            if (physReg != 0)
                _physicalRegisters.Write(physReg, record.Value);

            _archCommitMap.Commit(vtId, record.ArchReg, physReg == 0 ? record.ArchReg : physReg);
        }

        private void ApplyPcWrite(in RetireRecord record)
        {
            if (_archContexts == null)
                return;

            _archContexts[record.VtId].CommittedPc = record.Value;
        }

        private void ValidateRecord(in RetireRecord record)
        {
            if ((uint)record.VtId >= (uint)VtCount)
            {
                int maxVtId = Math.Max(VtCount - 1, 0);
                throw new ArgumentOutOfRangeException(
                    nameof(record),
                    record.VtId,
                    $"Retire record VT identifier {record.VtId} is outside the range [0, {maxVtId}].");
            }

            if (record.Kind == RetireRecordKind.RegisterWrite &&
                (uint)record.ArchReg >= (uint)RenameMap.ArchRegs)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(record),
                    record.ArchReg,
                    $"Retire record architectural register {record.ArchReg} is outside the range [0, {RenameMap.ArchRegs - 1}].");
            }
        }
    }
}
