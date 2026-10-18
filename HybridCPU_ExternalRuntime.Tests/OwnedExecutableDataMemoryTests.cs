namespace HybridCPU.ExternalRuntime.Tests;

public sealed class OwnedExecutableDataMemoryTests
{
    [Fact]
    public void CrossPageCapacityRefusalPreservesExistingBytesAndPageInventory()
    {
        var memory = new OwnedExecutableDataMemory(16384, 4096);
        Assert.True(memory.TryWritePhysicalRange(4095, [7]));
        Assert.False(memory.TryWritePhysicalRange(4095, [9, 11]));
        Assert.Equal(4096UL, memory.AllocatedDataBytes);
        byte[] read = new byte[2];
        Assert.True(memory.TryReadPhysicalRange(4095, read));
        Assert.Equal(new byte[] { 7, 0 }, read);
        Assert.False(memory.TryWritePhysicalRange(ulong.MaxValue, [1]));
        Assert.True(memory.TryWritePhysicalRange(16384, []));
        memory.Retire();
        Assert.True(memory.Retired);
        Assert.Equal(0UL, memory.AllocatedDataBytes);
        Assert.False(memory.TryReadPhysicalRange(0, read));
        Assert.False(memory.TryWritePhysicalRange(0, [1]));
        memory.Retire();
        Assert.Equal(0UL, memory.AllocatedDataBytes);
    }

    [Fact]
    public void ConcurrentAllocationCannotExceedQuotaAndRetirementDoesNotCloseForeignOwner()
    {
        var original = new OwnedExecutableDataMemory(16384, 4096);
        var foreign = new OwnedExecutableDataMemory(16384, 4096);
        int accepted = 0;
        Parallel.For(0, 4, page =>
        {
            if (original.TryWritePhysicalRange((ulong)page * 4096, [(byte)(page + 1)]))
                Interlocked.Increment(ref accepted);
        });
        Assert.Equal(1, accepted);
        Assert.Equal(4096UL, original.AllocatedDataBytes);
        Assert.True(foreign.TryWritePhysicalRange(0, [42]));
        Parallel.Invoke(original.Retire, () => original.TryWritePhysicalRange(0, [5]));
        Assert.True(original.Retired);
        Assert.Equal(0UL, original.AllocatedDataBytes);
        Assert.False(foreign.Retired);
        Assert.Equal(4096UL, foreign.AllocatedDataBytes);
        Assert.Equal(42UL, foreign.ReadPhysicalWord(0));
    }
}
