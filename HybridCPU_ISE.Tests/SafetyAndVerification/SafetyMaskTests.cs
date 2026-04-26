using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using System.Collections.Generic;
using System.Reflection;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Unit tests for Safety Mask generation and verification (Phase 6: Safety Tags).
    /// Tests verify that safety masks correctly encode resource conflicts and enable
    /// fast parallel verification of injection safety.
    /// </summary>
    public class SafetyMaskTests
    {
        [Fact]
        public void MicroOpDescriptor_HasNo_LegacyGenerateSafetyMaskOverride_Property()
        {
            PropertyInfo? property = typeof(MicroOpDescriptor).GetProperty("GenerateSafetyMask");
            Assert.Null(property);
        }

        [Fact]
        public void InstructionRegistry_HasNo_LegacyDefaultSafetyMaskGenerator_Method()
        {
            MethodInfo? method = typeof(InstructionRegistry).GetMethod("DefaultSafetyMaskGenerator");
            Assert.Null(method);
        }

        #region Helper Methods

        /// <summary>
        /// Helper method to create a scalar ALU micro-operation with admission metadata.
        /// </summary>
        private MicroOp CreateScalarALUMicroOpWithMask(int threadId, ushort destReg, ushort src1Reg, ushort src2Reg, int memoryDomainId = 0)
        {
            var aluOp = new ScalarALUMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Addition,
                OwnerThreadId = threadId,
                VirtualThreadId = threadId,
                DestRegID = destReg,
                Src1RegID = src1Reg,
                Src2RegID = src2Reg,
                WritesRegister = true
            };
            aluOp.InitializeMetadata();

            return aluOp;
        }

        /// <summary>
        /// Helper method to create a load micro-operation with admission metadata.
        /// </summary>
        private MicroOp CreateLoadMicroOpWithMask(int threadId, ushort destReg, ulong address, int memoryDomainId = 0, ushort baseReg = 10)
        {
            var loadOp = new LoadMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Load,
                OwnerThreadId = threadId,
                VirtualThreadId = threadId,
                DestRegID = destReg,
                Address = address,
                Size = 4,
                BaseRegID = baseReg,  // Use different base register to avoid conflicts
                Placement = SlotPlacementMetadata.Default with { DomainTag = (ulong)memoryDomainId },
                WritesRegister = true
            };
            loadOp.InitializeMetadata();
            loadOp.SafetyMask = ResourceMaskBuilder.ForRegisterRead128(baseReg)
                              | ResourceMaskBuilder.ForRegisterWrite128(destReg)
                              | ResourceMaskBuilder.ForLoad128()
                              | ResourceMaskBuilder.ForMemoryDomain128(memoryDomainId);
            loadOp.RefreshAdmissionMetadata();

            return loadOp;
        }

        /// <summary>
        /// Helper method to create a store micro-operation with admission metadata.
        /// </summary>
        private MicroOp CreateStoreMicroOpWithMask(int threadId, ushort srcReg, ulong address, int memoryDomainId = 0, ushort baseReg = 20)
        {
            var storeOp = new StoreMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Store,
                OwnerThreadId = threadId,
                VirtualThreadId = threadId,
                SrcRegID = srcReg,
                BaseRegID = baseReg,  // Use different base register to avoid conflicts
                Address = address,
                Value = 0,
                Size = 4,
                Placement = SlotPlacementMetadata.Default with { DomainTag = (ulong)memoryDomainId }
            };
            storeOp.InitializeMetadata();
            storeOp.SafetyMask = ResourceMaskBuilder.ForRegisterRead128(srcReg)
                               | ResourceMaskBuilder.ForRegisterRead128(baseReg)
                               | ResourceMaskBuilder.ForStore128()
                               | ResourceMaskBuilder.ForMemoryDomain128(memoryDomainId);
            storeOp.RefreshAdmissionMetadata();

            return storeOp;
        }

        /// <summary>
        /// Helper method to create an empty bundle with NOPs
        /// </summary>
        private MicroOp[] CreateEmptyBundle(int threadId)
        {
            var bundle = new MicroOp[8];
            for (int i = 0; i < 8; i++)
            {
                bundle[i] = new NopMicroOp
                {
                    OpCode = 0,
                    OwnerThreadId = threadId,
                    SafetyMask = 0  // NOP has no conflicts
                };
            }
            return bundle;
        }

        #endregion

        #region Safety Mask Generation Tests

        [Fact]
        public void DefaultSafetyMask128Generator_RegisterReads_ShouldEncodeBits0to15()
        {
            // Arrange: Create context with register reads
            var context = new SafetyMaskContext
            {
                ReadRegisters = new List<int> { 0, 4, 8, 12 },
                WriteRegisters = new List<int>(),
                MemoryDomainId = 0,
                IsMemoryOp = false
            };

            // Act: Generate mask
            SafetyMask128 mask = InstructionRegistry.DefaultSafetyMask128Generator(context);

            // Assert: Bits 0-15 should have register read groups encoded
            // Register 0 -> group 0 -> bit 0
            // Register 4 -> group 1 -> bit 1
            // Register 8 -> group 2 -> bit 2
            // Register 12 -> group 3 -> bit 3
            ulong expectedMask = (1UL << 0) | (1UL << 1) | (1UL << 2) | (1UL << 3);
            Assert.Equal(expectedMask, mask.Low);
            Assert.Equal(0UL, mask.High);
        }

        [Fact]
        public void DefaultSafetyMask128Generator_RegisterWrites_ShouldEncodeBits16to31()
        {
            // Arrange: Create context with register writes
            var context = new SafetyMaskContext
            {
                ReadRegisters = new List<int>(),
                WriteRegisters = new List<int> { 0, 4, 8 },
                MemoryDomainId = 0,
                IsMemoryOp = false
            };

            // Act: Generate mask
            SafetyMask128 mask = InstructionRegistry.DefaultSafetyMask128Generator(context);

            // Assert: Bits 16-31 should have register write groups encoded
            // Register 0 -> group 0 -> bit 16
            // Register 4 -> group 1 -> bit 17
            // Register 8 -> group 2 -> bit 18
            ulong expectedMask = (1UL << 16) | (1UL << 17) | (1UL << 18);
            Assert.Equal(expectedMask, mask.Low);
            Assert.Equal(0UL, mask.High);
        }

        [Fact]
        public void DefaultSafetyMask128Generator_MemoryDomain_ShouldEncodeBits32to47()
        {
            // Arrange: Create context with memory domain
            var context = new SafetyMaskContext
            {
                ReadRegisters = new List<int>(),
                WriteRegisters = new List<int>(),
                MemoryDomainId = 5,
                IsMemoryOp = true,
                IsLoad = true
            };

            // Act: Generate mask
            SafetyMask128 mask = InstructionRegistry.DefaultSafetyMask128Generator(context);

            // Assert: Bit 37 (32 + 5) should be set for domain 5, and bit 48 for Load
            ulong expectedMask = (1UL << 37) | (1UL << 48);
            Assert.Equal(expectedMask, mask.Low);
            Assert.Equal(0UL, mask.High);
        }

        [Fact]
        public void DefaultSafetyMask128Generator_LSU_Load_ShouldSetBit48()
        {
            // Arrange: Create context for load operation
            var context = new SafetyMaskContext
            {
                ReadRegisters = new List<int>(),
                WriteRegisters = new List<int>(),
                MemoryDomainId = 0,
                IsMemoryOp = true,
                IsLoad = true,
                IsStore = false,
                IsAtomic = false
            };

            // Act: Generate mask
            SafetyMask128 mask = InstructionRegistry.DefaultSafetyMask128Generator(context);

            // Assert: Bit 48 should be set for Load
            Assert.True((mask.Low & (1UL << 48)) != 0);
            Assert.Equal(0UL, mask.High);
        }

        [Fact]
        public void DefaultSafetyMask128Generator_LSU_Store_ShouldSetBit49()
        {
            // Arrange: Create context for store operation
            var context = new SafetyMaskContext
            {
                ReadRegisters = new List<int>(),
                WriteRegisters = new List<int>(),
                MemoryDomainId = 0,
                IsMemoryOp = true,
                IsLoad = false,
                IsStore = true,
                IsAtomic = false
            };

            // Act: Generate mask
            SafetyMask128 mask = InstructionRegistry.DefaultSafetyMask128Generator(context);

            // Assert: Bit 49 should be set for Store
            Assert.True((mask.Low & (1UL << 49)) != 0);
            Assert.Equal(0UL, mask.High);
        }

        [Fact]
        public void ComputeSafetyMask_ScalarALU_ShouldGenerateCorrectMask()
        {
            // Arrange: Create ScalarALUMicroOp
            var aluOp = new ScalarALUMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Addition,
                DestRegID = 1,
                Src1RegID = 2,
                Src2RegID = 3,
                WritesRegister = true
            };
            aluOp.InitializeMetadata();

            // Act: Compute safety mask
            SafetyMask128 mask = InstructionRegistry.ComputeSafetyMask(aluOp.OpCode, aluOp, 0);

            // Assert: Should have read bits for registers 2,3 and write bit for register 1
            // Reg 2 -> group 0 -> bit 0, Reg 3 -> group 0 -> bit 0
            // Reg 1 -> group 0 -> bit 16
            ulong expectedRead = 1UL << 0;  // Both src1 and src2 in group 0
            ulong expectedWrite = 1UL << 16;  // Dest in group 0
            ulong expectedMask = expectedRead | expectedWrite;
            Assert.Equal(expectedMask, mask.Low);
        }

        #endregion

        #region Fast Verification Tests

        [Fact]
        public void VerifyInjectionFast_NoConflicts_ShouldReturnTrue()
        {
            // Arrange: Create bundle with operation using registers 0-3
            var bundle = CreateEmptyBundle(0);
            bundle[0] = CreateScalarALUMicroOpWithMask(0, 1, 2, 3);  // R1 = R2 + R3

            // Create candidate using different registers (8-11)
            var candidate = CreateScalarALUMicroOpWithMask(1, 9, 10, 11);  // R9 = R10 + R11

            var verifier = new SafetyVerifier();

            // Act: Verify injection using fast path
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: Should be safe (no register conflicts)
            Assert.True(result);
        }

        [Fact]
        public void VerifyInjectionFast_RegisterConflict_ShouldReturnFalse()
        {
            // Arrange: Create bundle with operation writing to R1
            var bundle = CreateEmptyBundle(0);
            bundle[0] = CreateScalarALUMicroOpWithMask(0, 1, 2, 3);  // R1 = R2 + R3

            // Create candidate reading from R1
            var candidate = CreateScalarALUMicroOpWithMask(1, 5, 1, 2);  // R5 = R1 + R2

            var verifier = new SafetyVerifier();

            // Act: Verify injection using fast path
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: Should be unsafe (RAW hazard on R1)
            Assert.False(result);
        }

        [Fact]
        public void VerifyInjectionFast_MemoryDomainConflict_ShouldReturnFalse()
        {
            // Arrange: Create bundle with load from domain 0
            var bundle = CreateEmptyBundle(0);
            bundle[0] = CreateLoadMicroOpWithMask(0, 1, 0x1000, memoryDomainId: 0);

            // Create candidate store to domain 0
            var candidate = CreateStoreMicroOpWithMask(1, 2, 0x2000, memoryDomainId: 0);

            var verifier = new SafetyVerifier();

            // Act: Verify injection using fast path
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: Should be unsafe (same memory domain)
            Assert.False(result);
        }

        [Fact]
        public void VerifyInjectionFast_DifferentMemoryDomains_ShouldReturnTrue()
        {
            // Arrange: Create bundle with load from domain 0
            var bundle = CreateEmptyBundle(0);
            bundle[0] = CreateLoadMicroOpWithMask(0, 1, 0x1000, memoryDomainId: 0);

            // Create candidate store to domain 1
            var candidate = CreateStoreMicroOpWithMask(1, 12, 0x2000, memoryDomainId: 1);

            var verifier = new SafetyVerifier();

            // Act: Verify injection using fast path
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: Should be safe (different memory domains)
            Assert.True(result);
        }

        [Fact]
        public void VerifyInjection_WithSafetyMask_ShouldUseFastPath()
        {
            // Arrange: Create bundle and candidate with safety masks
            var bundle = CreateEmptyBundle(0);
            bundle[0] = CreateScalarALUMicroOpWithMask(0, 1, 2, 3);

            var candidate = CreateScalarALUMicroOpWithMask(1, 9, 10, 11);

            var verifier = new SafetyVerifier();

            // Act: Verify injection (should use fast path)
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjection(verifier, bundle, 1, candidate, 0, 1);

            // Assert: Should be safe
            Assert.True(result);
        }

        [Fact]
        public void VerifyInjection_WithoutSafetyMask_ShouldUseLegacyPath()
        {
            // Arrange: Create bundle and candidate WITHOUT safety masks
            var bundle = CreateEmptyBundle(0);
            var aluOp = new ScalarALUMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Addition,
                OwnerThreadId = 0,
                DestRegID = 1,
                Src1RegID = 2,
                Src2RegID = 3,
                WritesRegister = true,
                SafetyMask = 0  // No mask
            };
            aluOp.InitializeMetadata();
            bundle[0] = aluOp;

            var candidate = new ScalarALUMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Addition,
                OwnerThreadId = 1,
                DestRegID = 9,
                Src1RegID = 10,
                Src2RegID = 11,
                WritesRegister = true,
                SafetyMask = 0  // No mask
            };
            candidate.InitializeMetadata();

            var verifier = new SafetyVerifier();

            // Act: Verify injection (should use legacy path)
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjection(verifier, bundle, 1, candidate, 0, 1);

            // Assert: Should be safe (different registers)
            Assert.True(result);
        }

        #endregion

        #region Bundle Mask Tests

        [Fact]
        public void VerifyInjectionFast_MultipleOpsInBundle_ShouldORAllMasks()
        {
            // Arrange: Create bundle with multiple operations
            // Note: Register grouping is 4 registers per group
            // Group 0: R0-3, Group 1: R4-7, Group 2: R8-11, Group 3: R12-15
            var bundle = CreateEmptyBundle(0);
            bundle[0] = CreateScalarALUMicroOpWithMask(0, 1, 2, 3);     // Uses R1, R2, R3 (Group 0)
            bundle[1] = CreateScalarALUMicroOpWithMask(0, 5, 6, 7);     // Uses R5, R6, R7 (Group 1)
            bundle[2] = CreateScalarALUMicroOpWithMask(0, 9, 10, 11);   // Uses R9, R10, R11 (Group 2)

            // Create candidate using R12-14 (Group 3, no conflicts)
            var candidate = CreateScalarALUMicroOpWithMask(1, 13, 14, 15);

            var verifier = new SafetyVerifier();

            // Act: Verify injection
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: Should be safe (different register groups)
            Assert.True(result);
        }

        [Fact]
        public void VerifyInjectionFast_ConflictWithAnyBundleOp_ShouldReturnFalse()
        {
            // Arrange: Create bundle with multiple operations
            var bundle = CreateEmptyBundle(0);
            bundle[0] = CreateScalarALUMicroOpWithMask(0, 1, 2, 3);   // Uses R1, R2, R3
            bundle[1] = CreateScalarALUMicroOpWithMask(0, 4, 5, 6);   // Uses R4, R5, R6

            // Create candidate that conflicts with second operation (reads R5)
            var candidate = CreateScalarALUMicroOpWithMask(1, 10, 5, 11);

            var verifier = new SafetyVerifier();

            // Act: Verify injection
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: Should be unsafe (conflict with bundle[1])
            Assert.False(result);
        }

        #endregion

        #region Decoder Integration Tests (Phase 6 Refactoring)

        [Fact]
        public void DecoderContext_WithMemoryDomainId_ShouldBePassedToMicroOp()
        {
            // Arrange: Create decoder context with memory domain
            var context = new DecoderContext
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Addition,
                Reg1ID = 1,
                Reg2ID = 2,
                Reg3ID = 3,
                OwnerThreadId = 5,
                MemoryDomainId = 7
            };

            // Act: Create MicroOp
            var microOp = InstructionRegistry.CreateMicroOp(context.OpCode, context);
            microOp.OwnerThreadId = context.OwnerThreadId;

            // Compute safety mask (as decoder would)
            var safetyMask = InstructionRegistry.ComputeSafetyMask(context.OpCode, microOp, context.MemoryDomainId);

            // Assert: Safety mask should be non-zero and properly initialized
            Assert.NotEqual(0UL, safetyMask);
            Assert.Equal(context.OwnerThreadId, microOp.OwnerThreadId);
        }

        [Fact]
        public void ComputeSafetyMask_AfterInitializeMetadata_ShouldGenerateNonZeroMask()
        {
            // Arrange: Create ScalarALUMicroOp and initialize metadata
            var aluOp = new ScalarALUMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Addition,
                DestRegID = 1,
                Src1RegID = 2,
                Src2RegID = 3,
                WritesRegister = true
            };
            aluOp.InitializeMetadata();

            // Act: Compute safety mask
            SafetyMask128 mask = InstructionRegistry.ComputeSafetyMask(aluOp.OpCode, aluOp, 0);

            // Assert: Mask should be non-zero
            Assert.NotEqual(0UL, mask.Low);
        }

        [Fact]
        public void ComputeSafetyMask_ForLoadOperation_ShouldIncludeLSUBit()
        {
            // Arrange: Create LoadMicroOp
            var loadOp = new LoadMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Load,
                DestRegID = 1,
                Address = 0x1000,
                Size = 4,
                BaseRegID = 10,
                WritesRegister = true
            };
            loadOp.InitializeMetadata();

            // Act: Compute safety mask
            SafetyMask128 mask = InstructionRegistry.ComputeSafetyMask(loadOp.OpCode, loadOp, 0);

            // Assert: Should have LSU Load bit (bit 48) set
            Assert.True((mask.Low & (1UL << 48)) != 0, "Load operation should have LSU Load bit set");
        }

        [Fact]
        public void ComputeSafetyMask_ForStoreOperation_ShouldIncludeLSUBit()
        {
            // Arrange: Create StoreMicroOp
            var storeOp = new StoreMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Store,
                SrcRegID = 1,
                BaseRegID = 20,
                Address = 0x2000,
                Value = 0,
                Size = 4
            };
            storeOp.InitializeMetadata();

            // Act: Compute safety mask
            SafetyMask128 mask = InstructionRegistry.ComputeSafetyMask(storeOp.OpCode, storeOp, 0);

            // Assert: Should have LSU Store bit (bit 49) set
            Assert.True((mask.Low & (1UL << 49)) != 0, "Store operation should have LSU Store bit set");
        }

        [Fact]
        public void VerifyInjectionFast_WithZeroSafetyMask_ShouldReturnFalse()
        {
            // Arrange: candidate exposes no admission-side structural or register legality facts.
            var bundle = CreateEmptyBundle(0);
            var candidate = new NopMicroOp
            {
                OwnerThreadId = 1,
                SafetyMask = SafetyMask128.Zero
            };

            // Act
            var verifier = new SafetyVerifier();
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region Collision Detection Tests (Phase 6 Refactoring)

        [Fact]
        public void VerifyInjectionFast_SameRegisterGroup_ShouldDetectCollision()
        {
            // Arrange: Register grouping is 4 registers per group
            // Group 0: R0-3, Group 1: R4-7, etc.
            var bundle = CreateEmptyBundle(0);
            // Bundle writes to R1 (Group 0)
            bundle[0] = CreateScalarALUMicroOpWithMask(0, destReg: 1, src1Reg: 10, src2Reg: 11);

            // Candidate writes to R2 (also Group 0)
            var candidate = CreateScalarALUMicroOpWithMask(1, destReg: 2, src1Reg: 12, src2Reg: 13);

            var verifier = new SafetyVerifier();

            // Act: Verify injection
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: Should detect collision (both in Group 0)
            Assert.False(result);
        }

        [Fact]
        public void VerifyInjectionFast_DifferentRegisterGroups_ShouldAllowInjection()
        {
            // Arrange: Create operations using different register groups
            var bundle = CreateEmptyBundle(0);
            // Bundle writes to R1 (Group 0: R0-3)
            bundle[0] = CreateScalarALUMicroOpWithMask(0, destReg: 1, src1Reg: 2, src2Reg: 3);

            // Candidate writes to R8 (Group 2: R8-11)
            var candidate = CreateScalarALUMicroOpWithMask(1, destReg: 8, src1Reg: 9, src2Reg: 10);

            var verifier = new SafetyVerifier();

            // Act: Verify injection
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: Should allow (different groups)
            Assert.True(result);
        }

        [Fact]
        public void VerifyInjectionFast_LoadStoreToSameDomain_ShouldDetectCollision()
        {
            // Arrange: Create load and store to same memory domain
            var bundle = CreateEmptyBundle(0);
            bundle[0] = CreateLoadMicroOpWithMask(0, destReg: 1, address: 0x1000, memoryDomainId: 3);

            var candidate = CreateStoreMicroOpWithMask(1, srcReg: 2, address: 0x2000, memoryDomainId: 3);

            var verifier = new SafetyVerifier();

            // Act: Verify injection
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: Should detect collision (same domain, different LSU classes)
            Assert.False(result);
        }

        [Fact]
        public void VerifyInjectionFast_LoadLoadToDifferentDomains_ShouldDetectLSUConflict()
        {
            // Arrange: Create two loads to different memory domains
            // Note: Even though domains are different, both loads compete for LSU Load channel (bit 48)
            // This is a conservative safety check - hardware may support parallel loads,
            // but safety mask verification errs on the side of caution
            var bundle = CreateEmptyBundle(0);
            bundle[0] = CreateLoadMicroOpWithMask(0, destReg: 1, address: 0x1000, memoryDomainId: 0);

            var candidate = CreateLoadMicroOpWithMask(1, destReg: 5, address: 0x2000, memoryDomainId: 1);

            var verifier = new SafetyVerifier();

            // Act: Verify injection
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: Should detect LSU conflict (both use LSU Load channel)
            // Conservative approach: LSU class conflicts prevent injection even with different domains
            Assert.False(result);
        }

        [Fact]
        public void VerifyInjectionFast_MultipleOpsWithOverlappingMasks_ShouldDetectCollision()
        {
            // Arrange: Create bundle with multiple operations
            var bundle = CreateEmptyBundle(0);
            bundle[0] = CreateScalarALUMicroOpWithMask(0, destReg: 1, src1Reg: 2, src2Reg: 3);   // Group 0
            bundle[1] = CreateScalarALUMicroOpWithMask(0, destReg: 5, src1Reg: 6, src2Reg: 7);   // Group 1
            bundle[2] = CreateLoadMicroOpWithMask(0, destReg: 10, address: 0x1000, memoryDomainId: 0);

            // Candidate conflicts with bundle[0] (writes to R0, Group 0)
            var candidate = CreateScalarALUMicroOpWithMask(1, destReg: 0, src1Reg: 20, src2Reg: 21);

            var verifier = new SafetyVerifier();

            // Act: Verify injection
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: Should detect collision
            Assert.False(result);
        }

        [Fact]
        public void VerifyInjectionFast_NonConflictingComplexBundle_ShouldAllowInjection()
        {
            // Arrange: Create complex bundle with no conflicts for candidate
            var bundle = CreateEmptyBundle(0);
            bundle[0] = CreateScalarALUMicroOpWithMask(0, destReg: 1, src1Reg: 2, src2Reg: 3);   // Group 0
            bundle[1] = CreateScalarALUMicroOpWithMask(0, destReg: 5, src1Reg: 6, src2Reg: 7);   // Group 1
            bundle[2] = CreateLoadMicroOpWithMask(0, destReg: 10, address: 0x1000, memoryDomainId: 0, baseReg: 11);

            // Candidate uses Group 3 (R12-15) and different memory domain
            var candidate = CreateScalarALUMicroOpWithMask(1, destReg: 13, src1Reg: 14, src2Reg: 15);

            var verifier = new SafetyVerifier();

            // Act: Verify injection
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: Should allow (no conflicts)
            Assert.True(result);
        }

        #endregion
    }
}
