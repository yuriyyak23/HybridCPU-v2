using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using System;
using System.Collections.Generic;

namespace HybridCPU_ISE.Tests.TestHelpers
{
    /// <summary>
    /// TEST-ONLY helper for creating synthetic micro-ops with proper masks.
    /// Enables FSP testing without requiring full multi-threaded execution.
    ///
    /// Purpose: Simplify test creation by providing factory methods for common micro-op patterns.
    /// All methods create micro-ops with valid resource and safety masks.
    ///
    /// Created: 2026-03-02
    /// For: Performance test infrastructure (testPerfPlan.md Iteration 2)
    /// </summary>
    public static class MicroOpTestHelper
    {
        #region Scalar ALU Micro-Ops

        /// <summary>
        /// Create a scalar ALU micro-op with valid resource/safety masks.
        /// </summary>
        /// <param name="virtualThreadId">Virtual thread ID (0-3)</param>
        /// <param name="destReg">Destination register ID</param>
        /// <param name="src1Reg">Source register 1 ID</param>
        /// <param name="src2Reg">Source register 2 ID</param>
        /// <param name="opCode">Operation code (default: Addition)</param>
        /// <returns>Configured ScalarALUMicroOp</returns>
        public static ScalarALUMicroOp CreateScalarALU(
            int virtualThreadId,
            ushort destReg,
            ushort src1Reg,
            ushort src2Reg,
            uint opCode = 0)
        {
            var op = new ScalarALUMicroOp
            {
                VirtualThreadId = virtualThreadId,
                OwnerThreadId   = virtualThreadId,
                DestRegID = destReg,
                Src1RegID = src1Reg,
                Src2RegID = src2Reg,
                OpCode = opCode != 0 ? opCode : (uint)Processor.CPU_Core.InstructionsEnum.Addition,
                WritesRegister = true
            };

            // Build resource mask using static methods
            var maskRead1 = ResourceMaskBuilder.ForRegisterRead(src1Reg);
            var maskRead2 = ResourceMaskBuilder.ForRegisterRead(src2Reg);
            var maskWrite = ResourceMaskBuilder.ForRegisterWrite(destReg);
            op.ResourceMask = maskRead1 | maskRead2 | maskWrite;

            // Build safety mask - combine read and write masks
            var safetyRead1 = ResourceMaskBuilder.ForRegisterRead128(src1Reg);
            var safetyRead2 = ResourceMaskBuilder.ForRegisterRead128(src2Reg);
            var safetyWrite = ResourceMaskBuilder.ForRegisterWrite128(destReg);
            op.SafetyMask = safetyRead1 | safetyRead2 | safetyWrite;

            op.InitializeMetadata();
            return op;
        }

        /// <summary>
        /// Create a scalar ALU micro-op with immediate operand.
        /// </summary>
        public static ScalarALUMicroOp CreateScalarALUImmediate(
            int virtualThreadId,
            ushort destReg,
            ushort srcReg,
            ulong immediate,
            uint opCode = 0)
        {
            var op = new ScalarALUMicroOp
            {
                VirtualThreadId = virtualThreadId,
                OwnerThreadId   = virtualThreadId,
                DestRegID = destReg,
                Src1RegID = srcReg,
                OpCode = opCode != 0 ? opCode : (uint)Processor.CPU_Core.InstructionsEnum.Addition,
                WritesRegister = true,
                UsesImmediate = true
            };

            // Note: Immediate value stored in Src2RegID for simple ALU ops
            op.Src2RegID = (ushort)(immediate & 0xFFFF);

            var maskRead = ResourceMaskBuilder.ForRegisterRead(srcReg);
            var maskWrite = ResourceMaskBuilder.ForRegisterWrite(destReg);
            op.ResourceMask = maskRead | maskWrite;

            var safetyRead = ResourceMaskBuilder.ForRegisterRead128(srcReg);
            var safetyWrite = ResourceMaskBuilder.ForRegisterWrite128(destReg);
            op.SafetyMask = safetyRead | safetyWrite;

            op.InitializeMetadata();
            return op;
        }

        #endregion

        #region Memory Micro-Ops

