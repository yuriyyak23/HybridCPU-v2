using System;
using System.Diagnostics;

namespace HybridCPU_ISE.Core
{
    /// <summary>
    /// Custom exception for invariant violations (ref5.docx)
    /// </summary>
    public class InvariantViolationException : Exception
    {
        public InvariantViolationException(string message) : base(message)
        {
        }

        public InvariantViolationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Invariant checking utilities for verification
    /// Checks can be disabled via conditional compilation with TRACE_INVARIANTS symbol
    /// </summary>
    public static class InvariantChecks
    {
        /// <summary>
        /// Check that masked-off elements in result vector remain unmodified
        /// </summary>
        [Conditional("TRACE_INVARIANTS")]
        public static void CheckMaskedElementsPreserved(byte[] original, byte[] result,
            ulong predicateMask, int elementCount, int elementSize, bool maskAgnostic)
        {
            if (maskAgnostic)
                return; // Agnostic mode allows modifications

            if (predicateMask == 0xFFFFFFFFFFFFFFFFUL)
                return; // All lanes active, no masked elements

            if (original == null || result == null)
                return;

            for (int i = 0; i < elementCount && i < 64; i++)
            {
                bool laneActive = (predicateMask & (1UL << i)) != 0;
                if (!laneActive)
                {
                    // Check that this element wasn't modified
                    int offset = i * elementSize;
                    if (offset + elementSize <= original.Length && offset + elementSize <= result.Length)
                    {
                        for (int b = 0; b < elementSize; b++)
                        {
                            if (original[offset + b] != result[offset + b])
                            {
                                throw new InvariantViolationException(
                                    $"Invariant violation: Masked-off element {i} was modified. " +
                                    $"Original[{offset + b}]={original[offset + b]}, Result[{offset + b}]={result[offset + b]}");
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Check that burst transaction doesn't cross AXI page boundary
        /// </summary>
        [Conditional("TRACE_INVARIANTS")]
        public static void CheckBurstBoundary(ulong address, int size, int axiBoundary = 4096)
        {
            ulong startPage = address / (ulong)axiBoundary;
            ulong endPage = (address + (ulong)size - 1) / (ulong)axiBoundary;

            if (startPage != endPage)
            {
                throw new InvariantViolationException(
                    $"Invariant violation: Burst transaction crosses AXI boundary. " +
                    $"Address=0x{address:X}, Size={size}, Boundary={axiBoundary}. " +
                    $"StartPage={startPage}, EndPage={endPage}");
            }
        }

        /// <summary>
        /// Check that strip-mining iteration processes exactly VL elements
        /// </summary>
        [Conditional("TRACE_INVARIANTS")]
        public static void CheckStripMiningCount(int processedCount, uint vl, bool isFinalIteration)
        {
            if (isFinalIteration)
            {
                // Final iteration may process fewer elements
                if (processedCount > vl)
                {
                    throw new InvariantViolationException(
                        $"Invariant violation: Final strip-mining iteration processed {processedCount} elements, " +
                        $"but VL={vl}");
                }
            }
            else
            {
                // Non-final iterations must process exactly VL elements
                if (processedCount != vl)
                {
                    throw new InvariantViolationException(
                        $"Invariant violation: Strip-mining iteration processed {processedCount} elements, " +
                        $"expected VL={vl}");
                }
            }
        }

        /// <summary>
        /// Check that VL <= VLMAX constraint is satisfied
        /// </summary>
        [Conditional("TRACE_INVARIANTS")]
        public static void CheckVLConstraint(uint vl, uint vlmax)
        {
            if (vl > vlmax)
            {
                throw new InvariantViolationException(
                    $"Invariant violation: VL={vl} exceeds VLMAX={vlmax}");
            }
        }

        /// <summary>
        /// Check that VSEW is valid power-of-2 between 8 and 64
        /// </summary>
        [Conditional("TRACE_INVARIANTS")]
        public static void CheckVSEW(byte vsew)
        {
            if (vsew != 8 && vsew != 16 && vsew != 32 && vsew != 64)
            {
                throw new InvariantViolationException(
                    $"Invariant violation: VSEW={vsew} is not a valid value (must be 8, 16, 32, or 64)");
            }
        }

        /// <summary>
        /// Check that LMUL is valid (1, 2, 4, or 8)
        /// </summary>
        [Conditional("TRACE_INVARIANTS")]
        public static void CheckLMUL(byte lmul)
        {
            if (lmul != 1 && lmul != 2 && lmul != 4 && lmul != 8)
            {
                throw new InvariantViolationException(
                    $"Invariant violation: LMUL={lmul} is not a valid value (must be 1, 2, 4, or 8)");
            }
        }

        /// <summary>
        /// Check that array index is within bounds
        /// </summary>
        [Conditional("TRACE_INVARIANTS")]
        public static void CheckIndexBounds(int index, int arrayLength, string arrayName)
        {
            if (index < 0 || index >= arrayLength)
            {
                throw new InvariantViolationException(
                    $"Invariant violation: Index {index} is out of bounds for {arrayName} (length={arrayLength})");
            }
        }

        /// <summary>
        /// Check that memory address is aligned
        /// </summary>
        [Conditional("TRACE_INVARIANTS")]
        public static void CheckAlignment(ulong address, int alignment)
        {
            if ((address % (ulong)alignment) != 0)
            {
                throw new InvariantViolationException(
                    $"Invariant violation: Address 0x{address:X} is not aligned to {alignment} bytes");
            }
        }

        /// <summary>
        /// Check that predicate register index is valid (0-15)
        /// </summary>
        [Conditional("TRACE_INVARIANTS")]
        public static void CheckPredicateRegisterIndex(byte index)
        {
            if (index >= 16)
            {
                throw new InvariantViolationException(
                    $"Invariant violation: Predicate register index {index} is invalid (must be 0-15)");
            }
        }

        /// <summary>
        /// Check that rounding mode is valid (0-4)
        /// </summary>
        [Conditional("TRACE_INVARIANTS")]
        public static void CheckRoundingMode(byte mode)
        {
            if (mode > 4)
            {
                throw new InvariantViolationException(
                    $"Invariant violation: Rounding mode {mode} is invalid (must be 0-4)");
            }
        }

        /// <summary>
        /// Check that exception mode is valid (0-2)
        /// </summary>
        [Conditional("TRACE_INVARIANTS")]
        public static void CheckExceptionMode(byte mode)
        {
            if (mode > 2)
            {
                throw new InvariantViolationException(
                    $"Invariant violation: Exception mode {mode} is invalid (must be 0-2)");
            }
        }
    }
}
