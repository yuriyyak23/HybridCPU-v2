using System.Collections.Generic;
using System.IO;

namespace HybridCPU_ISE.Tests.TestHelpers;

internal static class CompatFreezeGateCatalog
{
    internal const string Category = "CompatFreeze";
    internal static readonly string PolicyDocumentRelativePath =
        RelativePath("Documentation", "Old", "legacy_audit_2026-04-11", "compat_freeze_gate.md");
    internal static readonly string RunnerScriptRelativePath =
        RelativePath("build", "run-compat-freeze-gate.ps1");

    internal static readonly string[] ProductionRootRelativePaths =
    [
        "CpuInterfaceBridge",
        "forms",
        "HybridCPU_Compiler",
        "HybridCPU_EnvGUI",
        "HybridCPU_ISE",
        "TestAssemblerConsoleApps",
    ];

    internal static readonly string[] ForbiddenStaticAccessorPatterns =
    [
        "ISE_StateAccessor.",
        "ISE_StateAccessor ",
        "ISE_StateAccessor\t",
        "ISE_StateAccessor:",
        "ISE_StateAccessor>",
        "ISE_StateAccessor)",
    ];

    internal sealed record SymbolAllowance(
        string Name,
        string Pattern,
        string Owner,
        string RemovalMilestone,
        string[] AllowedRelativePaths,
        string[]? ProductionRootRelativePaths = null);

