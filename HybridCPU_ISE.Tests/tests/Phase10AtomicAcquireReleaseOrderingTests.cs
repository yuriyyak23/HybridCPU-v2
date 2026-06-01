using System;
using HybridCPU_ISE.CloseToRTL.Memory.MMU;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using YAKSys_Hybrid_CPU.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests
{
    public sealed class Phase10AtomicAcquireReleaseOrderingTests
    {
        private const byte VtId = 0;

        private static void InitializeCpuMainMemoryIdentityMap()
        {
            Processor.MainMemory = new Processor.MultiBankMemoryArea(4, 0x4000000UL);
            IOMMU.Initialize();
            IOMMU.RegisterDevice(0);
            IOMMU.Map(
                deviceID: 0,
                ioVirtualAddress: 0,
                physicalAddress: 0,
                size: 0x100000000UL,
                permissions: IOMMUAccessPermissions.ReadWrite);
        }

        private static void InitializeMemorySubsystem()
        {
            Processor proc = default;
            Processor.Memory = new MemorySubsystem(ref proc);
        }

        private static void InitializeMemory()
        {
            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();
        }

        private static InstructionIR CreateAtomicInstructionIr(
            InstructionsEnum opcode,
            bool acquireOrdering,
            bool releaseOrdering,
            byte rd = 5,
            byte rs1 = 1,
            byte rs2 = 2)
        {
            return new InstructionIR
            {
                CanonicalOpcode = opcode,
                Class = InstructionClassifier.GetClass(opcode),
                SerializationClass = InstructionClassifier.GetSerializationClass(opcode),
                Rd = rd,
                Rs1 = rs1,
                Rs2 = rs2,
                Imm = 0,
                AcquireOrdering = acquireOrdering,
                ReleaseOrdering = releaseOrdering
            };
        }

        private static AtomicRetireEffect ResolveAtomicEffect(
            ref Processor.CPU_Core core,
            InstructionsEnum opcode,
            ushort destinationRegister,
            ulong address,
            ulong sourceValue,
            bool acquireOrdering,
            bool releaseOrdering)
        {
            return core.AtomicMemoryUnit.ResolveRetireEffect(
                unchecked((ushort)opcode),
                destinationRegister,
                address,
                sourceValue,
                (int)core.CoreID,
                VtId,
                acquireOrdering,
                releaseOrdering);
        }

        private static RetireWindowCaptureSnapshot CaptureAndApplyAtomicOnly(
            ref Processor.CPU_Core core,
            AtomicRetireEffect atomicEffect)
        {
            Span<RetireRecord> retireRecords = stackalloc RetireRecord[12];
            Span<Processor.CPU_Core.RetireWindowEffect> retireEffects =
                stackalloc Processor.CPU_Core.RetireWindowEffect[8];
            PipelineEvent?[] pipelineEvents = new PipelineEvent?[8];
            var retireBatch = new Processor.CPU_Core.RetireWindowBatch(
                retireRecords,
                retireEffects,
                pipelineEvents);

            retireBatch.CaptureRetireWindowAtomicEffect(atomicEffect);
            var snapshot = new RetireWindowCaptureSnapshot(
                retireBatch.RetireRecords.ToArray(),
                retireBatch.Effects.ToArray(),
                (PipelineEvent?[])pipelineEvents.Clone(),
                retireBatch.AssistBoundaryKilledThisRetireWindow);

            core.ApplyCapturedRetireWindowBatch(ref retireBatch);
            return snapshot;
        }

        private static RetireWindowCaptureSnapshot CaptureAndApplyStoreThenAtomic(
            ref Processor.CPU_Core core,
            ulong storeAddress,
            ulong storeData,
            byte storeAccessSize,
            AtomicRetireEffect atomicEffect)
        {
            Span<RetireRecord> retireRecords = stackalloc RetireRecord[12];
            Span<Processor.CPU_Core.RetireWindowEffect> retireEffects =
                stackalloc Processor.CPU_Core.RetireWindowEffect[8];
            PipelineEvent?[] pipelineEvents = new PipelineEvent?[8];
            var retireBatch = new Processor.CPU_Core.RetireWindowBatch(
                retireRecords,
                retireEffects,
                pipelineEvents);

            retireBatch.CaptureRetireWindowScalarMemoryStore(
                storeAddress,
                storeData,
                storeAccessSize);
            retireBatch.CaptureRetireWindowAtomicEffect(atomicEffect);
            var snapshot = new RetireWindowCaptureSnapshot(
                retireBatch.RetireRecords.ToArray(),
                retireBatch.Effects.ToArray(),
                (PipelineEvent?[])pipelineEvents.Clone(),
                retireBatch.AssistBoundaryKilledThisRetireWindow);

            core.ApplyCapturedRetireWindowBatch(ref retireBatch);
            return snapshot;
        }

        private static RetireWindowCaptureSnapshot CaptureAndApplyAtomicThenStore(
            ref Processor.CPU_Core core,
            AtomicRetireEffect atomicEffect,
            ulong storeAddress,
            ulong storeData,
            byte storeAccessSize)
        {
            Span<RetireRecord> retireRecords = stackalloc RetireRecord[12];
            Span<Processor.CPU_Core.RetireWindowEffect> retireEffects =
                stackalloc Processor.CPU_Core.RetireWindowEffect[8];
            PipelineEvent?[] pipelineEvents = new PipelineEvent?[8];
            var retireBatch = new Processor.CPU_Core.RetireWindowBatch(
                retireRecords,
                retireEffects,
                pipelineEvents);

            retireBatch.CaptureRetireWindowAtomicEffect(atomicEffect);
            retireBatch.CaptureRetireWindowScalarMemoryStore(
                storeAddress,
                storeData,
                storeAccessSize);
            var snapshot = new RetireWindowCaptureSnapshot(
                retireBatch.RetireRecords.ToArray(),
                retireBatch.Effects.ToArray(),
                (PipelineEvent?[])pipelineEvents.Clone(),
                retireBatch.AssistBoundaryKilledThisRetireWindow);

            core.ApplyCapturedRetireWindowBatch(ref retireBatch);
            return snapshot;
        }

        private static uint ReadWord(ulong address) =>
            BitConverter.ToUInt32(Processor.MainMemory.ReadFromPosition(new byte[4], address, 4), 0);

        private static ulong ReadDoubleword(ulong address) =>
            BitConverter.ToUInt64(Processor.MainMemory.ReadFromPosition(new byte[8], address, 8), 0);

        private static void WriteMemory(ulong address, ulong value, byte accessSize)
        {
            Processor.MainMemory.WriteToPosition(
                accessSize == 4
                    ? BitConverter.GetBytes(unchecked((uint)value))
                    : BitConverter.GetBytes(value),
                address);
        }

        private static ulong ReadMemory(ulong address, byte accessSize) =>
            accessSize == 4 ? ReadWord(address) : ReadDoubleword(address);

        private static ulong SignExtendWordToUlong(uint value) =>
            unchecked((ulong)(long)(int)value);

        private static ulong NormalizeAtomicRegisterValue(ulong value, byte accessSize) =>
            accessSize == 4
                ? SignExtendWordToUlong(unchecked((uint)value))
                : value;

        private static ulong ComputeAddResult(ulong previousValue, ulong sourceValue, byte accessSize) =>
            accessSize == 4
                ? unchecked((uint)previousValue + (uint)sourceValue)
                : unchecked(previousValue + sourceValue);

        [Fact]
        public void AtomicAcquireReleaseBits_ReachRetireEffectWithoutEarlyPublication()
        {
            const ulong address = 0x1_7000;
            const uint initialWord = 0x0000_0010U;
            const uint sourceWord = 0x0000_0004U;
            const ulong originalDestination = 0xCAFE_BABE_DEAD_BEEFUL;

            InitializeMemory();

            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(VtId, 1, address);
            core.WriteCommittedArch(VtId, 2, sourceWord);
            core.WriteCommittedArch(VtId, 9, originalDestination);
            Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(initialWord), address);

            var dispatcher = new ExecutionDispatcherV4();
            var state = core.CreateLiveCpuStateAdapter(VtId);
            InstructionIR instruction = CreateAtomicInstructionIr(
                InstructionsEnum.AMOADD_W,
                acquireOrdering: true,
                releaseOrdering: true,
                rd: 9);

            RetireWindowCaptureSnapshot captured =
                RetireWindowCaptureTestHelper.CaptureExecutionDispatcherRetireWindowPublications(
                    dispatcher,
                    instruction,
                    state,
                    bundleSerial: 0xA10,
                    vtId: VtId);

            Assert.Equal(RetireWindowCaptureEffectKind.Atomic, captured.TypedEffectKind);
            Assert.True(captured.AtomicEffect.AcquireOrdering);
            Assert.True(captured.AtomicEffect.ReleaseOrdering);
            Assert.False(captured.HasSerializingBoundaryEffect);
            Assert.Equal(0, captured.RetireRecordCount);
            Assert.Equal(originalDestination, core.ReadArch(VtId, 9));
            Assert.Equal(initialWord, ReadWord(address));
        }

        [Fact]
        public void AtomicMicroOpMaterialization_CarriesAcquireReleaseIntoRetireEffect()
        {
            const ulong address = 0x1_7080;
            const uint initialWord = 0x0000_0010U;
            const uint sourceWord = 0x0000_0004U;

            InitializeMemory();

            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(VtId, 1, address);
            core.WriteCommittedArch(VtId, 2, sourceWord);
            Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(initialWord), address);

            MicroOp materialized = InstructionRegistry.CreateMicroOp(
                (uint)InstructionsEnum.AMOADD_W,
                new DecoderContext
                {
                    OpCode = (uint)InstructionsEnum.AMOADD_W,
                    Reg1ID = 9,
                    Reg2ID = 1,
                    Reg3ID = 2,
                    AcquireOrdering = true,
                    ReleaseOrdering = true
                });

            AtomicMicroOp atomicMicroOp = Assert.IsType<AtomicMicroOp>(materialized);
            Assert.True(atomicMicroOp.AcquireOrdering);
            Assert.True(atomicMicroOp.ReleaseOrdering);

            Assert.True(atomicMicroOp.Execute(ref core));
            AtomicRetireEffect effect = atomicMicroOp.CreateRetireEffect();

            Assert.True(effect.AcquireOrdering);
            Assert.True(effect.ReleaseOrdering);
            Assert.Equal((byte)4, effect.AccessSize);
            Assert.Equal(address, effect.Address);
            Assert.Equal(sourceWord, effect.SourceValue);
        }

        [Theory]
        [InlineData(InstructionsEnum.AMOADD_W, 4)]
        [InlineData(InstructionsEnum.AMOADD_D, 8)]
        public void ReleaseAmo_RetiresPriorStoreBeforeAtomicPublishes(
            InstructionsEnum opcode,
            byte accessSize)
        {
            const ulong address = 0x1_7100;
            const ulong initialValue = 0x0000_0000_0000_0001UL;
            const ulong priorStoreValue = 0x0000_0000_0000_0010UL;
            const ulong sourceValue = 0x0000_0000_0000_0004UL;

            InitializeMemory();

            var core = new Processor.CPU_Core(0);
            WriteMemory(address, initialValue, accessSize);
            AtomicRetireEffect effect = ResolveAtomicEffect(
                ref core,
                opcode,
                destinationRegister: 9,
                address,
                sourceValue,
                acquireOrdering: true,
                releaseOrdering: true);

            RetireWindowCaptureSnapshot snapshot = CaptureAndApplyStoreThenAtomic(
                ref core,
                address,
                priorStoreValue,
                accessSize,
                effect);

            Assert.True(snapshot.AtomicEffect.AcquireOrdering);
            Assert.True(snapshot.AtomicEffect.ReleaseOrdering);
            Assert.False(snapshot.HasSerializingBoundaryEffect);
            Assert.Equal(
                NormalizeAtomicRegisterValue(priorStoreValue, accessSize),
                core.ReadArch(VtId, 9));
            Assert.Equal(
                ComputeAddResult(priorStoreValue, sourceValue, accessSize),
                ReadMemory(address, accessSize));
        }

        [Theory]
        [InlineData(InstructionsEnum.AMOADD_W, 4)]
        [InlineData(InstructionsEnum.AMOADD_D, 8)]
        public void AcquireAmo_RetiresAtomicBeforeLaterStoreBecomesVisible(
            InstructionsEnum opcode,
            byte accessSize)
        {
            const ulong address = 0x1_7200;
            const ulong initialValue = 0x0000_0000_0000_0001UL;
            const ulong sourceValue = 0x0000_0000_0000_0004UL;
            const ulong laterStoreValue = 0x0000_0000_0000_0040UL;

            InitializeMemory();

            var core = new Processor.CPU_Core(0);
            WriteMemory(address, initialValue, accessSize);
            AtomicRetireEffect effect = ResolveAtomicEffect(
                ref core,
                opcode,
                destinationRegister: 9,
                address,
                sourceValue,
                acquireOrdering: true,
                releaseOrdering: true);

            RetireWindowCaptureSnapshot snapshot = CaptureAndApplyAtomicThenStore(
                ref core,
                effect,
                address,
                laterStoreValue,
                accessSize);

            Assert.True(snapshot.AtomicEffect.AcquireOrdering);
            Assert.True(snapshot.AtomicEffect.ReleaseOrdering);
            Assert.False(snapshot.HasSerializingBoundaryEffect);
            Assert.Equal(
                NormalizeAtomicRegisterValue(initialValue, accessSize),
                core.ReadArch(VtId, 9));
            Assert.Equal(laterStoreValue, ReadMemory(address, accessSize));
        }

        [Theory]
        [InlineData(InstructionsEnum.AMOADD_W, 4)]
        [InlineData(InstructionsEnum.AMOADD_D, 8)]
        public void ReleaseOnlyAmo_RetiresPriorStoreBeforeAtomicPublishes(
            InstructionsEnum opcode,
            byte accessSize)
        {
            const ulong address = 0x1_7280;
            const ulong initialValue = 0x0000_0000_0000_0001UL;
            const ulong priorStoreValue = 0x0000_0000_0000_0018UL;
            const ulong sourceValue = 0x0000_0000_0000_0004UL;

            InitializeMemory();

            var core = new Processor.CPU_Core(0);
            WriteMemory(address, initialValue, accessSize);
            AtomicRetireEffect effect = ResolveAtomicEffect(
                ref core,
                opcode,
                destinationRegister: 9,
                address,
                sourceValue,
                acquireOrdering: false,
                releaseOrdering: true);

            RetireWindowCaptureSnapshot snapshot = CaptureAndApplyStoreThenAtomic(
                ref core,
                address,
                priorStoreValue,
                accessSize,
                effect);

            Assert.False(snapshot.AtomicEffect.AcquireOrdering);
            Assert.True(snapshot.AtomicEffect.ReleaseOrdering);
            Assert.False(snapshot.HasSerializingBoundaryEffect);
            Assert.Equal(
                NormalizeAtomicRegisterValue(priorStoreValue, accessSize),
                core.ReadArch(VtId, 9));
            Assert.Equal(
                ComputeAddResult(priorStoreValue, sourceValue, accessSize),
                ReadMemory(address, accessSize));
        }

        [Theory]
        [InlineData(InstructionsEnum.AMOADD_W, 4)]
        [InlineData(InstructionsEnum.AMOADD_D, 8)]
        public void AcquireOnlyAmo_RetiresAtomicBeforeLaterStoreBecomesVisible(
            InstructionsEnum opcode,
            byte accessSize)
        {
            const ulong address = 0x1_72C0;
            const ulong initialValue = 0x0000_0000_0000_0001UL;
            const ulong sourceValue = 0x0000_0000_0000_0004UL;
            const ulong laterStoreValue = 0x0000_0000_0000_0048UL;

            InitializeMemory();

            var core = new Processor.CPU_Core(0);
            WriteMemory(address, initialValue, accessSize);
            AtomicRetireEffect effect = ResolveAtomicEffect(
                ref core,
                opcode,
                destinationRegister: 9,
                address,
                sourceValue,
                acquireOrdering: true,
                releaseOrdering: false);

            RetireWindowCaptureSnapshot snapshot = CaptureAndApplyAtomicThenStore(
                ref core,
                effect,
                address,
                laterStoreValue,
                accessSize);

            Assert.True(snapshot.AtomicEffect.AcquireOrdering);
            Assert.False(snapshot.AtomicEffect.ReleaseOrdering);
            Assert.False(snapshot.HasSerializingBoundaryEffect);
            Assert.Equal(
                NormalizeAtomicRegisterValue(initialValue, accessSize),
                core.ReadArch(VtId, 9));
            Assert.Equal(laterStoreValue, ReadMemory(address, accessSize));
        }

        [Theory]
        [InlineData(InstructionsEnum.LR_W, 4)]
        [InlineData(InstructionsEnum.LR_D, 8)]
        public void AcquireReleaseLoadReserved_ObservesPriorStoreBeforeRegisterPublication(
            InstructionsEnum opcode,
            byte accessSize)
        {
            const ulong address = 0x1_7300;
            const ulong initialValue = 0x0000_0000_0000_0001UL;
            const ulong priorStoreValue = 0x0000_0000_0000_0020UL;

            InitializeMemory();

            var core = new Processor.CPU_Core(0);
            WriteMemory(address, initialValue, accessSize);
            AtomicRetireEffect effect = ResolveAtomicEffect(
                ref core,
                opcode,
                destinationRegister: 9,
                address,
                sourceValue: 0,
                acquireOrdering: true,
                releaseOrdering: true);

            RetireWindowCaptureSnapshot snapshot = CaptureAndApplyStoreThenAtomic(
                ref core,
                address,
                priorStoreValue,
                accessSize,
                effect);

            Assert.True(snapshot.AtomicEffect.AcquireOrdering);
            Assert.True(snapshot.AtomicEffect.ReleaseOrdering);
            Assert.False(snapshot.HasSerializingBoundaryEffect);
            Assert.Equal(
                NormalizeAtomicRegisterValue(priorStoreValue, accessSize),
                core.ReadArch(VtId, 9));
            Assert.Equal(priorStoreValue, ReadMemory(address, accessSize));
        }

        [Theory]
        [InlineData(InstructionsEnum.LR_W, 4)]
        [InlineData(InstructionsEnum.LR_D, 8)]
        public void AcquireReleaseLoadReserved_RetiresBeforeLaterStoreBecomesVisible(
            InstructionsEnum opcode,
            byte accessSize)
        {
            const ulong address = 0x1_7400;
            const ulong initialValue = 0x0000_0000_0000_0001UL;
            const ulong laterStoreValue = 0x0000_0000_0000_0080UL;

            InitializeMemory();

            var core = new Processor.CPU_Core(0);
            WriteMemory(address, initialValue, accessSize);
            AtomicRetireEffect effect = ResolveAtomicEffect(
                ref core,
                opcode,
                destinationRegister: 9,
                address,
                sourceValue: 0,
                acquireOrdering: true,
                releaseOrdering: true);

            RetireWindowCaptureSnapshot snapshot = CaptureAndApplyAtomicThenStore(
                ref core,
                effect,
                address,
                laterStoreValue,
                accessSize);

            Assert.True(snapshot.AtomicEffect.AcquireOrdering);
            Assert.True(snapshot.AtomicEffect.ReleaseOrdering);
            Assert.False(snapshot.HasSerializingBoundaryEffect);
            Assert.Equal(
                NormalizeAtomicRegisterValue(initialValue, accessSize),
                core.ReadArch(VtId, 9));
            Assert.Equal(laterStoreValue, ReadMemory(address, accessSize));
        }

        [Theory]
        [InlineData(InstructionsEnum.LR_W, 4)]
        [InlineData(InstructionsEnum.LR_D, 8)]
        public void AcquireOnlyLoadReserved_RetiresBeforeLaterStoreBecomesVisible(
            InstructionsEnum opcode,
            byte accessSize)
        {
            const ulong address = 0x1_7480;
            const ulong initialValue = 0x0000_0000_0000_0001UL;
            const ulong laterStoreValue = 0x0000_0000_0000_0088UL;

            InitializeMemory();

            var core = new Processor.CPU_Core(0);
            WriteMemory(address, initialValue, accessSize);
            AtomicRetireEffect effect = ResolveAtomicEffect(
                ref core,
                opcode,
                destinationRegister: 9,
                address,
                sourceValue: 0,
                acquireOrdering: true,
                releaseOrdering: false);

            RetireWindowCaptureSnapshot snapshot = CaptureAndApplyAtomicThenStore(
                ref core,
                effect,
                address,
                laterStoreValue,
                accessSize);

            Assert.True(snapshot.AtomicEffect.AcquireOrdering);
            Assert.False(snapshot.AtomicEffect.ReleaseOrdering);
            Assert.False(snapshot.HasSerializingBoundaryEffect);
            Assert.Equal(
                NormalizeAtomicRegisterValue(initialValue, accessSize),
                core.ReadArch(VtId, 9));
            Assert.Equal(laterStoreValue, ReadMemory(address, accessSize));
        }

        [Theory]
        [InlineData(InstructionsEnum.LR_W, InstructionsEnum.SC_W, 4)]
        [InlineData(InstructionsEnum.LR_D, InstructionsEnum.SC_D, 8)]
        public void AcquireReleaseStoreConditional_ReleaseSideSeesPriorInvalidatingStore(
            InstructionsEnum lrOpcode,
            InstructionsEnum scOpcode,
            byte accessSize)
        {
            const ulong address = 0x1_7500;
            const ulong initialValue = 0x0000_0000_0000_0001UL;
            const ulong priorStoreValue = 0x0000_0000_0000_0010UL;
            const ulong conditionalStoreValue = 0x0000_0000_0000_0040UL;

            InitializeMemory();

            var core = new Processor.CPU_Core(0);
            WriteMemory(address, initialValue, accessSize);
            AtomicRetireEffect lrEffect = ResolveAtomicEffect(
                ref core,
                lrOpcode,
                destinationRegister: 8,
                address,
                sourceValue: 0,
                acquireOrdering: false,
                releaseOrdering: false);
            CaptureAndApplyAtomicOnly(ref core, lrEffect);

            AtomicRetireEffect scEffect = ResolveAtomicEffect(
                ref core,
                scOpcode,
                destinationRegister: 9,
                address,
                conditionalStoreValue,
                acquireOrdering: true,
                releaseOrdering: true);

            RetireWindowCaptureSnapshot snapshot = CaptureAndApplyStoreThenAtomic(
                ref core,
                address,
                priorStoreValue,
                accessSize,
                scEffect);

            Assert.True(snapshot.AtomicEffect.AcquireOrdering);
            Assert.True(snapshot.AtomicEffect.ReleaseOrdering);
            Assert.False(snapshot.HasSerializingBoundaryEffect);
            Assert.Equal(1UL, core.ReadArch(VtId, 9));
            Assert.Equal(priorStoreValue, ReadMemory(address, accessSize));
        }

        [Theory]
        [InlineData(InstructionsEnum.LR_W, InstructionsEnum.SC_W, 4)]
        [InlineData(InstructionsEnum.LR_D, InstructionsEnum.SC_D, 8)]
        public void AcquireReleaseStoreConditional_AcquireSideRetiresBeforeLaterStore(
            InstructionsEnum lrOpcode,
            InstructionsEnum scOpcode,
            byte accessSize)
        {
            const ulong address = 0x1_7600;
            const ulong initialValue = 0x0000_0000_0000_0001UL;
            const ulong conditionalStoreValue = 0x0000_0000_0000_0040UL;
            const ulong laterStoreValue = 0x0000_0000_0000_0080UL;

            InitializeMemory();

            var core = new Processor.CPU_Core(0);
            WriteMemory(address, initialValue, accessSize);
            AtomicRetireEffect lrEffect = ResolveAtomicEffect(
                ref core,
                lrOpcode,
                destinationRegister: 8,
                address,
                sourceValue: 0,
                acquireOrdering: false,
                releaseOrdering: false);
            CaptureAndApplyAtomicOnly(ref core, lrEffect);

            AtomicRetireEffect scEffect = ResolveAtomicEffect(
                ref core,
                scOpcode,
                destinationRegister: 9,
                address,
                conditionalStoreValue,
                acquireOrdering: true,
                releaseOrdering: true);

            RetireWindowCaptureSnapshot snapshot = CaptureAndApplyAtomicThenStore(
                ref core,
                scEffect,
                address,
                laterStoreValue,
                accessSize);

            Assert.True(snapshot.AtomicEffect.AcquireOrdering);
            Assert.True(snapshot.AtomicEffect.ReleaseOrdering);
            Assert.False(snapshot.HasSerializingBoundaryEffect);
            Assert.Equal(0UL, core.ReadArch(VtId, 9));
            Assert.Equal(laterStoreValue, ReadMemory(address, accessSize));
        }

        [Theory]
        [InlineData(InstructionsEnum.LR_W, InstructionsEnum.SC_W, 4)]
        [InlineData(InstructionsEnum.LR_D, InstructionsEnum.SC_D, 8)]
        public void ReleaseOnlyStoreConditional_ReleaseSideSeesPriorInvalidatingStore(
            InstructionsEnum lrOpcode,
            InstructionsEnum scOpcode,
            byte accessSize)
        {
            const ulong address = 0x1_7680;
            const ulong initialValue = 0x0000_0000_0000_0001UL;
            const ulong priorStoreValue = 0x0000_0000_0000_0018UL;
            const ulong conditionalStoreValue = 0x0000_0000_0000_0048UL;

            InitializeMemory();

            var core = new Processor.CPU_Core(0);
            WriteMemory(address, initialValue, accessSize);
            AtomicRetireEffect lrEffect = ResolveAtomicEffect(
                ref core,
                lrOpcode,
                destinationRegister: 8,
                address,
                sourceValue: 0,
                acquireOrdering: false,
                releaseOrdering: false);
            CaptureAndApplyAtomicOnly(ref core, lrEffect);

            AtomicRetireEffect scEffect = ResolveAtomicEffect(
                ref core,
                scOpcode,
                destinationRegister: 9,
                address,
                conditionalStoreValue,
                acquireOrdering: false,
                releaseOrdering: true);

            RetireWindowCaptureSnapshot snapshot = CaptureAndApplyStoreThenAtomic(
                ref core,
                address,
                priorStoreValue,
                accessSize,
                scEffect);

            Assert.False(snapshot.AtomicEffect.AcquireOrdering);
            Assert.True(snapshot.AtomicEffect.ReleaseOrdering);
            Assert.False(snapshot.HasSerializingBoundaryEffect);
            Assert.Equal(1UL, core.ReadArch(VtId, 9));
            Assert.Equal(priorStoreValue, ReadMemory(address, accessSize));
        }

        [Theory]
        [InlineData(InstructionsEnum.LR_W, InstructionsEnum.SC_W, 4)]
        [InlineData(InstructionsEnum.LR_D, InstructionsEnum.SC_D, 8)]
        public void AcquireOnlyStoreConditional_AcquireSideRetiresBeforeLaterStore(
            InstructionsEnum lrOpcode,
            InstructionsEnum scOpcode,
            byte accessSize)
        {
            const ulong address = 0x1_76C0;
            const ulong initialValue = 0x0000_0000_0000_0001UL;
            const ulong conditionalStoreValue = 0x0000_0000_0000_0048UL;
            const ulong laterStoreValue = 0x0000_0000_0000_0088UL;

            InitializeMemory();

            var core = new Processor.CPU_Core(0);
            WriteMemory(address, initialValue, accessSize);
            AtomicRetireEffect lrEffect = ResolveAtomicEffect(
                ref core,
                lrOpcode,
                destinationRegister: 8,
                address,
                sourceValue: 0,
                acquireOrdering: false,
                releaseOrdering: false);
            CaptureAndApplyAtomicOnly(ref core, lrEffect);

            AtomicRetireEffect scEffect = ResolveAtomicEffect(
                ref core,
                scOpcode,
                destinationRegister: 9,
                address,
                conditionalStoreValue,
                acquireOrdering: true,
                releaseOrdering: false);

            RetireWindowCaptureSnapshot snapshot = CaptureAndApplyAtomicThenStore(
                ref core,
                scEffect,
                address,
                laterStoreValue,
                accessSize);

            Assert.True(snapshot.AtomicEffect.AcquireOrdering);
            Assert.False(snapshot.AtomicEffect.ReleaseOrdering);
            Assert.False(snapshot.HasSerializingBoundaryEffect);
            Assert.Equal(0UL, core.ReadArch(VtId, 9));
            Assert.Equal(laterStoreValue, ReadMemory(address, accessSize));
        }
    }
}
