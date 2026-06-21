using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU
{
    public partial class PodController
    {
        public bool TryPeekInterCoreAssistTransport(
            int localCoreId,
            out AssistInterCoreTransport transport)
        {
            transport = default;
            return (uint)localCoreId < CORES_PER_POD &&
                   _scheduler.TryPeekInterCoreAssistTransport(localCoreId, out transport);
        }

        public bool TryConsumeInterCoreAssistTransport(
            int localCoreId,
            out AssistInterCoreTransport transport)
        {
            transport = default;
            return (uint)localCoreId < CORES_PER_POD &&
                   _scheduler.TryConsumeInterCoreAssistTransport(localCoreId, out transport);
        }

        internal void ClearInterCoreAssistTransport(int localCoreId)
        {
            if ((uint)localCoreId >= CORES_PER_POD)
            {
                return;
            }

            _scheduler.ClearInterCoreAssistNominationPort(localCoreId);
        }
    }
}
