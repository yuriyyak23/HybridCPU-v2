namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Reference-owned containment for the current decode latch and bundle-scoped
    /// decode progress. Admission, scheduling and publication authority remain with
    /// their existing owners.
    /// </summary>
    internal sealed class DecodeState
    {
        internal Processor.CPU_Core.DecodeStage Decode;
        internal byte PipelineBundleSlot;
        internal DecodedBundleRuntimeState BundleRuntime;
        internal BundleProgressState BundleProgress;
        internal DecodedBundleDerivedIssuePlanState DerivedIssuePlan;
        internal ulong BundleStateEpochCounter;
        internal ulong BundleStateVersionCounter;
        internal ClusterIssuePreparation ClusterPreparation = null!;
        internal bool BundleDecodedAndPacked;
    }
}
