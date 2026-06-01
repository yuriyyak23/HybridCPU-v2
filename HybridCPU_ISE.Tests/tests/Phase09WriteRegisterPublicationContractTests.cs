using System;
using System.Linq;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09WriteRegisterPublicationContractTests
    {
        [Fact]
        public void T9_09a_AllRegisteredFactories_PublishCanonicalWriteRegistersForWritebackContours()
        {
            string[] failures = InstructionRegistry.GetRegisteredOpcodes()
                .Where(opCode => !InstructionRegistry.IsCustomAcceleratorOpcode(opCode))
                .Where(opCode => !IsRegistryRawFactoryFailClosedBoundary(opCode))
                .Select(ValidateRegisteredOpcodeWriteContract)
                .Where(failure => failure is not null)
                .Cast<string>()
                .ToArray();

            Assert.True(
                failures.Length == 0,
                "Registered writeback contours must publish explicit canonical WriteRegisters. Failures:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, failures));
        }

        [Fact]
        public void T9_09c_DmaStreamComputeRegistryRawFactory_RemainsFailClosed()
        {
            var context = CreateCanonicalDecoderContext((uint)Processor.CPU_Core.InstructionsEnum.DmaStreamCompute);

            DecodeProjectionFaultException exception = Assert.Throws<DecodeProjectionFaultException>(
                () => InstructionRegistry.CreateMicroOp(
                    (uint)Processor.CPU_Core.InstructionsEnum.DmaStreamCompute,
                    context));

            Assert.Contains("guard-accepted descriptor sideband", exception.Message, StringComparison.Ordinal);
            Assert.Contains("not the canonical lane6 descriptor path", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void T9_09b_DecodedBundleSlotDescriptor_DoesNotFallbackToDestRegIdForWriteRegisters()
        {
            var microOp = new IncompleteWritePublicationMicroOp();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => DecodedBundleSlotDescriptor.Create(0, microOp));

            Assert.Contains("DestRegID is convenience only", exception.Message, StringComparison.Ordinal);
        }

        private static string? ValidateRegisteredOpcodeWriteContract(uint opCode)
        {
            try
            {
                PrepareOpcodeSpecificEnvironment(opCode);
                DecoderContext context = CreateCanonicalDecoderContext(opCode);
                MicroOp microOp = InstructionRegistry.CreateMicroOp(opCode, context);
                MicroOpAdmissionMetadata admission = microOp.AdmissionMetadata;
                DecodedBundleSlotDescriptor slot = DecodedBundleSlotDescriptor.Create(0, microOp);

                if (microOp.WritesRegister != admission.WritesRegister)
                {
                    return $"{DescribeOpcode(opCode)}: MicroOp.WritesRegister={microOp.WritesRegister} " +
                           $"but AdmissionMetadata.WritesRegister={admission.WritesRegister}.";
                }

                if (!admission.WritesRegister)
                {
                    if (admission.WriteRegisters.Count != 0)
                    {
                        return $"{DescribeOpcode(opCode)}: WritesRegister=false but AdmissionMetadata.WriteRegisters=" +
                               $"[{string.Join(", ", admission.WriteRegisters)}].";
                    }

                    if (slot.WriteRegisters.Count != 0)
                    {
                        return $"{DescribeOpcode(opCode)}: slot projection published unexpected WriteRegisters=" +
                               $"[{string.Join(", ", slot.WriteRegisters)}].";
                    }

                    return null;
                }

                if (admission.WriteRegisters.Count == 0)
                {
                    return $"{DescribeOpcode(opCode)}: WritesRegister=true but AdmissionMetadata.WriteRegisters is empty.";
                }

                if (!HaveSameRegisters(admission.WriteRegisters, slot.WriteRegisters))
                {
                    return $"{DescribeOpcode(opCode)}: slot WriteRegisters [{string.Join(", ", slot.WriteRegisters)}] " +
                           $"do not match admission [{string.Join(", ", admission.WriteRegisters)}].";
                }

                if (HasExplicitDestinationRegister(microOp.DestRegID) &&
                    !admission.WriteRegisters.Contains((int)microOp.DestRegID))
                {
                    return $"{DescribeOpcode(opCode)}: DestRegID={microOp.DestRegID} is missing from canonical " +
                           $"WriteRegisters [{string.Join(", ", admission.WriteRegisters)}].";
                }

                return null;
            }
            catch (Exception exception)
            {
                return $"{DescribeOpcode(opCode)}: {exception.GetType().Name}: {exception.Message}";
            }
        }

        private static DecoderContext CreateCanonicalDecoderContext(uint opCode)
        {
            if (opCode is (uint)Processor.CPU_Core.InstructionsEnum.FENCE or
                (uint)Processor.CPU_Core.InstructionsEnum.FENCE_I)
            {
                return new DecoderContext
                {
                    OpCode = opCode,
                    PredicateMask = 0,
                };
            }

            ulong vectorPrimaryPointer = IsVectorFmaOpcode(opCode) ? 0x240UL : 0x220UL;
            ulong vectorSecondaryPointer =
                IsVectorFmaOpcode(opCode) || IsIndexedVectorMemoryOpcode(opCode)
                    ? 0x340UL
                    : IsSingleSurfaceVectorOpcode(opCode)
                        ? 0UL
                        : 0x320UL;

            DecoderContext context = new DecoderContext
            {
                OpCode = opCode,
                Immediate = ResolveCanonicalImmediate(opCode),
                HasImmediate = true,
                DataType = ResolveCanonicalDecoderDataType(opCode),
                HasDataType = true,
                IndexedAddressing = IsIndexedVectorMemoryOpcode(opCode),
                Is2DAddressing = false,
                HasVectorAddressingContour = true,
                VectorPrimaryPointer = vectorPrimaryPointer,
                VectorSecondaryPointer = vectorSecondaryPointer,
                VectorStreamLength = ResolveCanonicalVectorStreamLength(opCode),
                VectorStride = 0,
                VectorRowStride = 0,
                TailAgnostic = false,
                MaskAgnostic = false,
                HasVectorPayload = true,
                MemoryAddress = 0x1000,
                HasMemoryAddress = true,
                PackedRegisterTriplet = VLIW_Instruction.PackArchRegs(7, 5, 6),
                HasPackedRegisterTriplet = true,
                Reg1ID = 7,
                Reg2ID = 5,
                Reg3ID = 6,
                AuxData = 0x1000,
                PredicateMask = 0xFF,
                OwnerThreadId = 0,
                MemoryDomainId = 0
            };

            ApplyOpcodeSpecificRegisterAbi(opCode, ref context);

            if (opCode is (uint)Processor.CPU_Core.InstructionsEnum.SLLIW or
                (uint)Processor.CPU_Core.InstructionsEnum.SRLIW or
                (uint)Processor.CPU_Core.InstructionsEnum.SRAIW or
                (uint)Processor.CPU_Core.InstructionsEnum.SEXT_W or
                (uint)Processor.CPU_Core.InstructionsEnum.ZEXT_W)
            {
                context.Reg3ID = 0;
                context.PackedRegisterTriplet = VLIW_Instruction.PackArchRegs(7, 5, 0);
                context.HasPackedRegisterTriplet = true;

                if (opCode is (uint)Processor.CPU_Core.InstructionsEnum.SEXT_W or
                    (uint)Processor.CPU_Core.InstructionsEnum.ZEXT_W)
                {
                    context.Immediate = 0;
                    context.HasImmediate = true;
                }
            }

            return context;
        }

        private static void PrepareOpcodeSpecificEnvironment(uint opCode)
        {
            if (!IsVectorFmaOpcode(opCode) &&
                !IsIndexedVectorMemoryOpcode(opCode))
            {
                return;
            }

            Processor.MainMemory = new Processor.MultiBankMemoryArea(4, 0x4000000UL);
            IOMMU.Initialize();
            IOMMU.RegisterDevice(0);
            IOMMU.Map(
                deviceID: 0,
                ioVirtualAddress: 0,
                physicalAddress: 0,
                size: 0x100000000UL,
                permissions: IOMMUAccessPermissions.ReadWrite);

            Processor proc = default;
            Processor.Memory = new MemorySubsystem(ref proc);

            if (IsVectorFmaOpcode(opCode))
            {
                byte[] descriptor = new byte[20];
                BitConverter.GetBytes(0x440UL).CopyTo(descriptor, 0);
                BitConverter.GetBytes(0x540UL).CopyTo(descriptor, 8);
                BitConverter.GetBytes((ushort)4).CopyTo(descriptor, 16);
                BitConverter.GetBytes((ushort)4).CopyTo(descriptor, 18);
                Processor.MainMemory.WriteToPosition(descriptor, 0x340UL);
                return;
            }

            if (IsIndexedVectorMemoryOpcode(opCode))
            {
                byte[] descriptor = new byte[32];
                BitConverter.GetBytes(0x640UL).CopyTo(descriptor, 0);
                BitConverter.GetBytes(0x740UL).CopyTo(descriptor, 8);
                BitConverter.GetBytes((ushort)4).CopyTo(descriptor, 16);
                descriptor[18] = 0;
                descriptor[19] = 0;
                Processor.MainMemory.WriteToPosition(descriptor, 0x340UL);
                Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(0U), 0x740UL);
                Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(1U), 0x744UL);
                Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(2U), 0x748UL);
                Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(3U), 0x74CUL);
            }
        }

        private static ushort ResolveCanonicalImmediate(uint opCode)
        {
            return 0;
        }

        private static uint ResolveCanonicalVectorStreamLength(uint opCode)
        {
            return opCode == (uint)Processor.CPU_Core.InstructionsEnum.VPERM2
                ? 2U
                : 4U;
        }

        private static byte ResolveCanonicalDecoderDataType(uint opCode)
        {
            if (opCode == (uint)Processor.CPU_Core.InstructionsEnum.Move)
                return 0;

            if (opCode == (uint)Processor.CPU_Core.InstructionsEnum.Move_Num)
                return 1;

            if (opCode == (uint)Processor.CPU_Core.InstructionsEnum.VDOT_WIDE)
            {
                return new VLIW_Instruction
                {
                    DataTypeValue = DataTypeEnum.FLOAT16
                }.DataType;
            }

            if (opCode == (uint)Processor.CPU_Core.InstructionsEnum.VZEXT)
            {
                return new VLIW_Instruction
                {
                    DataTypeValue = DataTypeEnum.UINT16
                }.DataType;
            }

            DataTypeEnum publicationDataType =
                OpcodeRegistry.GetInfo(opCode)?.Flags.HasFlag(InstructionFlags.FloatingPoint) == true
                    ? DataTypeEnum.FLOAT32
                    : DataTypeEnum.INT32;

            return new VLIW_Instruction
            {
                DataTypeValue = publicationDataType
            }.DataType;
        }

        private static bool IsVectorFmaOpcode(uint opCode)
        {
            return opCode == (uint)Processor.CPU_Core.InstructionsEnum.VFMADD ||
                   opCode == (uint)Processor.CPU_Core.InstructionsEnum.VFMSUB ||
                   opCode == (uint)Processor.CPU_Core.InstructionsEnum.VFNMADD ||
                   opCode == (uint)Processor.CPU_Core.InstructionsEnum.VFNMSUB;
        }

        private static bool IsIndexedVectorMemoryOpcode(uint opCode)
        {
            return opCode == (uint)Processor.CPU_Core.InstructionsEnum.VGATHER ||
                   opCode == (uint)Processor.CPU_Core.InstructionsEnum.VSCATTER;
        }

        private static bool IsSingleSurfaceVectorOpcode(uint opCode)
        {
            return opCode == (uint)Processor.CPU_Core.InstructionsEnum.VSLIDE1UP ||
                   opCode == (uint)Processor.CPU_Core.InstructionsEnum.VSLIDE1DOWN ||
                   opCode == (uint)Processor.CPU_Core.InstructionsEnum.VTRANSPOSE;
        }

        private static void ApplyOpcodeSpecificRegisterAbi(
            uint opCode,
            ref DecoderContext context)
        {
            if (opCode is
                (uint)Processor.CPU_Core.InstructionsEnum.CLZ or
                (uint)Processor.CPU_Core.InstructionsEnum.DSC_STATUS or
                (uint)Processor.CPU_Core.InstructionsEnum.ACCEL_STATUS)
            {
                context.Reg3ID = 0;
                context.PackedRegisterTriplet =
                    VLIW_Instruction.PackArchRegs(
                        checked((byte)context.Reg1ID),
                        checked((byte)context.Reg2ID),
                        0);
                context.HasPackedRegisterTriplet = true;
                return;
            }

            if (opCode is
                (uint)Processor.CPU_Core.InstructionsEnum.RDCYCLE or
                (uint)Processor.CPU_Core.InstructionsEnum.DSC_QUERY_CAPS or
                (uint)Processor.CPU_Core.InstructionsEnum.ACCEL_QUERY_CAPS or
                (uint)Processor.CPU_Core.InstructionsEnum.ACCEL_SUBMIT)
            {
                context.Reg2ID = 0;
                context.Reg3ID = 0;
                context.PackedRegisterTriplet =
                    VLIW_Instruction.PackArchRegs(
                        checked((byte)context.Reg1ID),
                        0,
                        0);
                context.HasPackedRegisterTriplet = true;
            }
        }

        private static bool IsRegistryRawFactoryFailClosedBoundary(uint opCode)
        {
            return opCode == (uint)Processor.CPU_Core.InstructionsEnum.DmaStreamCompute;
        }

        private static string DescribeOpcode(uint opCode)
        {
            return $"0x{opCode:X} ({(Processor.CPU_Core.InstructionsEnum)opCode})";
        }

        private static bool HaveSameRegisters(
            System.Collections.Generic.IReadOnlyList<int> left,
            System.Collections.Generic.IReadOnlyList<int> right)
        {
            if (left.Count != right.Count)
                return false;

            for (int i = 0; i < left.Count; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }

        private static bool HasExplicitDestinationRegister(ushort destinationRegisterId)
        {
            return destinationRegisterId != 0 &&
                   destinationRegisterId != VLIW_Instruction.NoReg &&
                   destinationRegisterId != VLIW_Instruction.NoArchReg;
        }

        private sealed class IncompleteWritePublicationMicroOp : MicroOp
        {
            public IncompleteWritePublicationMicroOp()
            {
                OpCode = 0xDEADU;
                DestRegID = 7;
                WritesRegister = true;
                Class = MicroOpClass.Alu;
                InstructionClass = YAKSys_Hybrid_CPU.Arch.InstructionClass.ScalarAlu;
                SerializationClass = YAKSys_Hybrid_CPU.Arch.SerializationClass.Free;
                SetClassFlexiblePlacement(SlotClass.AluClass);
            }

            public override bool Execute(ref Processor.CPU_Core core) => true;

            public override string GetDescription() => "Incomplete write-publication micro-op";
        }
    }
}