    internal static readonly SymbolAllowance VliwInstructionMentions = new(
        Name: "VLIW_Instruction allowlist",
        Pattern: "VLIW_Instruction",
        Owner: "Frontend/compat container boundary",
        RemovalMilestone: "After canonical frontend payload replaces raw VLIW container across compiler, tooling, and runtime seams",
        AllowedRelativePaths:
        [
            RelativePath("forms", "Form_Main.SourceEditor", "Form_Main.SourceEditor.cs"),
            RelativePath("HybridCPU_Compiler", "API", "Compilation", "HybridCpuCanonicalCompiler.cs"),
            RelativePath("HybridCPU_Compiler", "API", "Facade", "AppAsmFacade.cs"),
            RelativePath("HybridCPU_Compiler", "API", "Facade", "IExpertBackendFacade.cs"),
            RelativePath("HybridCPU_Compiler", "API", "Facade", "PlatformAsmFacade.cs"),
            RelativePath("HybridCPU_Compiler", "API", "Multithreaded", "HybridCpuMultithreadedCompiler.cs"),
            RelativePath("HybridCPU_Compiler", "API", "Threading", "HybridCpuThreadCompilerContext.cs"),
            RelativePath("HybridCPU_Compiler", "API", "Threading", "ThreadCompilerContext.Mutation.cs"),
            RelativePath("HybridCPU_Compiler", "Core", "IR", "Bundling", "HybridCpuBundleLowerer.cs"),
            RelativePath("HybridCPU_Compiler", "Core", "IR", "Construction", "HybridCpuIrBuilder.cs"),
            RelativePath("HybridCPU_Compiler", "Core", "IR", "Decomposition", "CoordinatorFunctionSynthesizer.cs"),
            RelativePath("HybridCPU_Compiler", "Core", "IR", "Decomposition", "WorkerFunctionSynthesizer.cs"),
            RelativePath("HybridCPU_Compiler", "Core", "IR", "Hazards", "HybridCpuOpcodeSemantics.cs"),
            RelativePath("HybridCPU_EnvGUI", "Form_Main.Init.cs"),
            RelativePath("HybridCPU_EnvGUI", "SimpleAsmApp.AppLevel.cs"),
            RelativePath("HybridCPU_EnvGUI", "SimpleAsmApp.cs"),
            RelativePath("HybridCPU_EnvGUI", "SimpleAsmApp.PlatformLevel.cs"),
            RelativePath("HybridCPU_ISE", "Arch", "InstructionEncoder.cs"),
            RelativePath("HybridCPU_ISE", "Arch", "OpcodeInfo.Registry.Helpers.cs"),
            RelativePath("HybridCPU_ISE", "Arch", "OpcodeInfo.Types.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Contracts", "CompilerContract.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Decoder", "CPU_Core.Decoder.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Decoder", "DecodedBundleTransportProjector.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Decoder", "DecoderFrontendOccupiedInstructionProjection.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Decoder", "IDecoderFrontend.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Decoder", "VliwDecoderV4.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Diagnostics", "InstructionRegistry.Helpers.Core.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Diagnostics", "InstructionRegistry.Helpers.Vector.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Execution", "DmaStreamCompute", "DmaStreamComputeDescriptorParser.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Execution", "ExternalAccelerators", "Descriptors", "AcceleratorDescriptorParser.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Execution", "StreamEngine", "StreamEngine.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Execution", "StreamEngine", "StreamExecutionRequest.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Pipeline", "Core", "CPU_Core.Pipeline.Stages.DecodeStage.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Pipeline", "Core", "CPU_Core.PipelineExecution.ControlFlow.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Pipeline", "Core", "CPU_Core.PipelineExecution.StageFlow.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Pipeline", "Core", "CPU_Core.TestSupport.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Pipeline", "MicroOps", "MicroOp.Compute.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Pipeline", "MicroOps", "MicroOp.Control.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Pipeline", "MicroOps", "MicroOp.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Pipeline", "MicroOps", "MicroOp.IO.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Pipeline", "MicroOps", "MicroOp.LoadStore.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Pipeline", "MicroOps", "MicroOp.Misc.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Pipeline", "MicroOps", "VectorMicroOps.Compute.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Pipeline", "MicroOps", "VectorMicroOps.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Pipeline", "MicroOps", "VectorMicroOps.Data.cs"),
            RelativePath("HybridCPU_ISE", "Core", "System", "CPU_Core.System.cs"),
            RelativePath("HybridCPU_ISE", "Arch", "DataTypeEnum.cs"),
            RelativePath("HybridCPU_ISE", "Arch", "Compat", "VLIW_Instruction.Layout.cs"),
            RelativePath("HybridCPU_ISE", "Arch", "Compat", "VLIW_Instruction.Flags.cs"),
            RelativePath("HybridCPU_ISE", "Arch", "Compat", "VLIW_Instruction.RegisterPacking.cs"),
            RelativePath("HybridCPU_ISE", "Arch", "Compat", "VLIW_Instruction.Serialization.cs"),
            RelativePath("HybridCPU_ISE", "Arch", "Compat", "VLIW_Instruction.CompatIngress.cs"),
            RelativePath("HybridCPU_ISE", "Arch", "Compat", "VLIW_Bundle.cs"),
            RelativePath("HybridCPU_ISE", "Processor", "Core", "CpuCoreSystemInstructionEmitter.cs"),
            RelativePath("HybridCPU_ISE", "Processor", "Core", "Processor.CompilerEmission.cs"),
            RelativePath("HybridCPU_ISE", "Processor", "Core", "Processor.CompilerBridge.cs"),
            RelativePath("TestAssemblerConsoleApps", "SimpleAsmApp.cs"),
            RelativePath("TestAssemblerConsoleApps", "SimpleAsmApp.Emit.cs"),
        ]);

