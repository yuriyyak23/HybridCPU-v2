namespace YAKSys_Hybrid_CPU
{
    public partial class PodController
    {
        public readonly struct AssistOwnerSnapshot
        {
            public AssistOwnerSnapshot(
                int virtualThreadId,
                int ownerContextId,
                ulong domainTag,
                ulong assistEpochId)
            {
                IsPublished = true;
                VirtualThreadId = virtualThreadId;
                OwnerContextId = ownerContextId;
                DomainTag = domainTag;
                AssistEpochId = assistEpochId;
            }

            public bool IsPublished { get; }

            public int VirtualThreadId { get; }

            public int OwnerContextId { get; }

            public ulong DomainTag { get; }

            public ulong AssistEpochId { get; }

            public bool IsValid => IsPublished;

            public bool Matches(int virtualThreadId, int ownerContextId, ulong domainTag)
            {
                return VirtualThreadId == virtualThreadId &&
                       OwnerContextId == ownerContextId &&
                       DomainTag == domainTag;
            }
        }

        private readonly AssistOwnerSnapshot[] _assistOwnerSnapshots = CreateEmptyAssistOwnerSnapshots();

        public void PublishAssistOwnerSnapshot(
            int localCoreId,
            int virtualThreadId,
            int ownerContextId,
            ulong domainTag,
            ulong assistEpochId = 0)
        {
            if ((uint)localCoreId >= CORES_PER_POD)
                return;

            _assistOwnerSnapshots[localCoreId] =
                new AssistOwnerSnapshot(virtualThreadId, ownerContextId, domainTag, assistEpochId);
            _scheduler.ReconcileInterCoreAssistTransportOwnerSnapshot(
                localCoreId,
                PodId,
                ownerSnapshotValid: true,
                ownerSnapshot: _assistOwnerSnapshots[localCoreId]);
        }

        public void InvalidateAssistOwnerSnapshot(int localCoreId)
        {
            if ((uint)localCoreId >= CORES_PER_POD)
                return;

            _assistOwnerSnapshots[localCoreId] = default;
            _scheduler.ReconcileInterCoreAssistTransportOwnerSnapshot(
                localCoreId,
                PodId,
                ownerSnapshotValid: false,
                ownerSnapshot: _assistOwnerSnapshots[localCoreId]);
        }

        public bool TryGetAssistOwnerSnapshot(int localCoreId, out AssistOwnerSnapshot snapshot)
        {
            snapshot = default;
            if ((uint)localCoreId >= CORES_PER_POD)
                return false;

            snapshot = _assistOwnerSnapshots[localCoreId];
            return snapshot.IsValid;
        }

        private static AssistOwnerSnapshot[] CreateEmptyAssistOwnerSnapshots()
        {
            return new AssistOwnerSnapshot[CORES_PER_POD];
        }
    }
}
