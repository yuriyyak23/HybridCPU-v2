using System.Reflection;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123ajVConfigRegisterMaskValidInputCutoverTests
{
    [Fact]
    public void VConfigUsesExactlyTwoCheckedSelectionsWithExactRawFallbacks()
    {
        string initialize = ReadInitializeMetadata();

        Assert.Equal(2, Count(initialize, "ArchRegId.TryCreate("));
        Assert.Equal(1, Count(initialize,
            "ArchRegId.TryCreate(registerId, out ArchRegId register)"));
        Assert.Equal(1, Count(initialize,
            "ForArchitecturalRegisterRead(register)"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterRead(registerId)"));
        Assert.Equal(1, Count(initialize,
            "DestRegID, out ArchRegId destinationRegister"));
        Assert.Equal(1, Count(initialize,
            "ForArchitecturalRegisterWrite("));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterWrite(DestRegID)"));
        Assert.DoesNotContain("ArchRegId.Create(", initialize,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Math.Clamp", initialize,
            StringComparison.Ordinal);
        Assert.DoesNotContain("%", initialize, StringComparison.Ordinal);

        AssertOrdered(initialize,
            "bool writesRegister = HasArchitecturalDestinationRegister()",
            "ReadRegisters = OperationKind switch",
            "WriteRegisters = writesRegister",
            "ResourceMask = ResourceBitset.Zero",
            "foreach (int registerId in ReadRegisters)",
            "ArchRegId.TryCreate(registerId, out ArchRegId register)",
            "ForArchitecturalRegisterRead(register)",
            "ForRegisterRead(registerId)",
            "if (writesRegister)",
            "DestRegID, out ArchRegId destinationRegister",
            "ForArchitecturalRegisterWrite(",
            "ForRegisterWrite(DestRegID)",
            "PublishExplicitStructuralSafetyMask()",
            "RefreshAdmissionMetadata(this)");
    }

    [Fact]
    public void EveryRepresentableParticipatingRegisterPreservesRoleAndMaskParity()
    {
        for (int raw = 1; raw <= ArchRegId.MaxValue; raw++)
        {
            Assert.True(ArchRegId.TryCreate(raw, out ArchRegId register));
            Assert.Equal(ResourceMaskBuilder.ForRegisterRead(raw),
                ResourceMaskBuilder.ForArchitecturalRegisterRead(register));
            Assert.Equal(ResourceMaskBuilder.ForRegisterWrite(raw),
                ResourceMaskBuilder.ForArchitecturalRegisterWrite(register));

            VConfigMicroOp source = CreateSource(
                VectorConfigOperationKind.Vsetvl, (ushort)raw, (ushort)raw);
            Assert.Equal([raw, raw], source.ReadRegisters);
            Assert.Empty(source.WriteRegisters);
            Assert.Equal(ResourceMaskBuilder.ForRegisterRead(raw),
                source.ResourceMask);

            VConfigMicroOp destination = CreateDestination((ushort)raw);
            Assert.Empty(destination.ReadRegisters);
            Assert.Equal([raw], destination.WriteRegisters);
            Assert.True(destination.WritesRegister);
            Assert.Equal(ResourceMaskBuilder.ForRegisterWrite(raw),
                destination.ResourceMask);
        }
    }

    [Fact]
    public void FullUshortCompatibilityDomainPreservesListsMasksAndAbsence()
    {
        for (int raw = ushort.MinValue; raw <= ushort.MaxValue; raw++)
        {
            ushort value = (ushort)raw;
            bool absent = value is 0 or VLIW_Instruction.NoReg;

            VConfigMicroOp source = CreateSource(
                VectorConfigOperationKind.Vsetvli, value,
                VLIW_Instruction.NoReg);
            Assert.Equal(absent ? [] : [raw], source.ReadRegisters);
            Assert.Empty(source.WriteRegisters);
            Assert.Equal(absent
                    ? ResourceBitset.Zero
                    : ResourceMaskBuilder.ForRegisterRead(raw),
                source.ResourceMask);

            VConfigMicroOp destination = CreateDestination(value);
            Assert.Empty(destination.ReadRegisters);
            Assert.Equal(absent ? [] : [raw], destination.WriteRegisters);
            Assert.Equal(!absent, destination.WritesRegister);
            Assert.Equal(absent
                    ? ResourceBitset.Zero
                    : ResourceMaskBuilder.ForRegisterWrite(raw),
                destination.ResourceMask);
        }
    }

    [Fact]
    public void PublicByteProfilesPreserveX0OrderDuplicatesAndOperationRoles()
    {
        for (int raw = byte.MinValue; raw <= byte.MaxValue; raw++)
        {
            byte value = (byte)raw;
            var vsetvl = new VConfigMicroOp
            {
                DestRegID = VLIW_Instruction.NoReg
            };
            vsetvl.ConfigureForRegisterVType(value, value);
            vsetvl.InitializeMetadata();
            Assert.Equal(value == 0 ? [] : [raw, raw],
                vsetvl.ReadRegisters);
            Assert.Equal(value == 0
                    ? ResourceBitset.Zero
                    : ResourceMaskBuilder.ForRegisterRead(raw),
                vsetvl.ResourceMask);

            var vsetvli = new VConfigMicroOp
            {
                DestRegID = VLIW_Instruction.NoReg
            };
            vsetvli.ConfigureForImmediateVType(
                VectorConfigOperationKind.Vsetvli, value, 0);
            vsetvli.InitializeMetadata();
            Assert.Equal(value == 0 ? [] : [raw], vsetvli.ReadRegisters);

            var vsetivli = new VConfigMicroOp
            {
                DestRegID = VLIW_Instruction.NoReg
            };
            vsetivli.ConfigureForImmediateAvlAndVType(0, 0);
            vsetivli.InitializeMetadata();
            Assert.Empty(vsetivli.ReadRegisters);
            Assert.Empty(vsetivli.WriteRegisters);
            Assert.Equal(ResourceBitset.Zero, vsetivli.ResourceMask);
        }
    }

    [Fact]
    public void SafetyAdmissionSignaturesPlacementAndRetireOwnersRemainUnchanged()
    {
        var operation = new VConfigMicroOp { DestRegID = 31 };
        operation.ConfigureForRegisterVType(1, 31);
        operation.InitializeMetadata();

        ResourceBitset expected =
            ResourceMaskBuilder.ForRegisterRead(1) |
            ResourceMaskBuilder.ForRegisterRead(31) |
            ResourceMaskBuilder.ForRegisterWrite(31);
        Assert.Equal(expected, operation.ResourceMask);
        Assert.Equal(expected.Low, operation.SafetyMask.Low);
        Assert.Equal(1UL << 63, operation.SafetyMask.High);
        Assert.Equal(
            MicroOpAdmissionMetadata.BuildRegisterHazardMask(
                operation.ReadRegisters, operation.WriteRegisters),
            operation.AdmissionMetadata.RegisterHazardMask);
        Assert.Equal(SlotClass.SystemSingleton,
            operation.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned,
            operation.Placement.PinningKind);
        Assert.Equal(7, operation.Placement.PinnedLaneId);

        AssertMethod(nameof(VConfigMicroOp.ConfigureForRegisterVType),
            typeof(byte), typeof(byte));
        AssertMethod(nameof(VConfigMicroOp.ConfigureForImmediateVType),
            typeof(VectorConfigOperationKind), typeof(byte), typeof(ulong));
        AssertMethod(nameof(VConfigMicroOp.ConfigureForImmediateAvlAndVType),
            typeof(ulong), typeof(ulong));

        string root = FindRepositoryRoot();
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs");
        Assert.Contains(
            "vectorConfigEffect.DestinationRegister >= RenameMap.ArchRegs",
            retire, StringComparison.Ordinal);
        Assert.Contains("ApplyRetiredVectorConfigEffect", retire,
            StringComparison.Ordinal);
        Assert.Contains("EmitGeneratedVectorConfigRetireRecords", retire,
            StringComparison.Ordinal);

        foreach (string unrelated in new[]
                 {
                     "MemoryBankId", "ForMemoryBank", "ForDMAChannel",
                     "DomainId", "DomainTag", "Token", "Generation",
                     "SlotId", "ForSlot"
                 })
        {
            Assert.DoesNotContain(unrelated, ReadInitializeMetadata(),
                StringComparison.Ordinal);
        }
    }

    private static VConfigMicroOp CreateSource(
        VectorConfigOperationKind kind,
        ushort source1,
        ushort source2)
    {
        var operation = new VConfigMicroOp
        {
            DestRegID = VLIW_Instruction.NoReg
        };
        RequiredProperty(nameof(VConfigMicroOp.OperationKind))
            .SetValue(operation, kind);
        RequiredProperty(nameof(VConfigMicroOp.SrcReg1ID))
            .SetValue(operation, source1);
        RequiredProperty(nameof(VConfigMicroOp.SrcReg2ID))
            .SetValue(operation, source2);
        operation.InitializeMetadata();
        return operation;
    }

    private static VConfigMicroOp CreateDestination(ushort destination)
    {
        var operation = new VConfigMicroOp { DestRegID = destination };
        operation.ConfigureForImmediateAvlAndVType(0, 0);
        operation.InitializeMetadata();
        return operation;
    }

    private static void AssertMethod(string name, params Type[] parameters)
    {
        MethodInfo method = typeof(VConfigMicroOp).GetMethod(
            name, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new MissingMethodException(typeof(VConfigMicroOp).FullName,
                name);
        Assert.Equal(typeof(void), method.ReturnType);
        Assert.Equal(parameters,
            method.GetParameters().Select(parameter => parameter.ParameterType));
    }

    private static PropertyInfo RequiredProperty(string name) =>
        typeof(VConfigMicroOp).GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance)
        ?? throw new MissingMemberException(typeof(VConfigMicroOp).FullName,
            name);

    private static string ReadInitializeMetadata()
    {
        string source = Read(FindRepositoryRoot(), "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "MicroOps", "Vector",
            "VectorMicroOps.Data.cs");
        string carrier = ExtractBalanced(source, "public class VConfigMicroOp");
        return ExtractBalanced(carrier, "public void InitializeMetadata()");
    }

    private static void AssertOrdered(string source, params string[] tokens)
    {
        int previous = -1;
        foreach (string token in tokens)
        {
            int current = source.IndexOf(token, previous + 1,
                StringComparison.Ordinal);
            Assert.True(current > previous,
                $"Expected token after offset {previous}: {token}");
            previous = current;
        }
    }

    private static string ExtractBalanced(string source, string marker)
    {
        int markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, $"Missing marker: {marker}");
        int openBrace = source.IndexOf('{', markerIndex);
        Assert.True(openBrace >= 0, $"Missing opening brace after: {marker}");
        int depth = 0;
        for (int i = openBrace; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}' && --depth == 0)
                return source[markerIndex..(i + 1)];
        }

        throw new InvalidOperationException($"Unbalanced source after: {marker}");
    }

    private static int Count(string source, string token)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(token, index,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(Path.Combine([root, .. components]));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