    internal static readonly SymbolAllowance InstructionsEnumMentions = new(
        Name: "InstructionsEnum allowlist",
        Pattern: "InstructionsEnum",
        Owner: "ISA identity unification boundary",
        RemovalMilestone: "After compiler, runtime registry, and tooling converge on IsaOpcode-only public surfaces",
        AllowedRelativePaths:
        [
            RelativePath("forms", "Form_Main.SourceEditor", "Form_Main.SourceEditor.cs"),
            RelativePath("HybridCPU_Compiler", "API", "Facade", "AppAsmFacade.cs"),
            RelativePath("HybridCPU_Compiler", "API", "Facade", "ExpertBackendFacade.cs"),
            RelativePath("HybridCPU_Compiler", "API", "Facade", "PlatformAsmFacade.cs"),
            RelativePath("HybridCPU_Compiler", "API", "Multithreaded", "HybridCpuMultithreadedCompiler.cs"),
            RelativePath("HybridCPU_Compiler", "API", "Threading", "HybridCpuThreadCompilerContext.cs"),
            RelativePath("HybridCPU_Compiler", "Core", "IR", "Analysis", "ParallelRegionDetector.cs"),
            RelativePath("HybridCPU_Compiler", "Core", "IR", "Bundling", "HybridCpuBundleLowerer.cs"),
            RelativePath("HybridCPU_Compiler", "Core", "IR", "Construction", "HybridCpuIrBuilder.cs"),
            RelativePath("HybridCPU_Compiler", "Core", "IR", "Decomposition", "CoordinatorFunctionSynthesizer.cs"),
            RelativePath("HybridCPU_Compiler", "Core", "IR", "Decomposition", "ParallelForCompiler.cs"),
            RelativePath("HybridCPU_Compiler", "Core", "IR", "Decomposition", "WorkerFunctionSynthesizer.cs"),
            RelativePath("HybridCPU_Compiler", "Core", "IR", "Hazards", "HybridCpuHazardModel.cs"),
            RelativePath("HybridCPU_Compiler", "Core", "IR", "Hazards", "HybridCpuInstructionLegalityChecker.cs"),
            RelativePath("HybridCPU_Compiler", "Core", "IR", "Hazards", "HybridCpuOpcodeSemantics.cs"),
            RelativePath("HybridCPU_Compiler", "Core", "IR", "Hazards", "HybridCpuStructuralResourceModel.cs"),
            RelativePath("HybridCPU_Compiler", "Core", "IR", "Model", "IrInstruction.cs"),
            RelativePath("HybridCPU_Compiler", "Core", "IR", "Model", "IrOpcodeExecutionProfile.cs"),
            RelativePath("HybridCPU_Compiler", "Core", "IR", "Model", "IrParallelRegion.cs"),
            RelativePath("HybridCPU_EnvGUI", "Form_Main.ExternalModulesBridge.cs"),
            RelativePath("HybridCPU_EnvGUI", "Form_Main.Init.cs"),
            RelativePath("HybridCPU_EnvGUI", "SimpleAsmApp.AppLevel.cs"),
            RelativePath("HybridCPU_EnvGUI", "SimpleAsmApp.cs"),
            RelativePath("HybridCPU_EnvGUI", "SimpleAsmApp.PlatformLevel.cs"),
            RelativePath("HybridCPU_ISE", "Arch", "InstructionClassifier.cs"),
            RelativePath("HybridCPU_ISE", "Arch", "InstructionEncoder.cs"),
            RelativePath("HybridCPU_ISE", "Arch", "OpcodeInfo.Registry.Data.MemoryControl.cs"),
            RelativePath("HybridCPU_ISE", "Arch", "OpcodeInfo.Registry.Data.Scalar.cs"),
            RelativePath("HybridCPU_ISE", "Arch", "OpcodeInfo.Registry.Data.System.cs"),
            RelativePath("HybridCPU_ISE", "Arch", "OpcodeInfo.Registry.Data.Vector.cs"),
            RelativePath("HybridCPU_ISE", "Arch", "OpcodeInfo.Registry.Helpers.cs"),
            RelativePath("HybridCPU_ISE", "Arch", "OpcodeInfo.Types.cs"),
            RelativePath("HybridCPU_ISE", "Core", "ALU", "ScalarAluOps.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Common", "CPU_Core.Enums.cs"),
            
            RelativePath("HybridCPU_ISE", "Core", "Diagnostics", "InstructionRegistry.Helpers.Vector.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Diagnostics", "InstructionRegistry.Initialize.Base.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Diagnostics", "InstructionRegistry.Initialize.Scalar.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Diagnostics", "InstructionRegistry.Initialize.Vector.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Diagnostics", "InstructionRegistry.Runtime.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Execution", "Compute", "VectorALU.Comparison.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Execution", "Compute", "VectorALU.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Execution", "Compute", "VectorALU.FMA.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Execution", "Compute", "VectorALU.Reduction.cs"),
            
            RelativePath("HybridCPU_ISE", "Core", "Execution", "ExecutionEngine.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Execution", "IExecutionUnit.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Execution", "StreamEngine", "StreamExecutionRequest.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Execution", "StreamEngine", "StreamEngine.AddressGen.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Execution", "StreamEngine", "StreamEngine.Classification.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Execution", "StreamEngine", "StreamEngine.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Execution", "StreamEngine", "StreamEngine.Execute1D.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Execution", "StreamEngine", "StreamEngine.ExecuteModes.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Execution", "Validation", "ReferenceModel.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Legality", "BundleLegalityAnalyzer.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Pipeline", "Core", "CPU_Core.TestSupport.cs"),
            RelativePath("HybridCPU_ISE", "Core", "State", "CPU_Core.FSM.cs"),
            RelativePath("HybridCPU_ISE", "Core", "System", "CPU_Core.System.cs"),
            
            RelativePath("HybridCPU_ISE", "Arch", "Compat", "VLIW_Instruction.Layout.cs"),
            RelativePath("HybridCPU_ISE", "Processor", "Core", "CpuCoreSystemInstructionEmitter.cs"),
            RelativePath("TestAssemblerConsoleApps", "ArchitecturalDiagnostics.cs"),
            RelativePath("TestAssemblerConsoleApps", "SimpleAsmApp.Emit.cs"),
            RelativePath("TestAssemblerConsoleApps", "SimpleAsmApp.Showcase.cs"),
        ]);

