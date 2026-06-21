using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Core
{
    internal static class MicroOpSchedulerTestExtensions
    {
        public static MicroOp[] PackBundleIntraCoreSmt(
            this MicroOpScheduler scheduler,
            MicroOp[] bundle,
            int ownerVirtualThreadId,
            int localCoreId)
        {
            return scheduler.PackBundleIntraCoreSmt(
                bundle,
                ownerVirtualThreadId,
                localCoreId,
                MicroOpScheduler.AllEligibleVirtualThreadMask);
        }
    }
}
