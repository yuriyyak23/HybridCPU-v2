using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Memory;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using HybridCPU_ISE.Tests.TestHelpers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09AtomicMemoryDefaultBindingSeamTests
{
    [Fact]
    public void DirectCompatAtomicTransaction_WhenGlobalMainMemoryIsReplacedAfterConstruction_UsesSeededCoreAndDispatcherMemory()
    {
        const int vtId = 2;
        const ulong address = 0x100;
        const uint initialWord = 0x1020_3040U;
        const uint sourceWord = 0x10U;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        var seededMemory = new Processor.MultiBankMemoryArea(bankCount: 4, bankSize: 0x1000UL);
        var replacementMemory = new Processor.MultiBankMemoryArea(bankCount: 4, bankSize: 0x10UL);

        try
        {
            Processor.MainMemory = seededMemory;
            InitializeCpuMainMemoryIdentityMap(0x1000, preserveCurrentMainMemory: true);
            InitializeMemorySubsystem();

            var core = new Processor.CPU_Core(0);
            var dispatcher = new YAKSys_Hybrid_CPU.Core.Execution.ExecutionDispatcherV4();

            core.WriteCommittedArch(vtId, 1, address);
            core.WriteCommittedArch(vtId, 2, sourceWord);
            seededMemory.WriteToPosition(BitConverter.GetBytes(initialWord), address);

            Processor.MainMemory = replacementMemory;

            var transaction =
                RetireWindowCaptureTestHelper.CaptureAndApplyExecutionDispatcherRetireWindowPublications(
                    ref core,
                    dispatcher,
                    CreateInstructionIr(InstructionsEnum.AMOADD_W, rd: 9, rs1: 1, rs2: 2),
                    core.CreateLiveCpuStateAdapter(vtId),
                    bundleSerial: 17,
                    vtId: (byte)vtId);

        Assert.Equal(RetireWindowCaptureEffectKind.Atomic, transaction.TypedEffectKind);
            Assert.Equal(SignExtendWordToUlong(initialWord), core.ReadArch(vtId, 9));

            byte[] buffer = new byte[4];
            Assert.True(seededMemory.TryReadPhysicalRange(address, buffer));
            Assert.Equal(unchecked(initialWord + sourceWord), BitConverter.ToUInt32(buffer, 0));
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
        }
    }

    [Fact]
    public void AtomicMicroOp_WhenGlobalMainMemoryIsReplacedAfterCoreConstruction_UsesCoreSeededAtomicMemoryUnit()
    {
        const ulong address = 0x180;
        const uint initialWord = 0x5566_7700U;
        const uint sourceWord = 0x11U;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        var seededMemory = new Processor.MultiBankMemoryArea(bankCount: 4, bankSize: 0x1000UL);
        var replacementMemory = new Processor.MultiBankMemoryArea(bankCount: 4, bankSize: 0x10UL);

        try
        {
            Processor.MainMemory = seededMemory;
            InitializeCpuMainMemoryIdentityMap(0x1000, preserveCurrentMainMemory: true);
            InitializeMemorySubsystem();

            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(0, 1, address);
            core.WriteCommittedArch(0, 2, sourceWord);
            seededMemory.WriteToPosition(BitConverter.GetBytes(initialWord), address);

            Processor.MainMemory = replacementMemory;

            var microOp = new AtomicMicroOp
            {
                OpCode = (uint)InstructionsEnum.AMOADD_W,
                DestRegID = 9,
                BaseRegID = 1,
                SrcRegID = 2,
                Size = 4,
                WritesRegister = true
            };
            microOp.InitializeMetadata();

            Assert.True(microOp.Execute(ref core));

            AtomicRetireOutcome outcome =
                core.AtomicMemoryUnit.ApplyRetireEffect(microOp.CreateRetireEffect());

            Assert.True(outcome.MemoryMutated);
            Assert.True(outcome.HasRegisterWriteback);
            Assert.Equal((ushort)9, outcome.RegisterDestination);
            Assert.Equal(SignExtendWordToUlong(initialWord), outcome.RegisterWritebackValue);

            byte[] buffer = new byte[4];
            Assert.True(seededMemory.TryReadPhysicalRange(address, buffer));
            Assert.Equal(unchecked(initialWord + sourceWord), BitConverter.ToUInt32(buffer, 0));
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
        }
    }

    private static InstructionIR CreateInstructionIr(
        InstructionsEnum opcode,
        ushort rd = 0,
        ushort rs1 = 0,
        ushort rs2 = 0) =>
        new()
        {
            CanonicalOpcode = opcode,
            Class = InstructionClassifier.GetClass(opcode),
            SerializationClass = InstructionClassifier.GetSerializationClass(opcode),
            Rd = (byte)rd,
            Rs1 = (byte)rs1,
            Rs2 = (byte)rs2,
            Imm = 0
        };

    private static ulong SignExtendWordToUlong(uint value) =>
        unchecked((ulong)(long)(int)value);

    private static void InitializeCpuMainMemoryIdentityMap(
        ulong size,
        bool preserveCurrentMainMemory = false)
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

    private static void InitializeMemorySubsystem()
    {
        Processor proc = default;
        Processor.Memory = new MemorySubsystem(ref proc);
    }
}
