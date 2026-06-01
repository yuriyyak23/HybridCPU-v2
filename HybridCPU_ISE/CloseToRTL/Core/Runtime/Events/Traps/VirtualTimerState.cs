namespace YAKSys_Hybrid_CPU.Core
{
    public sealed partial class VirtualTimerState
    {
        public bool Armed { get; private set; }

        public ulong DeadlineCycle { get; private set; }

        public ulong TimerEpoch { get; private set; }

        public byte TargetVtId { get; private set; }

        public ushort ExecutionDomainTag { get; private set; }

        public ushort AddressSpaceTag { get; private set; }

        public byte Vector { get; private set; }

        public byte Priority { get; private set; }

        public void Arm(
            ulong nowCycle,
            ulong deltaCycles,
            byte targetVtId,
            ushort executionDomainTag,
            ushort addressSpaceTag,
            byte vector,
            byte priority = 0)
        {
            Armed = true;
            DeadlineCycle = nowCycle + deltaCycles;
            TargetVtId = targetVtId;
            ExecutionDomainTag = executionDomainTag;
            AddressSpaceTag = addressSpaceTag;
            Vector = vector;
            Priority = priority;
            AdvanceEpoch();
        }

        private void AdvanceEpoch()
        {
            unchecked
            {
                TimerEpoch++;
            }
        }
    }
}
