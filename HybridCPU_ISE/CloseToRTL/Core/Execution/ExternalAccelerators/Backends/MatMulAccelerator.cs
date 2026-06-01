using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core.Accelerators
{
    /// <summary>
    /// Retained matrix-multiply custom-accelerator descriptor fixture - Phase 4.
    ///
    /// This class is kept for legacy registration and descriptor-shape tests only.
    /// It is not an active DMA/stream execution path, not a canonical decode
    /// publication source, and not a security authority. Runtime publication of
    /// registered custom-accelerator opcodes remains fail-closed until a truthful
    /// operand ABI, placement, DMA, replay, and retire contract exists.
    ///
    /// Fixture notes:
    /// - reports a legacy opcode family and resource footprint for tests;
    /// - models latency from matrix dimensions for quarantined Phase 4 coverage;
    /// - does not make MatMul an active architecture-facing accelerator.
    /// </summary>
    public class MatMulAccelerator : ICustomAccelerator
    {
        public string Name => "MatMul";

        public IReadOnlyList<uint> SupportedOpcodes => new uint[]
        {
            0xC000,  // MATMUL_F32
            0xC001,  // MATMUL_F64
        };

        /// <summary>
        /// Execute the descriptor fixture's local model.
        /// This helper is test-only behavior; runtime opcode publication still fails closed.
        /// </summary>
        /// <param name="opcode">Operation code (F32 or F64)</param>
        /// <param name="operands">
        /// operands[0] = matA_addr (base address of matrix A)
        /// operands[1] = matB_addr (base address of matrix B)
        /// operands[2] = matC_addr (base address of result matrix C)
        /// operands[3] = M (number of rows in A)
        /// operands[4] = N (number of columns in B)
        /// operands[5] = K (common dimension: columns of A, rows of B)
        /// </param>
        /// <param name="config">Accelerator configuration (unused)</param>
        /// <returns>Result: [matC_addr]</returns>
        public ulong[] Execute(uint opcode, ulong[] operands, byte[] config)
        {
            if (operands.Length < 6)
                throw new ArgumentException("MatMul requires 6 operands");

            ulong matA_addr = operands[0];
            ulong matB_addr = operands[1];
            ulong matC_addr = operands[2];
            int M = (int)operands[3];
            int N = (int)operands[4];
            int K = (int)operands[5];

            // Descriptor-fixture model only: validate shape and return the destination address.

            // Validate dimensions
            if (M <= 0 || N <= 0 || K <= 0)
                throw new ArgumentException("Invalid matrix dimensions");

            // A future active implementation would require an explicit descriptor ABI,
            // owner/domain guard, placement, DMA, replay, and retire contract.

            // For simulation, return the result address
            return new ulong[] { matC_addr };
        }

        /// <summary>
        /// Get execution latency based on matrix dimensions
        /// </summary>
        public int GetLatency(uint opcode, ulong[] operands)
        {
            if (operands.Length < 6)
                return 100; // Default latency

            int M = (int)operands[3];
            int N = (int)operands[4];
            int K = (int)operands[5];

            // Data-dependent latency model:
            // - Each output element requires K multiply-accumulates
            // - Assume 4-way SIMD parallelism
            // - Add setup overhead

            int setupCycles = 10;
            int computeCycles = (M * N * K) / 4; // 4-way SIMD
            int writebackCycles = (M * N) / 8;   // 8 elements per burst

            return setupCycles + computeCycles + writebackCycles;
        }

        /// <summary>
        /// Get resource footprint for scheduling
        /// </summary>
        public AcceleratorResourceFootprint GetResourceFootprint(uint opcode)
        {
            return new AcceleratorResourceFootprint
            {
                MemoryBandwidthMBps = 10240,  // 10 GB/s peak bandwidth
                RegisterFileAccesses = 0,      // Operates on memory, not registers
                RequiresExclusiveAccess = true // Large DMA operations require exclusive access
            };
        }

        /// <summary>
        /// Matrix multiply is pipelined
        /// </summary>
        public bool IsPipelined(uint opcode) => true;

        /// <summary>
        /// Reset fixture state.
        /// </summary>
        public void Reset()
        {
            // Clear any internal state (pipeline registers, accumulators, etc.)
            // For this simple implementation, nothing to reset
        }
    }
}