        /// <summary>
        /// Create a load micro-op.
        /// </summary>
        /// <param name="virtualThreadId">Virtual thread ID (0-3)</param>
        /// <param name="destReg">Destination register ID</param>
        /// <param name="address">Memory address to load from</param>
        /// <param name="domainTag">Memory domain tag (default: 0)</param>
        /// <returns>Configured LoadMicroOp</returns>
        public static LoadMicroOp CreateLoad(
            int virtualThreadId,
            ushort destReg,
            ulong address,
            byte domainTag = 0)
        {
            var op = new LoadMicroOp
            {
                VirtualThreadId = virtualThreadId,
                DestRegID = destReg,
                Address = address,
                WritesRegister = true
            };
            op.Placement = op.Placement with { DomainTag = domainTag };

            var maskWrite = ResourceMaskBuilder.ForRegisterWrite(destReg);
            var maskLoad = ResourceMaskBuilder.ForLoad();
            var maskDomain = ResourceMaskBuilder.ForMemoryDomain(domainTag);
            op.ResourceMask = maskWrite | maskLoad | maskDomain;

            // Build safety mask for memory access
            var safetyWrite = ResourceMaskBuilder.ForRegisterWrite128(destReg);
            var safetyLoad = ResourceMaskBuilder.ForLoad128();
            var safetyDomain = ResourceMaskBuilder.ForMemoryDomain128(domainTag);
            op.SafetyMask = safetyWrite | safetyLoad | safetyDomain;

            op.InitializeMetadata();
            return op;
        }

        /// <summary>
        /// Create a store micro-op.
        /// </summary>
        /// <param name="virtualThreadId">Virtual thread ID (0-3)</param>
        /// <param name="srcReg">Source register ID</param>
        /// <param name="address">Memory address to store to</param>
        /// <param name="size">Store width in bytes (default: 8)</param>
        /// <param name="domainTag">Memory domain tag (default: 0)</param>
        /// <returns>Configured StoreMicroOp</returns>
        public static StoreMicroOp CreateStore(
            int virtualThreadId,
            ushort srcReg,
            ulong address,
            byte size = sizeof(ulong),
            byte domainTag = 0)
        {
            var op = new StoreMicroOp
            {
                VirtualThreadId = virtualThreadId,
                SrcRegID = srcReg,
                Address = address,
                Size = size
            };
            op.Placement = op.Placement with { DomainTag = domainTag };

            var maskRead = ResourceMaskBuilder.ForRegisterRead(srcReg);
            var maskStore = ResourceMaskBuilder.ForStore();
            var maskDomain = ResourceMaskBuilder.ForMemoryDomain(domainTag);
            op.ResourceMask = maskRead | maskStore | maskDomain;

            // Build safety mask for memory access
            var safetyRead = ResourceMaskBuilder.ForRegisterRead128(srcReg);
            var safetyStore = ResourceMaskBuilder.ForStore128();
            var safetyDomain = ResourceMaskBuilder.ForMemoryDomain128(domainTag);
            op.SafetyMask = safetyRead | safetyStore | safetyDomain;

            op.InitializeMetadata();
            return op;
        }

        /// <summary>
        /// Create a LoadStoreMicroOp (either Load or Store with explicit bank ID).
        /// Useful for testing bank-level scoreboard tracking in pipelined FSP.
        /// </summary>
        /// <param name="virtualThreadId">Virtual thread ID (0-3)</param>
        /// <param name="address">Memory address</param>
        /// <param name="destReg">Destination register ID (for loads)</param>
        /// <param name="isLoad">True for load, false for store</param>
        /// <param name="memoryBankId">Explicit memory bank ID</param>
        /// <param name="domainTag">Memory domain tag (default: 0)</param>
        /// <returns>Configured LoadStoreMicroOp</returns>
        public static LoadStoreMicroOp CreateLoadStore(
            int virtualThreadId,
            ulong address,
            ushort destReg,
            bool isLoad,
            int memoryBankId,
            byte domainTag = 0)
        {
            if (Processor.Memory is not { NumBanks: > 0, BankWidthBytes: > 0 } runtimeMemory)
            {
                throw new InvalidOperationException(
                    "MicroOpTestHelper.CreateLoadStore(...) requires an initialized Processor.Memory " +
                    "geometry so explicit memoryBankId arguments cannot silently rely on legacy defaults.");
            }

            int runtimeNumBanks = runtimeMemory.NumBanks;
            int runtimeBankWidthBytes = runtimeMemory.BankWidthBytes;
            int normalizedBankId = ((memoryBankId % runtimeNumBanks) + runtimeNumBanks) % runtimeNumBanks;

            // Auto-align the address so it computes to the requested memoryBankId
            // under the same runtime bank geometry the scheduler now consumes.
            ulong alignedAddress =
                ((ulong)normalizedBankId * (ulong)runtimeBankWidthBytes) +
                (address % (ulong)runtimeBankWidthBytes);
            if (isLoad)
            {
                var loadOp = CreateLoad(virtualThreadId, destReg, alignedAddress, domainTag);
                return loadOp;
            }
            else
            {
                var storeOp = CreateStore(virtualThreadId, destReg, alignedAddress, domainTag: domainTag);
                return storeOp;
            }
        }

