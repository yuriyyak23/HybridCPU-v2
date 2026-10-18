namespace YAKSys_Hybrid_CPU.Core
{
    public sealed partial class VirtualTimerState
    {
        public bool TryConsumeExpired(
            ulong nowCycle,
            out EventInjectionDescriptor descriptor)
        {
            if (!Armed || nowCycle < DeadlineCycle)
            {
                descriptor = default;
                return false;
            }

            Armed = false;
            AdvanceEpoch();
            descriptor = EventInjectionDescriptor.Create(
                EventInjectionKind.VirtualTimer,
                Vector,
                TargetVtId,
                ExecutionDomainTag,
                AddressSpaceTag,
                Priority,
                DeadlineCycle,
                posted: false);
            return true;
        }
    }
}