    internal static readonly SymbolAllowance LiveStreamCompatMentions = new(
        Name: "Live stream compat ingress allowlist",
        Pattern: "VLIW_Instruction",
        Owner: "Direct stream compat test seam",
        RemovalMilestone: "After direct-stream compat regression coverage no longer depends on runtime-side test support hooks",
        AllowedRelativePaths:
        [
            RelativePath("HybridCPU_ISE", "Core", "Execution", "StreamEngine", "StreamEngine.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Execution", "StreamEngine", "StreamExecutionRequest.cs"),
            RelativePath("HybridCPU_ISE", "Core", "Pipeline", "Core", "CPU_Core.TestSupport.cs"),
        ],
        ProductionRootRelativePaths:
        [
            RelativePath("HybridCPU_ISE", "Core", "Execution", "StreamEngine"),
            RelativePath("HybridCPU_ISE", "Core", "Pipeline", "Core", "CPU_Core.TestSupport.cs"),
        ]);

    internal static readonly SymbolAllowance AddVliwInstructionCallers = new(
        Name: "Add_VLIW_Instruction allowlist",
        Pattern: "Add_VLIW_Instruction(",
        Owner: "ProcessorCompilerBridge compat append ingress",
        RemovalMilestone: "After emitter-driven recording fully replaces raw Add_VLIW_Instruction(...) append entrypoints",
        AllowedRelativePaths:
        [
            RelativePath("HybridCPU_ISE", "Processor", "Core", "Processor.CompilerBridge.cs"),
        ],
        ProductionRootRelativePaths:
        [
            "HybridCPU_ISE",
        ]);

    internal static IReadOnlyList<SymbolAllowance> Allowances { get; } =
    [
        VliwInstructionMentions,
        InstructionsEnumMentions,
        LiveStreamCompatMentions,
        AddVliwInstructionCallers,
    ];

    private static string RelativePath(params string[] parts) => Path.Combine(parts);
}