        #endregion

        #region Utility Micro-Ops

        /// <summary>
        /// Create a NOP micro-op (useful for filling slots).
        /// </summary>
        /// <param name="virtualThreadId">Virtual thread ID (0-3)</param>
        /// <returns>Configured NopMicroOp</returns>
        public static NopMicroOp CreateNop(int virtualThreadId = 0)
        {
            return new NopMicroOp
            {
                VirtualThreadId = virtualThreadId,
                OpCode = 0,
                ResourceMask = new ResourceBitset(0, 0),
                SafetyMask = SafetyMask128.Zero
            };
        }

        #endregion

        #region Test Scenario Helpers

        /// <summary>
        /// Create an orthogonal set of micro-ops that don't conflict.
        /// Useful for Test C (Orthogonal Resource Mix).
        /// </summary>
        /// <param name="count">Number of ops to create (1-4)</param>
        /// <returns>List of non-conflicting micro-ops</returns>
        public static List<MicroOp> CreateOrthogonalSet(int count)
        {
            if (count < 1 || count > 4)
                throw new ArgumentOutOfRangeException(nameof(count), "Count must be 1-4");

            var ops = new List<MicroOp>();

            // VT0: Scalar ALU using R0-R7 (register group 0)
            if (count >= 1)
                ops.Add(CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));

            // VT1: Scalar ALU using R8-R15 (register group 1, no conflict with VT0)
            if (count >= 2)
                ops.Add(CreateScalarALU(1, destReg: 9, src1Reg: 10, src2Reg: 11));

            // VT2: Load from memory bank 0
            if (count >= 3)
                ops.Add(CreateLoad(2, destReg: 17, address: 0x100000, domainTag: 0));

            // VT3: Load from memory bank 1 (different bank, no conflict)
            if (count >= 4)
                ops.Add(CreateLoad(3, destReg: 18, address: 0x110000, domainTag: 1));

            return ops;
        }

        /// <summary>
        /// Create a conflicting set of micro-ops (for testing rejection).
        /// Both ops write to the same register - should cause rejection.
        /// </summary>
        /// <returns>List of conflicting micro-ops</returns>
        public static List<MicroOp> CreateConflictingSet()
        {
            var ops = new List<MicroOp>
            {
                // Both ops write to R1 - WAW conflict
                CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3),
                CreateScalarALU(1, destReg: 1, src1Reg: 4, src2Reg: 5)
            };

            return ops;
        }

        /// <summary>
        /// Create a set with RAW dependency (for testing hazard detection).
        /// Second op reads what first op writes.
        /// </summary>
        /// <returns>List of RAW-dependent micro-ops</returns>
        public static List<MicroOp> CreateRAWDependentSet()
        {
            var ops = new List<MicroOp>
            {
                // First op writes R10
                CreateScalarALU(0, destReg: 10, src1Reg: 2, src2Reg: 3),
                // Second op reads R10 - RAW hazard
                CreateScalarALU(1, destReg: 11, src1Reg: 10, src2Reg: 5)
            };

            return ops;
        }

        /// <summary>
        /// Create a set with memory domain conflicts.
        /// Both ops access the same memory domain.
        /// </summary>
        /// <returns>List of memory-conflicting micro-ops</returns>
        public static List<MicroOp> CreateMemoryConflictSet()
        {
            var ops = new List<MicroOp>
            {
                // Both access domain 0, different addresses but same bank
                CreateLoad(0, destReg: 10, address: 0x100000, domainTag: 0),
                CreateStore(1, srcReg: 11, address: 0x100100, domainTag: 0)
            };

            return ops;
        }

        /// <summary>
        /// Create a diverse set for instruction coverage testing (Test E).
        /// </summary>
        /// <returns>List of diverse micro-ops</returns>
        public static List<MicroOp> CreateDiverseSet()
        {
            var ops = new List<MicroOp>
            {
                CreateScalarALU(0, 1, 2, 3),                           // ALU
                CreateScalarALUImmediate(0, 4, 5, 100),               // ALU with immediate
                CreateLoad(1, 6, 0x100000, 0),                        // Load
                CreateStore(1, 7, 0x100100, domainTag: 0),            // Store
                CreateNop(2)                                          // NOP
            };

            return ops;
        }

        #endregion
    }
}
