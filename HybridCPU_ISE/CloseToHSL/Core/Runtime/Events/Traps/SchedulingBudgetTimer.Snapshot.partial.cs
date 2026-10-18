namespace YAKSys_Hybrid_CPU.Core
{
    public sealed partial class SchedulingBudgetTimer
    {
        public SchedulingBudgetTimerSnapshot CreateSnapshot() =>
            new(
                Armed,
                DeadlineCycle,
                TimerEpoch,
                TargetVtId,
                ExecutionDomainTag,
                AddressSpaceTag,
                Lane7PressurePreemptionArmed,
                Lane7PressureEpoch);

        public void RestoreSnapshot(SchedulingBudgetTimerSnapshot snapshot)
        {
            Armed = snapshot.Armed;
            DeadlineCycle = snapshot.DeadlineCycle;
            TimerEpoch = snapshot.TimerEpoch;
            TargetVtId = snapshot.TargetVtId;
            ExecutionDomainTag = snapshot.ExecutionDomainTag;
            AddressSpaceTag = snapshot.AddressSpaceTag;
            Lane7PressurePreemptionArmed = snapshot.Lane7PressurePreemptionArmed;
            Lane7PressureEpoch = snapshot.Lane7PressureEpoch;
        }
    }
}
