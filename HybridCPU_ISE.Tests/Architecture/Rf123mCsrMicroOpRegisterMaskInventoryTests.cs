using System.Reflection;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123mCsrMicroOpRegisterMaskInventoryTests
{
    private const string ThisFileName =
        "Rf123mCsrMicroOpRegisterMaskInventoryTests.cs";
    private const string ValidInputCutoverGuardFileName =
        "Rf123nCsrMicroOpRegisterMaskValidInputCutoverTests.cs";

    [Fact]
    public void PaperDefinesCsrRolesSentinelsCapabilityAndOnlyLaterValidCutover()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains(
            "#### 3.7.5 CSR MicroOp architectural-register metadata boundary",
            paper, StringComparison.Ordinal);
        Assert.Contains("The two architectural-register roles are distinct",
            paper, StringComparison.Ordinal);
        Assert.Contains("neither x0, `NoArchReg=255`, nor", paper,
            StringComparison.Ordinal);
        Assert.Contains("Destination x0 is a valid", paper,
            StringComparison.Ordinal);
        Assert.Contains("atomic/serializing resource", paper,
            StringComparison.Ordinal);
        Assert.Contains("writeback-capability seed is stateful", paper,
            StringComparison.Ordinal);
        Assert.Contains("construction bypasses those wire checks",
            paper, StringComparison.Ordinal);
        Assert.Contains("Values `1..31` may use", paper,
            StringComparison.Ordinal);
        Assert.Contains("unrepresentable participating value must use the exact raw call",
            paper, StringComparison.Ordinal);
        Assert.Contains("Invalid-input rejection, constructor", paper,
            StringComparison.Ordinal);
    }


    [Fact]
    public void MetadataSourceShapeFreezesFiltersRawCallsAtomicSafetyAndCapabilitySeed()
    {
        string root = FindRepositoryRoot();
        string control = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Control", "MicroOp.Control.cs");
        string csr = Slice(
            control,
            "public abstract class CSRMicroOp : MicroOp",
            "/// <summary>\n    /// NOP (No Operation) micro-operation");
        string initialize = ExtractMethod(csr, "public void InitializeMetadata()");
        string capability = ExtractMethod(
            csr,
            "private bool IsRegisterWritebackConfigured()");

        Assert.Equal(1, Count(csr,
            "registerId != 0 &&\n            registerId != VLIW_Instruction.NoReg &&\n            registerId != VLIW_Instruction.NoArchReg;"));
        Assert.Equal(1, Count(initialize,
            "WritesFromSourceRegister && HasArchitecturalSourceRegister"));
        Assert.Equal(1, Count(initialize,
            "ReadsCsr &&\n                                             IsRegisterWritebackConfigured() &&\n                                             HasArchitecturalDestinationRegister"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterRead(SrcRegID)"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterWrite(DestRegID)"));
        Assert.Equal(1, Count(initialize, "ResourceMaskBuilder.ForAtomic()"));
        Assert.Equal(2, Count(initialize, "ArchRegId.TryCreate("));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForArchitecturalRegisterRead(sourceRegisterId)"));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForArchitecturalRegisterWrite(destinationRegisterId)"));
        Assert.Contains("PublishExplicitStructuralSafetyMask();", initialize,
            StringComparison.Ordinal);
        Assert.Contains("RefreshAdmissionMetadata(this);", initialize,
            StringComparison.Ordinal);

        Assert.Contains("if (!_registerWritebackCapabilitySeeded || WritesRegister)",
            capability, StringComparison.Ordinal);
        Assert.Contains("_registerWritebackConfigured = WritesRegister;",
            capability, StringComparison.Ordinal);
        Assert.Contains("return _registerWritebackConfigured;", capability,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Math.Clamp", initialize, StringComparison.Ordinal);
        Assert.DoesNotContain("%", initialize, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryRawUshortPreservesSourceAndConfiguredDestinationMetadata()
    {
        ResourceBitset atomic = ResourceMaskBuilder.ForAtomic();

        for (int raw = ushort.MinValue; raw <= ushort.MaxValue; raw++)
        {
            ushort value = (ushort)raw;
            bool participates =
                value != 0 &&
                value != VLIW_Instruction.NoArchReg &&
                value != VLIW_Instruction.NoReg;

            var source = new CsrReadWriteMicroOp
            {
                SrcRegID = value,
                DestRegID = VLIW_Instruction.NoReg,
            };
            source.InitializeMetadata();
            Assert.Equal(
                participates ? new[] { (int)value } : Array.Empty<int>(),
                source.ReadRegisters);
            Assert.Empty(source.WriteRegisters);
            Assert.Equal(
                participates ? atomic | ExpectedRead(value) : atomic,
                source.ResourceMask);
            AssertMetadataSnapshots(source);

            var destination = new CsrReadCounterMicroOp
            {
                DestRegID = value,
                WritesRegister = true,
            };
            destination.InitializeMetadata();
            Assert.Empty(destination.ReadRegisters);
            Assert.Equal(
                participates ? new[] { (int)value } : Array.Empty<int>(),
                destination.WriteRegisters);
            Assert.Equal(participates, destination.WritesRegister);
            Assert.Equal(
                participates ? atomic | ExpectedWrite(value) : atomic,
                destination.ResourceMask);
            AssertMetadataSnapshots(destination);
        }
    }

    [Fact]
    public void ConcreteSubclassRolesAndStatefulWritebackCapabilityRemainExplicit()
    {
        foreach (Type type in ExpectedConcreteTypes)
        {
            var operation = Assert.IsAssignableFrom<CSRMicroOp>(
                Activator.CreateInstance(type));
            operation.SrcRegID = 7;
            operation.DestRegID = 9;
            operation.WritesRegister = true;
            operation.InitializeMetadata();

            bool readsSource =
                operation is CsrReadWriteMicroOp or
                CsrReadSetMicroOp or
                CsrReadClearMicroOp;
            bool writesDestination = operation is not CsrClearMicroOp;
            Assert.Equal(
                readsSource ? new[] { 7 } : Array.Empty<int>(),
                operation.ReadRegisters);
            Assert.Equal(
                writesDestination ? new[] { 9 } : Array.Empty<int>(),
                operation.WriteRegisters);
            Assert.Equal(writesDestination, operation.WritesRegister);
        }

        var stateful = new CsrReadCounterMicroOp
        {
            DestRegID = 9,
        };
        stateful.InitializeMetadata();
        Assert.False(stateful.WritesRegister);
        Assert.Empty(stateful.WriteRegisters);

        stateful.WritesRegister = true;
        stateful.InitializeMetadata();
        Assert.True(stateful.WritesRegister);
        Assert.Equal(new[] { 9 }, stateful.WriteRegisters);

        stateful.DestRegID = VLIW_Instruction.NoArchReg;
        stateful.InitializeMetadata();
        Assert.False(stateful.WritesRegister);
        Assert.Empty(stateful.WriteRegisters);

        stateful.DestRegID = 9;
        stateful.InitializeMetadata();
        Assert.True(stateful.WritesRegister);
        Assert.Equal(new[] { 9 }, stateful.WriteRegisters);
    }


    private static void AssertMetadataSnapshots(CSRMicroOp operation)
    {
        Assert.Equal(operation.ReadRegisters,
            operation.AdmissionMetadata.ReadRegisters);
        Assert.Equal(operation.WriteRegisters,
            operation.AdmissionMetadata.WriteRegisters);
        Assert.Equal(operation.ResourceMask.Low, operation.SafetyMask.Low);
        Assert.Equal(1UL << 63, operation.SafetyMask.High);
    }

    private static ResourceBitset ExpectedRead(ushort value)
    {
        int group = Math.Min(value / 4, 15);
        return new ResourceBitset(1UL << group, 0);
    }

    private static ResourceBitset ExpectedWrite(ushort value)
    {
        int group = Math.Min(value / 4, 15);
        return new ResourceBitset(1UL << (16 + group), 0);
    }

    private static string[] FindFilesContainingAny(
        string root,
        string relativeRoot,
        IReadOnlyList<string> values,
        params string[] excludedFileNames) =>
        Directory.EnumerateFiles(
                Path.Combine(root, relativeRoot), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path => !excludedFileNames.Contains(
                Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            .Where(path =>
            {
                string text = File.ReadAllText(path);
                return values.Any(value => text.Contains(value, StringComparison.Ordinal));
            })
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

    private static string ReadTree(
        string root,
        string relativeRoot,
        IReadOnlyCollection<string>? excludedFileNames = null,
        string? requiredPathFragment = null) =>
        string.Join("\n", Directory.EnumerateFiles(
                Path.Combine(root, relativeRoot), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path => excludedFileNames is null ||
                           !excludedFileNames.Contains(
                               Path.GetFileName(path),
                               StringComparer.OrdinalIgnoreCase))
            .Where(path => requiredPathFragment is null ||
                           path.Contains(
                               requiredPathFragment,
                               StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));

    private static bool IsBuildOutput(string path) =>
        path.Contains(
            $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase) ||
        path.Contains(
            $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase);

    private static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Start marker '{startMarker}' was not found.");
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"End marker '{endMarker}' was not found.");
        return source[start..end];
    }

    private static string ExtractMethod(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Method signature '{signature}' was not found.");
        int brace = source.IndexOf('{', start);
        Assert.True(brace >= 0);
        int depth = 0;
        for (int index = brace; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}' && --depth == 0)
                return source[start..(index + 1)];
        }

        throw new InvalidOperationException($"Method '{signature}' has no closing brace.");
    }

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

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

    private static readonly Type[] ExpectedConcreteTypes =
    [
        typeof(CsrClearMicroOp),
        typeof(CsrReadClearImmediateMicroOp),
        typeof(CsrReadClearMicroOp),
        typeof(CsrReadCounterMicroOp),
        typeof(CsrReadSetImmediateMicroOp),
        typeof(CsrReadSetMicroOp),
        typeof(CsrReadWriteImmediateMicroOp),
        typeof(CsrReadWriteMicroOp),
    ];

    private static readonly string[] CsrTypeSymbols =
    [
        "CSRMicroOp",
        "CsrClearMicroOp",
        "CsrReadClearImmediateMicroOp",
        "CsrReadClearMicroOp",
        "CsrReadCounterMicroOp",
        "CsrReadSetImmediateMicroOp",
        "CsrReadSetMicroOp",
        "CsrReadWriteImmediateMicroOp",
        "CsrReadWriteMicroOp",
    ];

    private static readonly string[] CsrOpcodeSymbols =
    [
        "CSR_CLEAR",
        "CSRRC",
        "CSRRCI",
        "CSRRS",
        "CSRRSI",
        "CSRRW",
        "CSRRWI",
        "RDCYCLE",
        "VSETVEXCPMASK",
        "VSETVEXCPPRI",
    ];

    private static readonly string[] ExpectedProductionFiles =
    [
        "HybridCPU_ISE/CloseToHSL/Core/Diagnostics/InstructionRegistry.Helpers.Csr.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Diagnostics/InstructionRegistry.Initialize.Base.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Execution/Dispatch/ExecutionDispatcherV4.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Execution/ExecutionDispatcherV4.Trace.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Legality/BundleLegalityAnalyzer.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.StageFlow.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Pipeline/InternalOpBuilder.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Control/MicroOp.Control.cs",
        "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.Retire.cs",
    ];

    private static readonly string[] ExpectedTestFiles =
    [
        "HybridCPU_ISE.Tests/Architecture/Rf06SpecializedCapabilityProjectionTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf084eCsrWriteIdentitySourceBlockerAuditTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf084fCsrWriteApprovedResidualExclusionTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf084vCsrReadbackRegisterWriteSourceBlockerAuditTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf1132RetireRefThisInventoryTests.cs",
        "HybridCPU_ISE.Tests/Architecture/Rf1136RetiredCsrWriteRefThisHardeningTests.cs",
        "HybridCPU_ISE.Tests/CompilerTests/CompilerEmissionInventoryTests.cs",
        "HybridCPU_ISE.Tests/PhasingAndExtensions/SlotClassTaxonomyTests.cs",
        "HybridCPU_ISE.Tests/tests/DecoderContextImmediateAbiTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase02InstructionIrTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase03ScalarSystemCounterRdcycleExecutableTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase09CanonicalDecodePublicationContractTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase09DirectFactoryCallerBoundaryTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase3V6IsaTableDrivenTests.cs",
        "HybridCPU_ISE.Tests/tests/Phase5LegacyRemovalTests.cs",
        "HybridCPU_ISE.Tests/tests/RetireContractClosureTests.cs",
        "HybridCPU_ISE.Tests/tests/WriteBackFaultOrderingTests.cs",
    ];

    private static readonly string[] ExpectedCompilerWireFiles =
    [
        "HybridCPU_Compiler/API/Facade/PlatformAsmFacade.cs",
        "HybridCPU_Compiler/Core/IR/Bundling/HybridCpuBundleLowerer.cs",
        "HybridCPU_Compiler/Core/IR/Construction/HybridCpuIrBuilder.cs",
        "HybridCPU_Compiler/Core/IR/Model/CompilerSystemCounterAbiContract.cs",
    ];

    private static readonly string[] ExpectedAssemblerWireFiles =
    [
        "TestAssemblerConsoleApps/SimpleAsmApp.Showcase.cs",
    ];
}
