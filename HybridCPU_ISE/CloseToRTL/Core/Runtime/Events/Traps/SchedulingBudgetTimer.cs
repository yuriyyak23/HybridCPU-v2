namespace YAKSys_Hybrid_CPU.Core
{
    public readonly record struct SchedulingBudgetTimerSnapshot(
        bool Armed,
        ulong DeadlineCycle,
        ulong TimerEpoch,
        byte TargetVtId,
        ushort ExecutionDomainTag,
        ushort AddressSpaceTag,
        bool Lane7PressurePreemptionArmed,
        ulong Lane7PressureEpoch);

    public sealed partial class SchedulingBudgetTimer
    {
        public bool Armed { get; private set; }

        public ulong DeadlineCycle { get; private set; }

        public ulong TimerEpoch { get; private set; }

        public byte TargetVtId { get; private set; }

        public ushort ExecutionDomainTag { get; private set; }

        public ushort AddressSpaceTag { get; private set; }

        public bool Lane7PressurePreemptionArmed { get; private set; }

        public ulong Lane7PressureEpoch { get; private set; }

        public void Arm(
            ulong nowCycle,
            ulong deltaCycles,
            byte targetVtId,
            ushort executionDomainTag,
            ushort addressSpaceTag)
        {
            Armed = true;
            DeadlineCycle = nowCycle + deltaCycles;
            TargetVtId = targetVtId;
            ExecutionDomainTag = executionDomainTag;
            AddressSpaceTag = addressSpaceTag;
            Lane7PressurePreemptionArmed = false;
            AdvanceEpoch();
        }

        public void ArmForLane7Pressure(
            ulong nowCycle,
            ulong deltaCycles,
            Lane7PressureSnapshot pressure)
        {
            Armed = true;
            Lane7PressurePreemptionArmed = true;
            DeadlineCycle = nowCycle + deltaCycles;
            TargetVtId = checked((byte)pressure.OwnerVirtualThreadId);
            ExecutionDomainTag = pressure.ExecutionDomainTag;
            AddressSpaceTag = pressure.AddressSpaceTag;
            Lane7PressureEpoch = pressure.PressureEpoch;
            AdvanceEpoch();
        }

        public bool TryConsumeExpired(
            ulong nowCycle,
            out NeutralTrapResult result)
        {
            if (!Armed || nowCycle < DeadlineCycle)
            {
                result = NeutralTrapResult.Continue(
                    TrapRequest.ForPreemptionTimer(TargetVtId, ExecutionDomainTag, AddressSpaceTag, DeadlineCycle));
                return false;
            }

            Armed = false;
            Lane7PressurePreemptionArmed = false;
            AdvanceEpoch();
            TrapRequest request = TrapRequest.ForPreemptionTimer(
                TargetVtId,
                ExecutionDomainTag,
                AddressSpaceTag,
                DeadlineCycle);
            result = NeutralTrapResult.Trap(request, NeutralTrapResultKind.PreemptionTimerExpired);
            return true;
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
