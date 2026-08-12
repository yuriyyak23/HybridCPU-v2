using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.SecureComputeDiagnostics;

internal sealed class DescriptorMapScenario : ISecureComputeScenario
{
    public string Id => "descriptor-maps";
    public string Description => "Checks order dependence for overlapping secure-memory regions and shared I/O buffers.";
    public DiagnosticSurfaceKind SurfaceKind => DiagnosticSurfaceKind.RuntimeContract;
    public string AuthorityCeiling => "Descriptor lookup behavior only; no enforced memory-controller, DMA or IOMMU path proof.";

    public Task ExecuteAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        var epoch = new SecureRevocationEpoch(7);
        var privateRegion = new SecureMemoryRegionDescriptor(
            SecureMemoryRegionClass.Private, 0x1000, 0x1000, SecureMemoryHostVisibility.Denied, 7);
        var sharedRegion = new SecureMemoryRegionDescriptor(
            SecureMemoryRegionClass.Shared, 0x1800, 0x1000, SecureMemoryHostVisibility.ExplicitShared, 7);
        var grant = new SecureGrantHandle(SecureGrantHandleKind.IoPolicy, 1, 0xB6, 7);
        var firstBuffer = new SecureSharedBufferDescriptor(
            1, 0x1000, 0x1000, SecureSharedBufferDirection.Bidirectional, grant,
            SecureEvidenceVisibilityClass.GuestVisible, 7, 7);
        var secondBuffer = new SecureSharedBufferDescriptor(
            2, 0x1800, 0x1000, SecureSharedBufferDirection.Bidirectional, grant,
            SecureEvidenceVisibilityClass.GuestVisible, 7, 7);

        for (int iteration = 0; iteration < context.Profile.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var memoryAB = new SecureMemoryDomainDescriptor(7, 9, epoch, [privateRegion, sharedRegion]);
            var memoryBA = new SecureMemoryDomainDescriptor(7, 9, epoch, [sharedRegion, privateRegion]);
            context.Check(memoryAB.TryFindRegion(0x1900, 0x10, out SecureMemoryRegionDescriptor regionAB), "overlap resolves in AB map");
            context.Check(memoryBA.TryFindRegion(0x1900, 0x10, out SecureMemoryRegionDescriptor regionBA), "overlap resolves in BA map");
            context.Check(regionAB.RegionClass != regionBA.RegionClass, "memory lookup is first-match and order-dependent");

            var ioAB = new SecureIoDomainDescriptor(SecureIoDmaPolicy.ExplicitSharedBuffersOnly, [firstBuffer, secondBuffer], true, true);
            var ioBA = new SecureIoDomainDescriptor(SecureIoDmaPolicy.ExplicitSharedBuffersOnly, [secondBuffer, firstBuffer], true, true);
            context.Check(ioAB.TryFindSharedBuffer(0x1900, 0x10, SecureSharedBufferDirection.DomainToDevice, 7, epoch, out SecureSharedBufferDescriptor bufferAB), "overlap resolves in AB I/O map");
            context.Check(ioBA.TryFindSharedBuffer(0x1900, 0x10, SecureSharedBufferDirection.DomainToDevice, 7, epoch, out SecureSharedBufferDescriptor bufferBA), "overlap resolves in BA I/O map");
            context.Check(bufferAB.BufferId != bufferBA.BufferId, "shared-buffer lookup is first-match and order-dependent");
            context.Count("order_dependent_memory_lookups");
            context.Count("order_dependent_shared_buffer_lookups");
            context.Trace("map-overlap", ("memoryAB", regionAB.RegionClass), ("memoryBA", regionBA.RegionClass), ("bufferAB", bufferAB.BufferId), ("bufferBA", bufferBA.BufferId));
            context.CompleteIteration("Descriptor map overlap completed.");
        }

        context.Finding(
            "C1-CANONICAL-MAPS",
            DiagnosticSeverity.Blocker,
            "Overlapping maps are order-dependent",
            "Region and shared-buffer constructors accept overlapping entries and lookups return the first match. Canonical non-overlap validation and a unique map owner are still required.");
        context.Finding(
            "C1-IOMMU-PATH",
            DiagnosticSeverity.Blocker,
            "Shared-buffer policy is not device authority",
            "Successful descriptor lookup does not prove a production DMA/IOMMU programming owner or effect-path enforcement.");
        return Task.CompletedTask;
    }
}
