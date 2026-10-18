using System.Reflection;
using HybridCPU.Compiler.Cil;
using HybridCPU.Compiler.Core.IR;

internal static class BranchEntrySmoke
{
    public static void ReportNextGate()
    {
        byte[] core = File.ReadAllBytes(typeof(object).Assembly.Location);
        var module = new ManagedBodyWorldModuleV1(core, "corelib-duplicate-control");
        var result = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2)
            .ImportBodyWorld(new(ManagedBodyWorldModeV1.StandaloneRestrictedModule,
                File.ReadAllBytes(typeof(ClosedComparable).Assembly.Location), "next-gate",
                [new(typeof(ClosedComparable).FullName!, nameof(ClosedComparable.Ping))], [],
                MetadataOnlyModules: [module, module]));
        Console.WriteLine($"CoreLib={typeof(object).Assembly.Location}; bytes={core.Length}; duplicateBytes={2L * core.Length}; budget={32 * 1024 * 1024}; status={result.Status}");
        foreach (var diagnostic in result.Diagnostics) Console.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
        if (!result.Diagnostics.Any(d => d.Code == "HCSCF-BUDGET2010") || result.Diagnostics.Any(d => d.Code == "HCSCF-BODYWORLD0003"))
            throw new InvalidOperationException("Next gate budget precedence was not reproduced");
        Console.WriteLine("PASS next-gate localization 1; failed=0; skipped=0; Classification: FixtureDrift; Qualification: no");
    }
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }

    public static void Run()
    {
        checks = 0;
        var graph = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2)
            .ImportBodyWorld(new(ManagedBodyWorldModeV1.StandaloneRestrictedModule,
                File.ReadAllBytes(typeof(BranchEntryFixture).Assembly.Location), "branch-entry-smoke",
                [new(typeof(BranchEntryFixture).FullName!, nameof(BranchEntryFixture.Update))], []));
        Check(graph.Status == RestrictedCilImportStatusV1.Success, string.Join(';', graph.Diagnostics));
        var program = graph.Methods.Single(m => m.Identity.MethodName == nameof(BranchEntryFixture.Update)).Import.Program!;
        var schedule = new HybridCpuLocalListScheduler().ScheduleProgram(program);
        var bundles = new HybridCpuBundleFormer().BundleProgram(schedule);
        var resource = (HybridCpuMiiResourceModelV1)typeof(ScalarControlFlowV2ObjectLinkerV1)
            .GetField("ResourceModel", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var allocation = new HybridCpuScheduleAwareRegisterAllocatorV1().Allocate(schedule, bundles,
            resourceModel: resource, options: HybridCpuRegisterAllocationOptionsV1.Qualification);
        Check(allocation.Status == IrRegisterAllocationStatusV1.Allocated, allocation.Reason);
        var layout = allocation.FinalBundles;
        var placements = new Dictionary<int, (int Bundle, int Slot, int Block)>();
        var entries = new Dictionary<int, int>();
        int ordinal = 0;
        foreach (var block in layout.BlockResults)
        {
            entries.Add(block.BlockId, ordinal);
            foreach (var bundle in block.Bundles)
            {
                foreach (var slot in bundle.Slots.Where(s => s.Instruction is not null))
                    placements.Add(slot.Instruction!.Index, (ordinal, slot.SlotIndex, block.BlockId));
                ordinal++;
            }
        }
        var branch = layout.Program.Instructions.Single(i => i.StableIdentity.EndsWith(":split-return:b0:b2", StringComparison.Ordinal));
        var target = layout.Program.Instructions.Single(i => i.Index == branch.Annotation.ResolvedBranchTargetInstructionIndex);
        var copy = layout.Program.Instructions.Single(i => i.StableIdentity.Contains("null-check-arg-copy", StringComparison.Ordinal));
        var at = placements[target.Index];
        Check(target.Index == layout.Program.BasicBlocks.Single(b => b.Id == at.Block).StartInstructionIndex,
            "Repro target must be a semantic CFG entry, not an interior label.");
        Check(entries[at.Block] == 19 && at.Bundle == 20 && placements[copy.Index].Bundle == 19,
            "Fixture drift: require original target bundle 20 and null-check entry copy bundle 19.");
        var lowered = new HybridCpuBundleLowerer().LowerProgram(layout);
        RelocationCancellationSmoke.Run(layout, lowered);
        var relocated = HybridCpuControlFlowRelocationResolver.ApplyRelocations(layout, lowered);
        byte[] bytes = new HybridCpuBundleSerializer().SerializeProgram(relocated);
        HybridCpuInstructionWord Word(IrInstruction instruction, byte[] image)
        {
            var p = placements[instruction.Index];
            var bundle = new HybridCpuInstructionBundle();
            Check(bundle.TryReadBytes(image, p.Bundle * 256), "Decode serialized bundle");
            return bundle.GetInstruction(p.Slot);
        }
        long Destination(IrInstruction instruction, byte[] image)
        {
            var word = Word(instruction, image);
            Check(word.Src2Pointer == 0, "No target sideband");
            if (instruction.Opcode != HybridCpuOpcode.JALR)
                return placements[instruction.Index].Bundle * 256L + (short)word.Immediate;
            string identity = instruction.StableIdentity.EndsWith(":long-branch-low", StringComparison.Ordinal)
                ? instruction.StableIdentity[..^":long-branch-low".Length] : instruction.StableIdentity;
            var high = layout.Program.Instructions.Single(i => i.StableIdentity == identity + ":long-branch-high");
            var highWord = Word(high, image);
            Check(HybridCpuInstructionWord.TryUnpackArchRegs(highWord.Word1, out byte highRd, out _, out _) && highRd == 5 &&
                  HybridCpuInstructionWord.TryUnpackArchRegs(word.Word1, out byte lowRd, out byte lowRs, out _) && lowRd == HybridCpuInstructionWord.NoArchReg && lowRs == 5,
                "Physical AUIPC x5 / non-linking JALR x5 operands");
            return placements[high.Index].Bundle * 256L + ((long)(short)highWord.Immediate << 12) + (short)word.Immediate;
        }
        long destination = Destination(branch, bytes);
        var copyWord = Word(copy, bytes);
        Check(HybridCpuInstructionWord.TryUnpackArchRegs(copyWord.Word1, out byte rd, out byte rs1, out _), "Physical copy operands");
        Check(copyWord.OpCode == (uint)HybridCpuOpcode.ADDI && copyWord.Immediate == 0 &&
              rd == 10 && rs1 == 18,
            "Encoded required preparation must be ADDI x10,x18,0");
        Console.WriteLine($"branch-entry: target={destination / 256}; entry=19; semantic-target=20; copy=ADDI x10,x18,0; skipped={destination > 19 * 256L}");
        Check(destination == 19 * 256L, "Encoded split-return bypasses required null-check-arg-copy at bundle 19");
        // Word-level execution of the required entry preparation, not guest execution.
        ulong[] registers = new ulong[32]; registers[18] = 0x280546e0; registers[10] = ulong.MaxValue;
        registers[rd] = unchecked(registers[rs1] + (ulong)(long)(short)copyWord.Immediate);
        Check(registers[10] == registers[18], "Entry preparation restores reference argument x10");
        foreach (var instruction in layout.Program.Instructions.Where(i => i.Annotation.ResolvedBranchTargetInstructionIndex.HasValue &&
                     i.Annotation.ControlFlowKind is IrControlFlowKind.ConditionalBranch or IrControlFlowKind.UnconditionalBranch))
        {
            int targetIndex = instruction.Annotation.ResolvedBranchTargetInstructionIndex!.Value;
            var p = placements[targetIndex];
            var block = layout.Program.BasicBlocks.Single(b => b.Id == p.Block);
            long expected = (targetIndex == block.StartInstructionIndex ? entries[p.Block] : p.Bundle) * 256L;
            Check(Destination(instruction, bytes) == expected, "Encoded synthetic/edge/frame target");
        }
        // A deliberately interior target stays at its exact instruction, even in the same block.
        var interior = layout.Program.Instructions.First(i => placements[i.Index].Block == at.Block && placements[i.Index].Bundle > at.Bundle);
        var interiorBranch = branch with { Annotation = branch.Annotation with { ResolvedBranchTargetInstructionIndex = interior.Index } };
        var changedProgram = layout.Program with { Instructions = layout.Program.Instructions.Select(i => i.Index == branch.Index ? interiorBranch : i).ToArray() };
        var changedSchedule = new IrProgramSchedule(changedProgram, layout.ProgramSchedule.DependencyGraph, layout.ProgramSchedule.BlockSchedules);
        var changedLayout = new IrProgramBundlingResult(changedSchedule, layout.BlockResults);
        byte[] interiorBytes = new HybridCpuBundleSerializer().SerializeProgram(HybridCpuControlFlowRelocationResolver.ApplyRelocations(changedLayout, lowered));
        Check(Destination(interiorBranch, interiorBytes) == placements[interior.Index].Bundle * 256L, "Legitimate interior target preserved");
        IrProgramBundlingResult WithBranch(IrInstruction replacement) => new(
            new IrProgramSchedule(layout.Program with
            {
                Instructions = layout.Program.Instructions.Select(i => i.Index == branch.Index ? replacement : i).ToArray()
            }, layout.ProgramSchedule.DependencyGraph, layout.ProgramSchedule.BlockSchedules), layout.BlockResults);
        var shortBranch = branch with { Opcode = HybridCpuOpcode.JAL };
        var shortWords = lowered.ToArray();
        var source = placements[branch.Index];
        var sourceBundle = shortWords[source.Bundle];
        var shortWord = sourceBundle.GetInstruction(source.Slot);
        shortWord.OpCode = (uint)HybridCpuOpcode.JAL;
        sourceBundle.SetInstruction(source.Slot, shortWord); shortWords[source.Bundle] = sourceBundle;
        var shortBytes = new HybridCpuBundleSerializer().SerializeProgram(
            HybridCpuControlFlowRelocationResolver.ApplyRelocations(WithBranch(shortBranch), shortWords));
        Check(Destination(shortBranch, shortBytes) == 19 * 256L, "Signed short branch entry encoding");
        var sameBlockBranch = shortBranch with { Annotation = shortBranch.Annotation with { ResolvedBranchTargetInstructionIndex = branch.Index } };
        var sameBlockBytes = new HybridCpuBundleSerializer().SerializeProgram(
            HybridCpuControlFlowRelocationResolver.ApplyRelocations(WithBranch(sameBlockBranch), shortWords));
        Check(Destination(sameBlockBranch, sameBlockBytes) == source.Bundle * 256L, "Same-block interior target preserved");
        void Reject(IrProgramBundlingResult bad, string expected)
        {
            try { HybridCpuControlFlowRelocationResolver.ApplyRelocations(bad, lowered); }
            catch (InvalidOperationException ex) when (ex.Message.Contains(expected, StringComparison.Ordinal)) { checks++; return; }
            throw new InvalidOperationException("Expected fail-closed relocation: " + expected);
        }
        Reject(WithBranch(branch with { Annotation = branch.Annotation with { ResolvedBranchTargetInstructionIndex = int.MaxValue } }), "target");
        Reject(WithBranch(branch with { Annotation = branch.Annotation with { ResolvedBranchTargetInstructionIndex = null } }), "unresolved");
        Reject(new(layout.ProgramSchedule, layout.BlockResults.Concat([layout.BlockResults[0]]).ToArray()), "ambiguous");
        Check(layout.Program.Instructions.Any(i => i.StableIdentity.Contains("prologue", StringComparison.Ordinal)) &&
              layout.Program.Instructions.Any(i => i.StableIdentity.Contains("epilogue", StringComparison.Ordinal)), "Real allocation frame controls present");
        Console.WriteLine($"PASS branch-entry {checks} checks; failed=0; skipped=0 (compiler words, no guest qualification)");
    }
}

public sealed class BranchEntryBox { public int Value; }
public static class BranchEntryFixture
{
    public static void Update(BranchEntryBox box, int index, bool flag)
    {
        int num = -1;
        if (flag) num = 0;
        box.Value = index + num;
    }
}
