using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Legality;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using HybridCPU.Compiler.Core;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Comprehensive tests for VLIW Decoder and Instruction Format.
    /// Validates 256-bit instruction encoding/decoding, bundle structure, and EILP flag extraction.
    /// </summary>
    public class VLIWDecoderTests
    {
        #region Instruction Encoding Tests

        [Fact]
        public void Encoder_EncodeScalar_ShouldCreateValid256BitInstruction()
        {
            // Arrange
            uint opCode = (uint)Processor.CPU_Core.InstructionsEnum.Addition;
            DataTypeEnum dataType = DataTypeEnum.INT32;
            ushort reg1 = 1, reg2 = 2, reg3 = 3;

            // Act
            var inst = InstructionEncoder.EncodeScalar(opCode, dataType, reg1, reg2, reg3);

            // Assert
            Assert.Equal(opCode, inst.OpCode);
            Assert.Equal((byte)dataType, inst.DataType);
            Assert.Equal(1U, inst.StreamLength); // Scalar = 1 element
            Assert.True(VLIW_Instruction.TryUnpackArchRegs(
                inst.Word1,
                out byte rd,
                out byte rs1,
                out byte rs2));
            Assert.Equal((byte)1, rd);
            Assert.Equal((byte)2, rs1);
            Assert.Equal((byte)3, rs2);
        }

        [Fact]
        public void Encoder_EncodeVector1D_ShouldSetAddressingMode()
        {
            // Arrange
            uint opCode = 100;
            ulong destPtr = 0x1000;
            ulong srcPtr = 0x2000;
            ulong length = 32;
            ushort stride = 4;

            // Act
            var inst = InstructionEncoder.EncodeVector1D(
                opCode, DataTypeEnum.FLOAT32,
                destPtr, srcPtr, length, stride);

            // Assert
            Assert.Equal(destPtr, inst.DestSrc1Pointer);
            Assert.Equal(srcPtr, inst.Src2Pointer);
            Assert.Equal(32U, inst.StreamLength);
            Assert.Equal(stride, inst.Stride);
            Assert.False(inst.Indexed);
            Assert.False(inst.Is2D);
        }

        [Fact]
        public void Encoder_EncodeVector2D_ShouldSet2DFlag()
        {
            // Arrange
            uint opCode = 101;
            ulong destPtr = 0x1000;
            ushort colStride = 4;
            ushort rowStride = 64;

            // Act
            var inst = InstructionEncoder.EncodeVector2D(
                opCode, DataTypeEnum.FLOAT32,
                destPtr, 0x2000, 32, colStride, rowStride, 8);

            // Assert
            Assert.True(inst.Is2D);
            Assert.False(inst.Indexed);
            Assert.Equal(colStride, inst.Stride);
            Assert.Equal(rowStride, inst.RowStride);
        }

        [Fact]
        public void Encoder_EncodeVectorIndexed_ShouldSetIndexedFlag()
        {
            // Arrange
            uint opCode = 102;
            ulong descriptorAddr = 0x3000;

            // Act
            var inst = InstructionEncoder.EncodeVectorIndexed(
                opCode, DataTypeEnum.INT32,
                0x1000, descriptorAddr, 16);

            // Assert
            Assert.True(inst.Indexed);
            Assert.False(inst.Is2D);
            Assert.Equal(descriptorAddr, inst.Src2Pointer);
        }

        [Fact]
        public void Instruction_PackArchRegs_ShouldRejectRegistersOutsideArchitecturalRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VLIW_Instruction.PackArchRegs(32, 1, 2));
        }

        #endregion

        #region Bundle Format Tests

        [Fact]
        public void Bundle_Size_ShouldBe256Bytes()
        {
            // Verify bundle format constraint
            int slotsPerBundle = 8;
            int bytesPerSlot = 32;
            int expectedBundleSize = 256;

            Assert.Equal(expectedBundleSize, slotsPerBundle * bytesPerSlot);
        }

        [Fact]
        public void Instruction_Structure_ShouldHaveExpectedFields()
        {
            // Verify VLIW_Instruction has required fields for 256-bit format
            var inst = new VLIW_Instruction();
            inst.OpCode = 100;
            inst.DataType = 1;
            inst.StreamLength = 32;
            inst.DestSrc1Pointer = 0x1000;
            inst.Src2Pointer = 0x2000;

            // Assert - Verify fields are accessible
            Assert.Equal(100U, inst.OpCode);
            Assert.Equal((byte)1, inst.DataType);
            Assert.Equal(32U, inst.StreamLength);
            Assert.Equal(0x1000UL, inst.DestSrc1Pointer);
            Assert.Equal(0x2000UL, inst.Src2Pointer);
        }

        #endregion

        #region Predicate Mask Tests

        [Fact]
        public void Instruction_PredicateMask_ShouldBePreserved()
        {
            // Arrange
            byte predicateMask = 0x55; // Alternating bits

            // Act
            var inst = InstructionEncoder.EncodeVector1D(
                100, DataTypeEnum.INT32,
                0x1000, 0x2000, 32, 0, predicateMask);

            // Assert
            Assert.Equal(predicateMask, inst.PredicateMask);
        }

        [Fact]
        public void Instruction_MaskAgnostic_ShouldSetFlag()
        {
            // Act
            var inst = InstructionEncoder.EncodeVector1D(
                100, DataTypeEnum.INT32,
                0x1000, 0x2000, 32, 0, 0, 0,
                tailAgnostic: true, maskAgnostic: true);

            // Assert
            Assert.True(inst.TailAgnostic);
            Assert.True(inst.MaskAgnostic);
        }

        #endregion

        #region Comparison and Reduction Tests

        [Fact]
        public void Encoder_EncodeVectorComparison_ShouldStoreDestInImmediate()
        {
            // Arrange
            byte destPredicateReg = 5;

            // Act
            var inst = InstructionEncoder.EncodeVectorComparison(
                opCode: 200, // Use generic opcode for comparison
                dataType: DataTypeEnum.INT32,
                src1Ptr: 0x1000,
                src2Ptr: 0x2000,
                streamLength: 32,
                destPredicateReg: destPredicateReg);

            // Assert
            Assert.Equal(destPredicateReg, inst.Immediate);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void Instruction_ZeroStreamLength_ShouldBeValid()
        {
            // Act
            var inst = InstructionEncoder.EncodeVector1D(
                100, DataTypeEnum.INT32,
                0x1000, 0x2000, 0); // Zero length

            // Assert
            Assert.Equal(0U, inst.StreamLength);
        }

        [Fact]
        public void Instruction_MaxStreamLength_ShouldBeValid()
        {
            // Arrange
            ulong maxLength = uint.MaxValue;

            // Act
            var inst = InstructionEncoder.EncodeVector1D(
                100, DataTypeEnum.INT32,
                0x1000, 0x2000, maxLength);

            // Assert
            Assert.Equal(maxLength, inst.StreamLength);
        }

        [Fact]
        public void Instruction_HighAddresses_ShouldBePreserved()
        {
            // Arrange
            ulong highAddr1 = 0xFFFFFFFF_FFFFFFF0;
            ulong highAddr2 = 0xFFFFFFFF_FFFFFFE0;

            // Act
            var inst = InstructionEncoder.EncodeVector1D(
                100, DataTypeEnum.INT64,
                highAddr1, highAddr2, 16);

            // Assert
            Assert.Equal(highAddr1, inst.DestSrc1Pointer);
            Assert.Equal(highAddr2, inst.Src2Pointer);
        }

        [Fact]
        public void CompatDecoder_CompilerEmittedAbsoluteMoveStore_CanonicalizesToStoreRegisterContract()
        {
            IDecoderFrontend decoder = RetainedCompatDecoderTestFactory.CreateRetainedCompatDecoder();
            var inst = new VLIW_Instruction
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Move,
                DataType = 2,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                    6,
                    VLIW_Instruction.NoArchReg,
                    VLIW_Instruction.NoArchReg),
                Src2Pointer = 0x280,
                StreamLength = 0,
                Stride = 0
            };

            InstructionIR ir = decoder.Decode(in inst, slotIndex: 0);

            Assert.Equal(Processor.CPU_Core.InstructionsEnum.Store, ir.CanonicalOpcode.ToInstructionsEnum());
            Assert.Equal(InstructionClass.Memory, ir.Class);
            Assert.Equal(SerializationClass.MemoryOrdered, ir.SerializationClass);
            Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rd);
            Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs1);
            Assert.Equal((byte)6, ir.Rs2);
        }

        [Fact]
        public void CanonicalDecoder_CompilerEmittedAbsoluteMoveStore_FailsClosedAsCompatOnlyContour()
        {
            var decoder = new VliwDecoderV4();
            var inst = new VLIW_Instruction
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Move,
                DataType = 2,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                    6,
                    VLIW_Instruction.NoArchReg,
                    VLIW_Instruction.NoArchReg),
                Src2Pointer = 0x280,
                StreamLength = 0,
                Stride = 0
            };

            InvalidOpcodeException exception = Assert.Throws<InvalidOpcodeException>(
                () => decoder.Decode(in inst, slotIndex: 0));

            Assert.False(exception.IsProhibited);
            Assert.Contains("compat contour", exception.Message);
            Assert.Contains("compat", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CompatDecoder_CompilerEmittedAbsoluteMoveLoad_CanonicalizesToLoadRegisterContract()
        {
            IDecoderFrontend decoder = RetainedCompatDecoderTestFactory.CreateRetainedCompatDecoder();
            var inst = new VLIW_Instruction
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Move,
                DataType = 3,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                    7,
                    VLIW_Instruction.NoArchReg,
                    VLIW_Instruction.NoArchReg),
                Src2Pointer = 0x2C0,
                StreamLength = 0,
                Stride = 0
            };

            InstructionIR ir = decoder.Decode(in inst, slotIndex: 0);

            Assert.Equal(Processor.CPU_Core.InstructionsEnum.Load, ir.CanonicalOpcode.ToInstructionsEnum());
            Assert.Equal(InstructionClass.Memory, ir.Class);
            Assert.Equal(SerializationClass.Free, ir.SerializationClass);
            Assert.Equal((byte)7, ir.Rd);
            Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs1);
            Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs2);
        }

        [Fact]
        public void CanonicalDecoder_CompilerEmittedAbsoluteMoveLoad_FailsClosedAsCompatOnlyContour()
        {
            var decoder = new VliwDecoderV4();
            var inst = new VLIW_Instruction
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Move,
                DataType = 3,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                    7,
                    VLIW_Instruction.NoArchReg,
                    VLIW_Instruction.NoArchReg),
                Src2Pointer = 0x2C0,
                StreamLength = 0,
                Stride = 0
            };

            InvalidOpcodeException exception = Assert.Throws<InvalidOpcodeException>(
                () => decoder.Decode(in inst, slotIndex: 0));

            Assert.False(exception.IsProhibited);
            Assert.Contains("compat contour", exception.Message);
            Assert.Contains("compat", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Decoder_RetainedDualWriteMoveDt4_FailsClosedAsInvalidOpcode()
        {
            var decoder = new VliwDecoderV4();
            var inst = new VLIW_Instruction
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Move,
                DataType = 4,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(4, 5, 0),
                Src2Pointer = 0x300,
                StreamLength = 0,
                Stride = 0
            };

            InvalidOpcodeException exception = Assert.Throws<InvalidOpcodeException>(
                () => decoder.Decode(in inst, slotIndex: 2));

            Assert.Equal(2, exception.SlotIndex);
            Assert.False(exception.IsProhibited);
            Assert.Contains("Move DT=4", exception.Message);
        }

        [Fact]
        public void Decoder_RetainedTripleDestinationMoveDt5_FailsClosedAsInvalidOpcode()
        {
            var decoder = new VliwDecoderV4();
            var inst = new VLIW_Instruction
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Move,
                DataType = 5,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(4, 5, 6),
                Src2Pointer = 0x300,
                StreamLength = 0,
                Stride = 0
            };

            InvalidOpcodeException exception = Assert.Throws<InvalidOpcodeException>(
                () => decoder.Decode(in inst, slotIndex: 3));

            Assert.Equal(3, exception.SlotIndex);
            Assert.False(exception.IsProhibited);
            Assert.Contains("Move DT=5", exception.Message);
        }

        [Theory]
        [InlineData(Processor.CPU_Core.InstructionsEnum.Interrupt, 4)]
        [InlineData(Processor.CPU_Core.InstructionsEnum.InterruptReturn, 5)]
        public void Decoder_RetainedInterruptContours_FailClosedAsInvalidOpcode(
            Processor.CPU_Core.InstructionsEnum opcode,
            int slotIndex)
        {
            var decoder = new VliwDecoderV4();
            var inst = new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(1, 2, 3),
                Src2Pointer = 0x300,
                StreamLength = 0,
                Stride = 0
            };

            InvalidOpcodeException exception = Assert.Throws<InvalidOpcodeException>(
                () => decoder.Decode(in inst, slotIndex));

            Assert.Equal(slotIndex, exception.SlotIndex);
            Assert.False(exception.IsProhibited);
            Assert.Contains($"0x{((uint)opcode):X}", exception.Message);
            Assert.Contains("typed mainline retire/boundary carrier", exception.Message);
        }

        [Theory]
        [InlineData(14u, 6)]
        [InlineData(15u, 7)]
        [InlineData(18u, 8)]
        public void Decoder_RetainedCallReturnJumpWrappers_RemainProhibited(
            uint rawOpcode,
            int slotIndex)
        {
            var decoder = new VliwDecoderV4();
            var inst = new VLIW_Instruction
            {
                OpCode = rawOpcode,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(1, 2, 3),
                Src2Pointer = 0x340,
                StreamLength = 0,
                Stride = 0
            };

            InvalidOpcodeException exception = Assert.Throws<InvalidOpcodeException>(
                () => decoder.Decode(in inst, slotIndex));

            Assert.Equal(slotIndex, exception.SlotIndex);
            Assert.True(exception.IsProhibited);
            Assert.Contains("is not part of ISA v4 canonical surface", exception.Message);
        }

        [Theory]
        [InlineData(14u, "0xE")]
        [InlineData(15u, "0xF")]
        [InlineData(18u, "0x12")]
        public void IsProhibited_NormalizesNumericOpcodeAliases(
            uint rawOpcode,
            string hexIdentifier)
        {
            Assert.True(VliwDecoderV4.IsProhibited(Processor.CPU_Core.IsaOpcode.FromRawValue(rawOpcode)));
            Assert.True(VliwDecoderV4.IsProhibited(rawOpcode.ToString()));
            Assert.True(VliwDecoderV4.IsProhibited(hexIdentifier));
        }

        [Theory]
        [InlineData(Processor.CPU_Core.InstructionsEnum.MTILE_MACC, 6)]
        [InlineData(Processor.CPU_Core.InstructionsEnum.MTRANSPOSE, 7)]
        public void Decoder_UnsupportedOptionalMatrixContours_FailClosedAsInvalidOpcode(
            Processor.CPU_Core.InstructionsEnum opcode,
            int slotIndex)
        {
            var decoder = new VliwDecoderV4();
            var inst = new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(1, 2, 3),
                Src2Pointer = 0x380,
                StreamLength = 0,
                Stride = 0
            };

            InvalidOpcodeException exception = Assert.Throws<InvalidOpcodeException>(
                () => decoder.Decode(in inst, slotIndex));

            Assert.Equal(slotIndex, exception.SlotIndex);
            Assert.False(exception.IsProhibited);
            Assert.Contains("optional matrix contour", exception.Message);
            Assert.Contains("scalar ALU register truth", exception.Message);
        }

        [Theory]
        [InlineData(45u, 8)]
        [InlineData(52u, 9)]
        public void Decoder_UnsupportedOptionalScalarContours_FailClosedAsInvalidOpcode(
            uint rawOpcode,
            int slotIndex)
        {
            var decoder = new VliwDecoderV4();
            var inst = new VLIW_Instruction
            {
                OpCode = rawOpcode,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(1, 2, 3),
                Src2Pointer = 0x388,
                StreamLength = 0,
                Stride = 0
            };

            InvalidOpcodeException exception = Assert.Throws<InvalidOpcodeException>(
                () => decoder.Decode(in inst, slotIndex));

            Assert.Equal(slotIndex, exception.SlotIndex);
            Assert.False(exception.IsProhibited);
            Assert.Contains($"{rawOpcode}", exception.Message);
            Assert.Contains("uses unsupported optional", exception.Message);
        }

        [Fact]
        public void Decoder_UnsupportedOptionalScalarXfmacContour_FailsClosedAsInvalidOpcode()
        {
            var decoder = new VliwDecoderV4();
            var inst = new VLIW_Instruction
            {
                OpCode = 55u,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(1, 2, 3),
                Src2Pointer = 0x390,
                StreamLength = 0,
                Stride = 0
            };

            InvalidOpcodeException exception = Assert.Throws<InvalidOpcodeException>(
                () => decoder.Decode(in inst, slotIndex: 8));

            Assert.Equal(8, exception.SlotIndex);
            Assert.False(exception.IsProhibited);
            Assert.Contains($"{55}", exception.Message);
            Assert.Contains("uses unsupported optional", exception.Message);
        }

        [Theory]
        [InlineData(Processor.CPU_Core.InstructionsEnum.MTILE_LOAD, 8)]
        [InlineData(Processor.CPU_Core.InstructionsEnum.MTILE_STORE, 9)]
        public void Decoder_UnsupportedOptionalMatrixMemoryContours_FailClosedAsInvalidOpcode(
            Processor.CPU_Core.InstructionsEnum opcode,
            int slotIndex)
        {
            var decoder = new VliwDecoderV4();
            var inst = new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(1, 2, 3),
                Src2Pointer = 0x3C0,
                StreamLength = 4,
                Stride = 4
            };

            InvalidOpcodeException exception = Assert.Throws<InvalidOpcodeException>(
                () => decoder.Decode(in inst, slotIndex));

            Assert.Equal(slotIndex, exception.SlotIndex);
            Assert.False(exception.IsProhibited);
            Assert.Contains("optional matrix memory contour", exception.Message);
            Assert.Contains("memory placement/register truth", exception.Message);
        }

        [Theory]
        [InlineData(Processor.CPU_Core.InstructionsEnum.VSETVEXCPMASK, 11)]
        [InlineData(Processor.CPU_Core.InstructionsEnum.VSETVEXCPPRI, 12)]
        public void Decoder_VectorExceptionControlOpcodes_CanonicalizeToCsrSourceOnlyRegisterContract(
            Processor.CPU_Core.InstructionsEnum opcode,
            byte sourceRegister)
        {
            var decoder = new VliwDecoderV4();
            var inst = new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                    VLIW_Instruction.NoArchReg,
                    sourceRegister,
                    VLIW_Instruction.NoArchReg),
                StreamLength = 0,
                Stride = 0
            };

            InstructionIR ir = decoder.Decode(in inst, slotIndex: 0);

            Assert.Equal(opcode, ir.CanonicalOpcode.ToInstructionsEnum());
            Assert.Equal(InstructionClass.Csr, ir.Class);
            Assert.Equal(SerializationClass.FullSerial, ir.SerializationClass);
            Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rd);
            Assert.Equal(sourceRegister, ir.Rs1);
            Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs2);
        }

        [Theory]
        [InlineData(Processor.CPU_Core.InstructionsEnum.Load)]
        [InlineData(Processor.CPU_Core.InstructionsEnum.Store)]
        [InlineData(Processor.CPU_Core.InstructionsEnum.VSETVEXCPMASK)]
        [InlineData(Processor.CPU_Core.InstructionsEnum.VSETVEXCPPRI)]
        public void OpcodeRegistry_UsesPackedArchRegisterWord1_CoversClassifierOnlyCanonicalContours(
            Processor.CPU_Core.InstructionsEnum opcode)
        {
            Assert.True(OpcodeRegistry.UsesPackedArchRegisterWord1(opcode));
        }

        [Theory]
        [InlineData(Processor.CPU_Core.InstructionsEnum.VLOAD)]
        [InlineData(Processor.CPU_Core.InstructionsEnum.VSTORE)]
        public void OpcodeRegistry_UsesPackedArchRegisterWord1_DoesNotReinterpretVectorTransferContours(
            Processor.CPU_Core.InstructionsEnum opcode)
        {
            Assert.False(OpcodeRegistry.UsesPackedArchRegisterWord1(opcode));
        }

        [Theory]
        [InlineData(Processor.CPU_Core.InstructionsEnum.VLOAD, SerializationClass.Free)]
        [InlineData(Processor.CPU_Core.InstructionsEnum.VSTORE, SerializationClass.MemoryOrdered)]
        public void Decoder_VectorTransferOpcodes_PublishCanonicalVectorMemorySurfaceWithoutPackedScalarRegs(
            Processor.CPU_Core.InstructionsEnum opcode,
            SerializationClass expectedSerializationClass)
        {
            var decoder = new VliwDecoderV4();
            var inst = new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0x0F,
                DestSrc1Pointer = 0x200,
                Src2Pointer = 0x300,
                StreamLength = 8,
                Stride = 4
            };

            InstructionIR ir = decoder.Decode(in inst, slotIndex: 6);

            Assert.Equal(opcode, ir.CanonicalOpcode.ToInstructionsEnum());
            Assert.Equal(InstructionClass.Memory, ir.Class);
            Assert.Equal(expectedSerializationClass, ir.SerializationClass);
            Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rd);
            Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs1);
            Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs2);
        }

        [Fact]
        public void Decoder_Vsetvl_CanonicalizesToSystemSingletonRegisterContract()
        {
            var decoder = new VliwDecoderV4();
            var inst = new VLIW_Instruction
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.VSETVL,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(4, 5, 6),
                StreamLength = 0,
                Stride = 0
            };

            InstructionIR ir = decoder.Decode(in inst, slotIndex: 0);

            Assert.Equal(Processor.CPU_Core.InstructionsEnum.VSETVL, ir.CanonicalOpcode.ToInstructionsEnum());
            Assert.Equal(InstructionClass.System, ir.Class);
            Assert.Equal(SerializationClass.FullSerial, ir.SerializationClass);
            Assert.Equal((byte)4, ir.Rd);
            Assert.Equal((byte)5, ir.Rs1);
            Assert.Equal((byte)6, ir.Rs2);
        }

        [Fact]
        public void Decoder_Vsetvli_CanonicalizesCurrentImmediateVtypeContourToSystemSingletonRegisterContract()
        {
            var decoder = new VliwDecoderV4();
            var inst = new VLIW_Instruction
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.VSETVLI,
                DataTypeValue = DataTypeEnum.UINT16,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                    7,
                    8,
                    VLIW_Instruction.NoArchReg),
                StreamLength = 1,
                Stride = 0
            };

            InstructionIR ir = decoder.Decode(in inst, slotIndex: 0);

            Assert.Equal(Processor.CPU_Core.InstructionsEnum.VSETVLI, ir.CanonicalOpcode.ToInstructionsEnum());
            Assert.Equal(InstructionClass.System, ir.Class);
            Assert.Equal(SerializationClass.FullSerial, ir.SerializationClass);
            Assert.Equal((byte)7, ir.Rd);
            Assert.Equal((byte)8, ir.Rs1);
            Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs2);
        }

        [Fact]
        public void Decoder_Vsetivli_CanonicalizesImmediateAvlContourToSystemSingletonRegisterContract()
        {
            var decoder = new VliwDecoderV4();
            var inst = new VLIW_Instruction
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.VSETIVLI,
                DataTypeValue = DataTypeEnum.INT16,
                PredicateMask = 0xFF,
                Immediate = 13,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                    9,
                    VLIW_Instruction.NoArchReg,
                    VLIW_Instruction.NoArchReg),
                StreamLength = 0,
                Stride = 0
            };

            InstructionIR ir = decoder.Decode(in inst, slotIndex: 0);

            Assert.Equal(Processor.CPU_Core.InstructionsEnum.VSETIVLI, ir.CanonicalOpcode.ToInstructionsEnum());
            Assert.Equal(InstructionClass.System, ir.Class);
            Assert.Equal(SerializationClass.FullSerial, ir.SerializationClass);
            Assert.Equal((byte)9, ir.Rd);
            Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs1);
            Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs2);
        }

        [Fact]
        public void Decoder_Vpopc_CanonicalizesScalarResultRegisterFromImmediateNibble()
        {
            var decoder = new VliwDecoderV4();
            var inst = new VLIW_Instruction
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.VPOPC,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                Immediate = (ushort)(1 | (6 << 8)),
                StreamLength = 8
            };

            InstructionIR ir = decoder.Decode(in inst, slotIndex: 0);

            Assert.Equal(Processor.CPU_Core.InstructionsEnum.VPOPC, ir.CanonicalOpcode.ToInstructionsEnum());
            Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
            Assert.Equal(SerializationClass.Free, ir.SerializationClass);
            Assert.Equal((byte)6, ir.Rd);
            Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs1);
            Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs2);
        }

        [Theory]
        [InlineData(Processor.CPU_Core.InstructionsEnum.VADD)]
        [InlineData(Processor.CPU_Core.InstructionsEnum.VSQRT)]
        public void Decoder_SingleElementFetchedVectorCompute_DoesNotCanonicalizeScalarRegisterFacts(
            Processor.CPU_Core.InstructionsEnum opcode)
        {
            var decoder = new VliwDecoderV4();
            var inst = new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(9, 1, 2),
                StreamLength = 1
            };

            InstructionIR ir = decoder.Decode(in inst, slotIndex: 0);

            Assert.Equal(InstructionClass.ScalarAlu, ir.Class);
            Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rd);
            Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs1);
            Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs2);
        }

        [Theory]
        [InlineData(Processor.CPU_Core.InstructionsEnum.VADD)]
        [InlineData(Processor.CPU_Core.InstructionsEnum.VSQRT)]
        public void CanonicalSingleElementFetchedVectorCompute_DoesNotPublishScalarLegalityOrMaterializerRegisterFacts(
            Processor.CPU_Core.InstructionsEnum opcode)
        {
            var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
            rawSlots[0] = new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(9, 1, 2),
                StreamLength = 1
            };

            var decoder = new VliwDecoderV4();
            DecodedInstructionBundle bundle = decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x8000, bundleSerial: 11);
            BundleLegalityDescriptor legality = new BundleLegalityAnalyzer().Analyze(bundle);
            MicroOp?[] carrierBundle = DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, bundle);
            MicroOp microOp = Assert.IsAssignableFrom<MicroOp>(carrierBundle[0]);

            DecodedInstruction slot = bundle.GetDecodedSlot(0);
            Assert.Equal(VLIW_Instruction.NoArchReg, slot.Instruction!.Rd);
            Assert.Equal(VLIW_Instruction.NoArchReg, slot.Instruction.Rs1);
            Assert.Equal(VLIW_Instruction.NoArchReg, slot.Instruction.Rs2);
            Assert.Equal(0UL, legality.DependencySummary!.Value.ReadRegisterMask);
            Assert.Equal(0UL, legality.DependencySummary.Value.WriteRegisterMask);
            Assert.Empty(microOp.AdmissionMetadata.ReadRegisters);
            Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
            Assert.False(microOp.AdmissionMetadata.WritesRegister);
        }

        [Fact]
        public void CanonicalSingleElementFetchedVpopc_PreservesDedicatedScalarResultContour()
        {
            var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
            rawSlots[0] = new VLIW_Instruction
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.VPOPC,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                Immediate = (ushort)(3 | (6 << 8)),
                StreamLength = 1
            };

            var decoder = new VliwDecoderV4();
            DecodedInstructionBundle bundle = decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x8100, bundleSerial: 12);
            BundleLegalityDescriptor legality = new BundleLegalityAnalyzer().Analyze(bundle);
            MicroOp?[] carrierBundle = DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, bundle);
            MicroOp microOp = Assert.IsAssignableFrom<MicroOp>(carrierBundle[0]);

            DecodedInstruction slot = bundle.GetDecodedSlot(0);
            Assert.Equal((byte)6, slot.Instruction!.Rd);
            Assert.Equal(VLIW_Instruction.NoArchReg, slot.Instruction.Rs1);
            Assert.Equal(VLIW_Instruction.NoArchReg, slot.Instruction.Rs2);
            Assert.NotEqual(0UL, legality.DependencySummary!.Value.WriteRegisterMask & (1UL << 6));
            Assert.Contains(6, microOp.AdmissionMetadata.WriteRegisters);
            Assert.True(microOp.AdmissionMetadata.WritesRegister);
        }

        [Theory]
        [InlineData(Processor.CPU_Core.InstructionsEnum.JumpIfEqual, Processor.CPU_Core.InstructionsEnum.BEQ, 3, 4, 3, 4)]
        [InlineData(Processor.CPU_Core.InstructionsEnum.JumpIfNotEqual, Processor.CPU_Core.InstructionsEnum.BNE, 5, 6, 5, 6)]
        [InlineData(Processor.CPU_Core.InstructionsEnum.JumpIfBelow, Processor.CPU_Core.InstructionsEnum.BLTU, 7, 8, 7, 8)]
        [InlineData(Processor.CPU_Core.InstructionsEnum.JumpIfBelowOrEqual, Processor.CPU_Core.InstructionsEnum.BGEU, 9, 10, 10, 9)]
        [InlineData(Processor.CPU_Core.InstructionsEnum.JumpIfAbove, Processor.CPU_Core.InstructionsEnum.BLTU, 11, 12, 12, 11)]
        [InlineData(Processor.CPU_Core.InstructionsEnum.JumpIfAboveOrEqual, Processor.CPU_Core.InstructionsEnum.BGEU, 13, 14, 13, 14)]
        public void CompatDecoder_CompilerEmittedLegacyConditionalWrapper_CanonicalizesToCanonicalUnsignedBranchContract(
            Processor.CPU_Core.InstructionsEnum rawOpcode,
            Processor.CPU_Core.InstructionsEnum expectedOpcode,
            byte firstOperandRegister,
            byte secondOperandRegister,
            byte expectedRs1,
            byte expectedRs2)
        {
            IDecoderFrontend decoder = RetainedCompatDecoderTestFactory.CreateRetainedCompatDecoder();
            var inst = new VLIW_Instruction
            {
                OpCode = (uint)rawOpcode,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                    2,
                    firstOperandRegister,
                    secondOperandRegister),
                Src2Pointer = 0x4100,
                StreamLength = 0,
                Stride = 0
            };

            InstructionIR ir = decoder.Decode(in inst, slotIndex: 0);

            Assert.Equal(expectedOpcode, ir.CanonicalOpcode.ToInstructionsEnum());
            Assert.Equal(InstructionClass.ControlFlow, ir.Class);
            Assert.Equal(SerializationClass.Free, ir.SerializationClass);
            Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rd);
            Assert.Equal(expectedRs1, ir.Rs1);
            Assert.Equal(expectedRs2, ir.Rs2);
            Assert.True(ir.HasAbsoluteAddressing);
            Assert.Equal(0x4100L, ir.Imm);
        }

        [Theory]
        [InlineData(Processor.CPU_Core.InstructionsEnum.JumpIfEqual)]
        [InlineData(Processor.CPU_Core.InstructionsEnum.JumpIfNotEqual)]
        [InlineData(Processor.CPU_Core.InstructionsEnum.JumpIfBelow)]
        [InlineData(Processor.CPU_Core.InstructionsEnum.JumpIfBelowOrEqual)]
        [InlineData(Processor.CPU_Core.InstructionsEnum.JumpIfAbove)]
        [InlineData(Processor.CPU_Core.InstructionsEnum.JumpIfAboveOrEqual)]
        public void CanonicalDecoder_CompilerEmittedLegacyConditionalWrapper_FailsClosedAsCompatOnlyContour(
            Processor.CPU_Core.InstructionsEnum rawOpcode)
        {
            var decoder = new VliwDecoderV4();
            var inst = new VLIW_Instruction
            {
                OpCode = (uint)rawOpcode,
                DataTypeValue = DataTypeEnum.INT32,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(2, 3, 4),
                Src2Pointer = 0x4100,
                StreamLength = 0,
                Stride = 0
            };

            InvalidOpcodeException exception = Assert.Throws<InvalidOpcodeException>(
                () => decoder.Decode(in inst, slotIndex: 0));

            Assert.False(exception.IsProhibited);
            Assert.Contains("compat contour", exception.Message);
            Assert.Contains("compat", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Data Type Tests

        [Theory]
        [InlineData(DataTypeEnum.INT8)]
        [InlineData(DataTypeEnum.INT16)]
        [InlineData(DataTypeEnum.INT32)]
        [InlineData(DataTypeEnum.INT64)]
        [InlineData(DataTypeEnum.FLOAT32)]
        [InlineData(DataTypeEnum.FLOAT64)]
        public void Instruction_AllDataTypes_ShouldBeSupported(DataTypeEnum dataType)
        {
            // Act
            var inst = InstructionEncoder.EncodeVector1D(
                100, dataType, 0x1000, 0x2000, 32);

            // Assert
            Assert.Equal((byte)dataType, inst.DataType);
        }

        #endregion
    }
}


