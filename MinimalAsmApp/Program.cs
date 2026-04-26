using HybridCPU.Compiler.Core;
using HybridCPU.Compiler.Core.IR;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;

internal static class Program
{
    private const int CoreId = 0;
    private const byte VirtualThreadId = 0;
    private const ulong EmissionBaseAddress = 0;
    private const ulong ResultAddress = 0x2000;
    private const ulong HardCycleLimit = 512;

    public static int Main()
    {
        try
        {
            BootstrapRuntime();

            // Conservative fences keep the example stable on the current pipeline surface.
            VLIW_Instruction[] program =
            [
                AddImmediate(destinationRegister: 1, sourceRegister: 0, immediate: 5), // x1 = 5
                AddImmediate(destinationRegister: 2, sourceRegister: 0, immediate: 7), // x2 = 7
                Fence(),
                Binary(Processor.CPU_Core.InstructionsEnum.Addition, destinationRegister: 3, sourceRegister1: 1, sourceRegister2: 2), // x3 = x1 + x2
                Fence(),
                StoreDoubleword(sourceRegister: 3, address: ResultAddress), // mem[ResultAddress] = x3
                Fence(),
                LoadDoubleword(destinationRegister: 4, address: ResultAddress), // x4 = mem[ResultAddress]
                Fence()
            ];

            HybridCpuCompiledProgram compiledProgram = HybridCpuCanonicalCompiler.CompileProgram(
                VirtualThreadId,
                program,
                EmissionBaseAddress,
                bundleAnnotations: CreateInputAnnotations(program.Length),
                frontendMode: FrontendMode.NativeVLIW);

            // The current runtime path expects emitted bundles to publish per-slot annotations.
            PublishBundleAnnotations(compiledProgram);

            Processor.CurrentProcessorMode = ProcessorMode.Emulation;
            Processor.CPU_Cores[CoreId].PrepareExecutionStart(EmissionBaseAddress);

            Processor.CPU_Core.PipelineControl control = RunUntilRetired((ulong)program.Length);
            DrainPipeline(32);
            control = Processor.CPU_Cores[CoreId].GetPipelineControl();
            ulong arithmeticResult = Processor.CPU_Cores[CoreId].ReadArch(VirtualThreadId, 3);
            ulong loadedResult = Processor.CPU_Cores[CoreId].ReadArch(VirtualThreadId, 4);
            ulong memoryResult = ReadUInt64(ResultAddress);

            EnsureExpectedResult("x3", arithmeticResult, expected: 12);
            EnsureExpectedResult("x4", loadedResult, expected: 12);
            EnsureExpectedResult($"mem[0x{ResultAddress:X}]", memoryResult, expected: 12);

            PrintProgramListing();
            Console.WriteLine();
            Console.WriteLine($"Bundles: {compiledProgram.BundleCount}");
            Console.WriteLine($"Retired instructions: {control.InstructionsRetired}");
            Console.WriteLine($"Cycles: {control.CycleCount}");
            Console.WriteLine($"x3 = {arithmeticResult}");
            Console.WriteLine($"x4 = {loadedResult}");
            Console.WriteLine($"mem[0x{ResultAddress:X}] = {memoryResult}");
            Console.WriteLine("Status: OK");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"MinimalAsmApp failed: {ex.Message}");
            return 1;
        }
    }

    private static void BootstrapRuntime()
    {
#pragma warning disable CS0618
        _ = new Processor(ProcessorMode.Compiler);
#pragma warning restore CS0618
        Processor.ResetPerformanceCounters();
    }

    private static Processor.CPU_Core.PipelineControl RunUntilRetired(ulong retirementTarget)
    {
        while (true)
        {
            Processor.CPU_Core.PipelineControl control = Processor.CPU_Cores[CoreId].GetPipelineControl();
            if (control.InstructionsRetired >= retirementTarget)
            {
                return control;
            }

            if (control.CycleCount >= HardCycleLimit)
            {
                throw new InvalidOperationException(
                    $"Pipeline did not retire the program within {HardCycleLimit} cycles. " +
                    $"Retired={control.InstructionsRetired}, target={retirementTarget}, activePC=0x{Processor.CPU_Cores[CoreId].ReadActiveLivePc():X}.");
            }

            Processor.CPU_Cores[CoreId].ExecutePipelineCycle();
        }
    }

    private static void DrainPipeline(int cycles)
    {
        for (int cycle = 0; cycle < cycles; cycle++)
        {
            Processor.CPU_Cores[CoreId].ExecutePipelineCycle();
        }
    }

    private static ulong ReadUInt64(ulong address)
    {
        byte[] bytes = (Processor.MainMemory ?? throw new InvalidOperationException("Processor main memory is not initialized."))
            .ReadFromPosition(new byte[sizeof(ulong)], address, sizeof(ulong));
        return BitConverter.ToUInt64(bytes, 0);
    }

    private static void EnsureExpectedResult(string name, ulong actual, ulong expected)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException($"{name} = {actual}, expected {expected}.");
        }
    }

    private static void PrintProgramListing()
    {
        Console.WriteLine("Minimal HybridCPU ISE program:");
        Console.WriteLine("  addi    x1, x0, 5");
        Console.WriteLine("  addi    x2, x0, 7");
        Console.WriteLine("  fence");
        Console.WriteLine("  add     x3, x1, x2");
        Console.WriteLine("  fence");
        Console.WriteLine($"  sd      [0x{ResultAddress:X}], x3");
        Console.WriteLine("  fence");
        Console.WriteLine($"  ld      x4, [0x{ResultAddress:X}]");
        Console.WriteLine("  fence");
    }

    private static VliwBundleAnnotations CreateInputAnnotations(int instructionCount)
    {
        var slotMetadata = new InstructionSlotMetadata[instructionCount];
        for (int instructionIndex = 0; instructionIndex < slotMetadata.Length; instructionIndex++)
        {
            slotMetadata[instructionIndex] = new InstructionSlotMetadata(
                VtId.Create(VirtualThreadId),
                SlotMetadata.NotStealable);
        }

        return new VliwBundleAnnotations(slotMetadata);
    }

    private static void PublishBundleAnnotations(HybridCpuCompiledProgram compiledProgram)
    {
        var materializedBundles = new List<IrMaterializedBundle>(compiledProgram.BundleCount);
        for (int blockIndex = 0; blockIndex < compiledProgram.BundleLayout.BlockResults.Count; blockIndex++)
        {
            IrBasicBlockBundlingResult blockResult = compiledProgram.BundleLayout.BlockResults[blockIndex];
            for (int bundleIndex = 0; bundleIndex < blockResult.Bundles.Count; bundleIndex++)
            {
                materializedBundles.Add(blockResult.Bundles[bundleIndex]);
            }
        }

        for (int bundleIndex = 0; bundleIndex < materializedBundles.Count; bundleIndex++)
        {
            var slotMetadata = new InstructionSlotMetadata[BundleMetadata.BundleSlotCount];
            for (int slotIndex = 0; slotIndex < slotMetadata.Length; slotIndex++)
            {
                slotMetadata[slotIndex] = InstructionSlotMetadata.Default;
            }

            foreach (IrMaterializedBundleSlot slot in materializedBundles[bundleIndex].Slots)
            {
                if (slot.Instruction is null)
                {
                    continue;
                }

                slotMetadata[slot.SlotIndex] = new InstructionSlotMetadata(
                    VtId.Create(slot.Instruction.VirtualThreadId),
                    SlotMetadata.NotStealable);
            }

            ulong bundleAddress = EmissionBaseAddress + ((ulong)bundleIndex * (ulong)HybridCpuBundleSerializer.BundleSizeBytes);
            (Processor.MainMemory ?? throw new InvalidOperationException("Processor main memory is not initialized."))
                .PublishVliwBundleAnnotations(bundleAddress, new VliwBundleAnnotations(slotMetadata));
        }
    }

    private static VLIW_Instruction AddImmediate(int destinationRegister, int sourceRegister, short immediate)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.ADDI,
            PredicateMask = 0,
            Immediate = unchecked((ushort)immediate),
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                checked((byte)destinationRegister),
                checked((byte)sourceRegister),
                VLIW_Instruction.NoArchReg),
        };
    }

    private static VLIW_Instruction Binary(
        Processor.CPU_Core.InstructionsEnum opcode,
        int destinationRegister,
        int sourceRegister1,
        int sourceRegister2)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            PredicateMask = 0,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                checked((byte)destinationRegister),
                checked((byte)sourceRegister1),
                checked((byte)sourceRegister2))
        };
    }

    private static VLIW_Instruction LoadDoubleword(int destinationRegister, ulong address)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.LD,
            PredicateMask = 0,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                checked((byte)destinationRegister),
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg),
            Src2Pointer = address
        };
    }

    private static VLIW_Instruction StoreDoubleword(int sourceRegister, ulong address)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.SD,
            PredicateMask = 0,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg,
                checked((byte)sourceRegister)),
            Src2Pointer = address
        };
    }

    private static VLIW_Instruction Fence()
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.FENCE,
            PredicateMask = 0
        };
    }
}
