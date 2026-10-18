using System.Reflection;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123pSystemDeviceCommandRegisterMaskValidInputCutoverTests
{
    [Fact]
    public void AggregatorUsesTwoIndependentCheckedPathsWithExactRawFallbacks()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Pipeline",
            "MicroOps",
            "Lane7Accelerator",
            "SystemDeviceCommandMicroOp.cs"));
        string aggregate = ExtractMethod(
            source,
            "private static ResourceBitset BuildResourceMask(");

        Assert.Equal(2, Count(aggregate, "ArchRegId.TryCreate("));
        Assert.Equal(1, Count(aggregate,
            "ResourceMaskBuilder.ForArchitecturalRegisterRead("));
        Assert.Equal(1, Count(aggregate,
            "ResourceMaskBuilder.ForRegisterRead(registerId)"));
        Assert.Equal(1, Count(aggregate,
            "ResourceMaskBuilder.ForArchitecturalRegisterWrite("));
        Assert.Equal(1, Count(aggregate,
            "ResourceMaskBuilder.ForRegisterWrite(registerId)"));
        Assert.True(
            aggregate.IndexOf("readRegisters.Count", StringComparison.Ordinal) <
            aggregate.IndexOf("writeRegisters.Count", StringComparison.Ordinal));
        Assert.DoesNotContain("ArchRegId.Create(", aggregate,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Math.Clamp", aggregate, StringComparison.Ordinal);
        Assert.DoesNotContain("%", aggregate, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryRepresentableRegisterPreservesReadWriteMaskParity()
    {
        for (int raw = ArchRegId.MinValue; raw <= ArchRegId.MaxValue; raw++)
        {
            Assert.True(ArchRegId.TryCreate(raw, out ArchRegId checkedId));
            Assert.Equal(
                ResourceMaskBuilder.ForRegisterRead(raw),
                ResourceMaskBuilder.ForArchitecturalRegisterRead(checkedId));
            Assert.Equal(
                ResourceMaskBuilder.ForRegisterWrite(raw),
                ResourceMaskBuilder.ForArchitecturalRegisterWrite(checkedId));
            Assert.Equal(
                BaseMask(0) | ResourceMaskBuilder.ForRegisterRead(raw),
                Invoke(0, [raw], []));
            Assert.Equal(
                BaseMask(0) | ResourceMaskBuilder.ForRegisterWrite(raw),
                Invoke(0, [], [raw]));
        }
    }

    [Fact]
    public void ReflectionRawFallbackDomainFoldOrderAndNullFailuresRemainFrozen()
    {
        int[] rawValues =
        [
            int.MinValue, -65, -64, -63, -5, -4, -3, -1,
            32, 63, 64, 65, 127, 255, 256, int.MaxValue,
        ];

        foreach (int raw in rawValues)
        {
            Assert.False(ArchRegId.TryCreate(raw, out _));
            Assert.Equal(
                BaseMask(0xE) | ResourceMaskBuilder.ForRegisterRead(raw),
                Invoke(0xABCDE, [raw], []));
            Assert.Equal(
                BaseMask(0xE) | ResourceMaskBuilder.ForRegisterWrite(raw),
                Invoke(0xABCDE, [], [raw]));
        }

        int[] reads = [-4, 7, 64, int.MaxValue];
        int[] writes = [-1, 9, 255, int.MinValue];
        ResourceBitset expected = BaseMask(1);
        foreach (int value in reads)
            expected |= ResourceMaskBuilder.ForRegisterRead(value);
        foreach (int value in writes)
            expected |= ResourceMaskBuilder.ForRegisterWrite(value);
        Assert.Equal(expected, Invoke(0x21, reads, writes));

        MethodInfo aggregate = GetAggregator();
        TargetInvocationException nullRead =
            Assert.Throws<TargetInvocationException>(() =>
                aggregate.Invoke(null, [0UL, null, Array.Empty<int>()]));
        Assert.IsType<NullReferenceException>(nullRead.InnerException);
        TargetInvocationException nullWrite =
            Assert.Throws<TargetInvocationException>(() =>
                aggregate.Invoke(null, [0UL, Array.Empty<int>(), null]));
        Assert.IsType<NullReferenceException>(nullWrite.InnerException);
    }

    [Fact]
    public void ProductionConstructorsSignaturesAndMetadataPublicationRemainUnchanged()
    {
        var operation = new AcceleratorPollMicroOp(9, 7);
        Assert.Equal((ushort)9, operation.DestinationRegister);
        Assert.Equal((ushort)7, operation.TokenRegister);
        Assert.Equal([7], operation.ReadRegisters);
        Assert.Equal([9], operation.WriteRegisters);
        Assert.Equal(
            BaseMask(0) |
            ResourceMaskBuilder.ForRegisterRead(7) |
            ResourceMaskBuilder.ForRegisterWrite(9),
            operation.ResourceMask);

        MethodInfo aggregate = GetAggregator();
        Assert.True(aggregate.IsPrivate);
        Assert.Equal(typeof(ResourceBitset), aggregate.ReturnType);
        Assert.Equal(
            [typeof(ulong), typeof(IReadOnlyList<int>), typeof(IReadOnlyList<int>)],
            aggregate.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Single(typeof(SystemDeviceCommandMicroOp)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.DeclaredOnly)
            .Where(method => method.Name ==
                             nameof(SystemDeviceCommandMicroOp.InitializeMetadata)));
    }

    private static ResourceBitset Invoke(
        ulong domainTag,
        IReadOnlyList<int>? reads,
        IReadOnlyList<int>? writes) =>
        Assert.IsType<ResourceBitset>(
            GetAggregator().Invoke(null, [domainTag, reads, writes]));

    private static MethodInfo GetAggregator() =>
        typeof(SystemDeviceCommandMicroOp).GetMethod(
            "BuildResourceMask",
            BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(
            nameof(SystemDeviceCommandMicroOp), "BuildResourceMask");

    private static ResourceBitset BaseMask(int domainBucket) =>
        ResourceMaskBuilder.ForAccelerator(0) |
        ResourceMaskBuilder.ForMemoryDomain(domainBucket);

    private static string ExtractMethod(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0);
        int brace = source.IndexOf('{', start);
        Assert.True(brace > start);
        int depth = 0;
        for (int index = brace; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}' && --depth == 0)
                return source[start..(index + 1)];
        }

        throw new InvalidOperationException("Method body was not terminated.");
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
                return current;

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("HybridCPU repository root was not found.");
    }
}
