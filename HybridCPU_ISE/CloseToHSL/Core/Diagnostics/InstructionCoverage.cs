using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Core
{
    /// <summary>
    /// Tracks instruction execution coverage and exception occurrence
    /// </summary>
    public class InstructionCoverage
    {
        private readonly Dictionary<uint, long> executionCount;
        private readonly Dictionary<string, long> exceptionCount;
        private long totalInstructions;
        private long totalExceptions;

        public InstructionCoverage()
        {
            executionCount = new Dictionary<uint, long>();
            exceptionCount = new Dictionary<string, long>
            {
                ["Overflow"] = 0,
                ["Underflow"] = 0,
                ["DivByZero"] = 0,
                ["InvalidOp"] = 0,
                ["Inexact"] = 0
            };
            totalInstructions = 0;
            totalExceptions = 0;
        }

        /// <summary>
        /// Record instruction execution
        /// </summary>
        public void Record(uint opcode)
        {
            if (!executionCount.ContainsKey(opcode))
            {
                executionCount[opcode] = 0;
            }
            executionCount[opcode]++;
            totalInstructions++;
        }

        /// <summary>
        /// Record exception occurrence
        /// </summary>
        public void RecordException(VectorExceptionFlags flags)
        {
            if (flags.Overflow)
            {
                exceptionCount["Overflow"]++;
                totalExceptions++;
            }
            if (flags.Underflow)
            {
                exceptionCount["Underflow"]++;
                totalExceptions++;
            }
            if (flags.DivByZero)
            {
                exceptionCount["DivByZero"]++;
                totalExceptions++;
            }
            if (flags.InvalidOp)
            {
                exceptionCount["InvalidOp"]++;
                totalExceptions++;
            }
            if (flags.Inexact)
            {
                exceptionCount["Inexact"]++;
                totalExceptions++;
            }
        }

        /// <summary>
        /// Get execution count for specific opcode
        /// </summary>
        public long GetExecutionCount(uint opcode)
        {
            return executionCount.TryGetValue(opcode, out var count) ? count : 0;
        }

        /// <summary>
        /// Get exception count for specific exception type
        /// </summary>
        public long GetExceptionCount(string exceptionType)
        {
            return exceptionCount.TryGetValue(exceptionType, out var count) ? count : 0;
        }

        /// <summary>
        /// Get total instructions executed
        /// </summary>
        public long TotalInstructions => totalInstructions;

        /// <summary>
        /// Get total exceptions occurred
        /// </summary>
        public long TotalExceptions => totalExceptions;

        /// <summary>
        /// Get count of unique opcodes executed
        /// </summary>
        public int UniqueOpcodes => executionCount.Count;

        /// <summary>
        /// Clear all coverage data
        /// </summary>
        public void Clear()
        {
            executionCount.Clear();
            foreach (var key in exceptionCount.Keys.ToList())
            {
                exceptionCount[key] = 0;
            }
            totalInstructions = 0;
            totalExceptions = 0;
        }

        /// <summary>
        /// Dump coverage report to file
        /// </summary>
        public void DumpCoverageReport(string path)
        {
            using var writer = new StreamWriter(path);

            writer.WriteLine("=== HybridCPU ISE Instruction Coverage Report ===");
            writer.WriteLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            writer.WriteLine();

            writer.WriteLine("--- Summary ---");
            writer.WriteLine($"Total Instructions Executed: {totalInstructions:N0}");
            writer.WriteLine($"Unique Opcodes Executed: {UniqueOpcodes}");
            writer.WriteLine($"Total Exceptions: {totalExceptions:N0}");
            writer.WriteLine();

            writer.WriteLine("--- Instruction Execution Counts ---");
            writer.WriteLine($"{"Opcode",-30} {"Count",15} {"Percentage",12}");
            writer.WriteLine(new string('-', 60));

            var sortedOpcodes = executionCount.OrderByDescending(kvp => kvp.Value);
            foreach (var kvp in sortedOpcodes)
            {
                var percentage = totalInstructions > 0
                    ? (kvp.Value * 100.0 / totalInstructions)
                    : 0.0;
                writer.WriteLine($"{kvp.Key,-30} {kvp.Value,15:N0} {percentage,11:F2}%");
            }

            writer.WriteLine();
            writer.WriteLine("--- Exception Counts ---");
            writer.WriteLine($"{"Exception Type",-30} {"Count",15} {"Percentage",12}");
            writer.WriteLine(new string('-', 60));

            var sortedExceptions = exceptionCount.OrderByDescending(kvp => kvp.Value);
            foreach (var kvp in sortedExceptions)
            {
                var percentage = totalExceptions > 0
                    ? (kvp.Value * 100.0 / totalExceptions)
                    : 0.0;
                writer.WriteLine($"{kvp.Key,-30} {kvp.Value,15:N0} {percentage,11:F2}%");
            }

            writer.WriteLine();
            writer.WriteLine("--- ISA Coverage Analysis ---");
            var totalOpcodes = 256; // Max opcode value for byte
            var coveragePercent = totalOpcodes > 0
                ? (UniqueOpcodes * 100.0 / totalOpcodes)
                : 0.0;
            writer.WriteLine($"Total Opcodes in ISA: {totalOpcodes}");
            writer.WriteLine($"Opcodes Executed: {UniqueOpcodes}");
            writer.WriteLine($"ISA Coverage: {coveragePercent:F2}%");

            writer.WriteLine();
            writer.WriteLine("--- Unexecuted Opcodes ---");
            var unexecutedOpcodes = new List<uint>();
            for (uint i = 0; i < 256; i++)
            {
                if (!executionCount.ContainsKey(i))
                {
                    unexecutedOpcodes.Add(i);
                }
            }

            if (unexecutedOpcodes.Any())
            {
                foreach (var opcode in unexecutedOpcodes)
                {
                    writer.WriteLine($"  Opcode {opcode}");
                }
            }
            else
            {
                writer.WriteLine("  (All opcodes executed)");
            }

            writer.WriteLine();
            writer.WriteLine("=== End of Report ===");
        }

        /// <summary>
        /// Get coverage statistics as dictionary
        /// </summary>
        public Dictionary<string, object> GetStatistics()
        {
            var totalOpcodes = 256; // Max opcode value for byte
            var coveragePercent = totalOpcodes > 0
                ? (UniqueOpcodes * 100.0 / totalOpcodes)
                : 0.0;

            return new Dictionary<string, object>
            {
                ["TotalInstructions"] = totalInstructions,
                ["UniqueOpcodes"] = UniqueOpcodes,
                ["TotalOpcodes"] = totalOpcodes,
                ["CoveragePercent"] = coveragePercent,
                ["TotalExceptions"] = totalExceptions,
                ["ExceptionCounts"] = new Dictionary<string, long>(exceptionCount)
            };
        }
    }
}
