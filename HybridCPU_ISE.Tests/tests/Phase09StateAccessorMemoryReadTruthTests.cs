using System;
using HybridCPU_ISE.Legacy;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09StateAccessorMemoryReadTruthTests
{
    [Fact]
    public void ReadMemory_WhenLegacyStrictSourceStartsBeyondMainMemoryLength_ThrowsTypedFault()
    {
        InitializeCpuMainMemoryIdentityMap(0x1000);

        var service = HybridCPU_ISE.Legacy.LegacyObservationServiceFactory.CreateLegacyGlobalStrict(new object());
        LegacyMachineStateReadException ex = Assert.Throws<LegacyMachineStateReadException>(
            () => service.ReadMemory((ulong)Processor.MainMemory.Length, 8));

        Assert.Equal(LegacyMachineStateReadFailureKind.AddressOutOfRange, ex.FailureKind);
        Assert.Equal((ulong)Processor.MainMemory.Length, ex.Address);
        Assert.Equal(8, ex.Length);
    }

    [Fact]
    public void ReadMemory_WhenLegacyCompatSourceStartsBeyondMainMemoryLength_ReturnsZeroFilledBuffer()
    {
        InitializeCpuMainMemoryIdentityMap(0x1000);

        var service = HybridCPU_ISE.Legacy.LegacyObservationServiceFactory.CreateLegacyGlobalCompat(new object());
        byte[] bytes = service.ReadMemory((ulong)Processor.MainMemory.Length, 8);

        Assert.Equal(new byte[8], bytes);
    }

    [Fact]
    public void ReadMemory_WhenUnderlyingReadFails_ThrowsExplicitFailureInsteadOfZeroFilledBuffer()
    {
        Processor.MainMemory = new YAKSys_Hybrid_CPU.MultiBankMemoryArea(bankCount: 4, bankSize: 0x1000UL);
        InitializeCpuMainMemoryIdentityMap(0x1000, preserveCurrentMainMemory: true);
        Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(0x1122_3344U), 0x100);

        YAKSys_Hybrid_CPU.MultiBankMemoryArea memory = Assert.IsType<YAKSys_Hybrid_CPU.MultiBankMemoryArea>(Processor.MainMemory);
        memory.SetBankDomainCapability(0, 0x1UL);
        YAKSys_Hybrid_CPU.MultiBankMemoryArea.SetAccessDomainTag(0x2UL);

        try
        {
            LegacyMachineStateReadException ex = Assert.Throws<LegacyMachineStateReadException>(
                () => HybridCPU_ISE.Legacy.LegacyObservationServiceFactory
                    .CreateLegacyGlobalStrict(new object())
                    .ReadMemory(0x100, 4));

            Assert.Equal(LegacyMachineStateReadFailureKind.ReadFault, ex.FailureKind);
            Assert.NotNull(ex.InnerException);
            Assert.Contains("silently squashed", ex.InnerException!.Message, StringComparison.Ordinal);
        }
        finally
        {
            YAKSys_Hybrid_CPU.MultiBankMemoryArea.SetAccessDomainTag(0);
            memory.SetBankDomainCapability(0, 0);
        }
    }

    private static void InitializeCpuMainMemoryIdentityMap(ulong size, bool preserveCurrentMainMemory = false)
    {
        if (!preserveCurrentMainMemory)
        {
            Processor.MainMemory = new Processor.MultiBankMemoryArea(4, 0x4000000UL);
        }

        IOMMU.Initialize();
        IOMMU.RegisterDevice(0);
        IOMMU.Map(
            deviceID: 0,
            ioVirtualAddress: 0,
            physicalAddress: 0,
            size: size,
            permissions: IOMMUAccessPermissions.ReadWrite);
    }
}
