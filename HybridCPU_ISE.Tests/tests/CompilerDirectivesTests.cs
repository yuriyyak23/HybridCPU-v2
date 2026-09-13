using HybridCPU.Compiler.Core;
using System;
using System.Linq;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_Tests
{
    /// <summary>
    /// Unit tests for compiler directive parsing.
    /// Tests .excmode and .roundmode directives, CSR instruction parsing, and error handling.
    /// </summary>
    public class CompilerDirectivesTests
    {
        /// <summary>
        /// Test basic directive parsing for exception mode
        /// </summary>
        public static bool Test_ExceptionMode_Parsing()
        {
            Console.WriteLine("\n=== Test: Exception Mode Directive Parsing ===");

            // Create a dummy CPU core for testing
            Processor.CPU_Core testCore = new Processor.CPU_Core(0);
            testCore.ExceptionStatus.Reset();

            HybridCpuCompilerDirectives parser = new HybridCpuCompilerDirectives(testCore);

            // Test valid exception modes
            string[] validDirectives = {
                ".excmode 0",     // Accumulate
                ".excmode 1",     // Trap-on-first
                ".excmode 2",     // Trap-on-any
            };

            bool allPassed = true;
            foreach (string directive in validDirectives)
            {
                var result = parser.ParseDirective(directive);
                if (!result.Success)
                {
                    Console.WriteLine($"FAIL: {directive} - {result.ErrorMessage}");
                    allPassed = false;
                }
                else
                {
                    Console.WriteLine($"PASS: {directive} => mode {result.Value}");
                }
            }

            // Test invalid exception modes
            string[] invalidDirectives = {
                ".excmode 3",      // Out of range
                ".excmode -1",     // Negative
                ".excmode abc",    // Not a number
                ".excmode",        // Missing argument
            };

            foreach (string directive in invalidDirectives)
            {
                var result = parser.ParseDirective(directive);
                if (result.Success)
                {
                    Console.WriteLine($"FAIL: {directive} - Should have failed but passed");
                    allPassed = false;
                }
                else
                {
                    Console.WriteLine($"PASS: {directive} - Correctly rejected: {result.ErrorMessage}");
                }
            }

            return allPassed;
        }

        /// <summary>
        /// Test basic directive parsing for rounding mode
        /// </summary>
        public static bool Test_RoundingMode_Parsing()
        {
            Console.WriteLine("\n=== Test: Rounding Mode Directive Parsing ===");

            Processor.CPU_Core testCore = new Processor.CPU_Core(0);
            testCore.ExceptionStatus.Reset();

            HybridCpuCompilerDirectives parser = new HybridCpuCompilerDirectives(testCore);

            // Test valid rounding modes
            string[] validDirectives = {
                ".roundmode 0",   // RNE
                ".roundmode 1",   // RTZ
                ".roundmode 2",   // RDN
                ".roundmode 3",   // RUP
                ".roundmode 4",   // RMM
            };

            bool allPassed = true;
            foreach (string directive in validDirectives)
            {
                var result = parser.ParseDirective(directive);
                if (!result.Success)
                {
                    Console.WriteLine($"FAIL: {directive} - {result.ErrorMessage}");
                    allPassed = false;
                }
                else
                {
                    Console.WriteLine($"PASS: {directive} => mode {result.Value}");
                }
            }

            // Test invalid rounding modes
            string[] invalidDirectives = {
                ".roundmode 5",      // Out of range
                ".roundmode -1",     // Negative
                ".roundmode xyz",    // Not a number
                ".roundmode",        // Missing argument
            };

            foreach (string directive in invalidDirectives)
            {
                var result = parser.ParseDirective(directive);
                if (result.Success)
                {
                    Console.WriteLine($"FAIL: {directive} - Should have failed but passed");
                    allPassed = false;
                }
                else
                {
                    Console.WriteLine($"PASS: {directive} - Correctly rejected: {result.ErrorMessage}");
                }
            }

            return allPassed;
        }

        /// <summary>
        /// Test CSR instruction parsing
        /// </summary>
        public static bool Test_CSR_Instruction_Parsing()
        {
            Console.WriteLine("\n=== Test: CSR Instruction Parsing ===");

            Processor.CPU_Core testCore = new Processor.CPU_Core(0);
            testCore.ExceptionStatus.Reset();

            HybridCpuCompilerDirectives parser = new HybridCpuCompilerDirectives(testCore);

            // Test CSR read instructions
            string[] csrReadTests = {
                "csrr t1, vxstat",    // Valid
                "csrr t2, vrm",       // Valid
                "csrr t3, vxmode",    // Valid
                "csrr x1, vxstat",    // Valid (alternative register name)
            };

            bool allPassed = true;
            foreach (string instr in csrReadTests)
            {
                var result = parser.ParseDirective(instr);
                if (!result.Success)
                {
                    Console.WriteLine($"FAIL: {instr} - {result.ErrorMessage}");
                    allPassed = false;
                }
                else
                {
                    Console.WriteLine($"PASS: {instr}");
                }
            }

            // Test CSR write instructions
            string[] csrWriteTests = {
                "csrw vxmode, t1",    // Valid
                "csrw vrm, t2",       // Valid
                "csrw vxstat, x0",    // Valid (clear with zero)
            };

            foreach (string instr in csrWriteTests)
            {
                var result = parser.ParseDirective(instr);
                if (!result.Success)
                {
                    Console.WriteLine($"FAIL: {instr} - {result.ErrorMessage}");
                    allPassed = false;
                }
                else
                {
                    Console.WriteLine($"PASS: {instr}");
                }
            }

            // Test invalid CSR instructions
            string[] invalidCSRTests = {
                "csrr t1, invalid_csr",    // Unknown CSR
                "csrw invalid_csr, t1",    // Unknown CSR
                "csrr",                     // Missing arguments
                "csrw vxmode",              // Missing source register
            };

            foreach (string instr in invalidCSRTests)
            {
                var result = parser.ParseDirective(instr);
                if (result.Success)
                {
                    Console.WriteLine($"FAIL: {instr} - Should have failed but passed");
                    allPassed = false;
                }
                else
                {
                    Console.WriteLine($"PASS: {instr} - Correctly rejected: {result.ErrorMessage}");
                }
            }

            return allPassed;
        }

        /// <summary>
        /// Test directive application (actually modifying CPU core state)
        /// </summary>
        public static bool Test_Directive_Application()
        {
            Console.WriteLine("\n=== Test: Directive Application to CPU Core ===");

            Processor.CPU_Core testCore = new Processor.CPU_Core(0);
            testCore.ExceptionStatus.Reset();

            HybridCpuCompilerDirectives parser = new HybridCpuCompilerDirectives(testCore);

            bool allPassed = true;

            // Test exception mode application
            var excResult = parser.ParseDirective(".excmode 1");
            if (!excResult.Success || !parser.ApplyDirective(excResult))
            {
                Console.WriteLine($"FAIL: Could not apply .excmode 1");
                allPassed = false;
            }
            else if (testCore.ExceptionStatus.ExceptionMode != 1)
            {
                Console.WriteLine($"FAIL: Exception mode not applied correctly. Expected 1, got {testCore.ExceptionStatus.ExceptionMode}");
                allPassed = false;
            }
            else
            {
                Console.WriteLine($"PASS: .excmode 1 applied successfully");
            }

            // Test rounding mode application
            var rmResult = parser.ParseDirective(".roundmode 3");
            if (!rmResult.Success || !parser.ApplyDirective(rmResult))
            {
                Console.WriteLine($"FAIL: Could not apply .roundmode 3");
                allPassed = false;
            }
            else if (testCore.ExceptionStatus.RoundingMode != 3)
            {
                Console.WriteLine($"FAIL: Rounding mode not applied correctly. Expected 3, got {testCore.ExceptionStatus.RoundingMode}");
                allPassed = false;
            }
            else
            {
                Console.WriteLine($"PASS: .roundmode 3 applied successfully");
            }

            // Test reset to defaults
            testCore.ExceptionStatus.Reset();
            if (testCore.ExceptionStatus.ExceptionMode != 0 || testCore.ExceptionStatus.RoundingMode != 0)
            {
                Console.WriteLine($"FAIL: Reset did not restore defaults");
                allPassed = false;
            }
            else
            {
                Console.WriteLine($"PASS: Reset restored defaults (excmode=0, roundmode=0)");
            }

            return allPassed;
        }

        /// <summary>
        /// Test comment handling
        /// </summary>
        public static bool Test_Comment_Handling()
        {
            Console.WriteLine("\n=== Test: Comment Handling ===");

            Processor.CPU_Core testCore = new Processor.CPU_Core(0);
            testCore.ExceptionStatus.Reset();

            HybridCpuCompilerDirectives parser = new HybridCpuCompilerDirectives(testCore);

            // Test directives with comments
            string[] directivesWithComments = {
                ".excmode 1     ; Set to trap-on-first mode",
                ".roundmode 0   ; RNE mode",
                "csrr t1, vxstat ; Read exception status",
            };

            bool allPassed = true;
            foreach (string directive in directivesWithComments)
            {
                var result = parser.ParseDirective(directive);
                if (!result.Success)
                {
                    Console.WriteLine($"FAIL: {directive} - {result.ErrorMessage}");
                    allPassed = false;
                }
                else
                {
                    Console.WriteLine($"PASS: {directive} - Comment handled correctly");
                }
            }

            // Test pure comment lines
            string[] pureComments = {
                "; This is a comment",
                "   ; Indented comment",
                ";",
            };

            foreach (string comment in pureComments)
            {
                var result = parser.ParseDirective(comment);
                if (result.Success)
                {
                    Console.WriteLine($"FAIL: Pure comment should not parse as directive");
                    allPassed = false;
                }
                else
                {
                    Console.WriteLine($"PASS: Pure comment correctly ignored");
                }
            }

            return allPassed;
        }

        /// <summary>
        /// Test batch processing of multiple directives
        /// </summary>
        public static bool Test_Batch_Processing()
        {
            Console.WriteLine("\n=== Test: Batch Directive Processing ===");

            Processor.CPU_Core testCore = new Processor.CPU_Core(0);
            testCore.ExceptionStatus.Reset();

            HybridCpuCompilerDirectives parser = new HybridCpuCompilerDirectives(testCore);

            string[] batchDirectives = {
                "; Setup exception handling",
                ".excmode 0",
                ".roundmode 2",
                "",
                "; CSR access",
                "csrr t1, vxmode",
                "csrw vrm, t2",
            };

            var results = parser.ProcessDirectives(batchDirectives, applyDirectives: true);

            // Should have 4 successful results (2 comments, 1 blank ignored)
            int successCount = results.Count(r => r.Success);

            bool passed = successCount == 4;
            if (passed)
            {
                Console.WriteLine($"PASS: Batch processed {successCount} directives successfully");
                Console.WriteLine($"      ExceptionMode = {testCore.ExceptionStatus.ExceptionMode} (expected 0)");
                Console.WriteLine($"      RoundingMode = {testCore.ExceptionStatus.RoundingMode} (expected 2)");
            }
            else
            {
                Console.WriteLine($"FAIL: Expected 4 successful parses, got {successCount}");
            }

            return passed;
        }

        /// <summary>
        /// Run all tests
        /// </summary>
        public static void RunAllTests()
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine("HybridCPU Compiler Directives Unit Tests");
            Console.WriteLine("================================================================================");

            bool allPassed = true;

            allPassed &= Test_ExceptionMode_Parsing();
            allPassed &= Test_RoundingMode_Parsing();
            allPassed &= Test_CSR_Instruction_Parsing();
            allPassed &= Test_Directive_Application();
            allPassed &= Test_Comment_Handling();
            allPassed &= Test_Batch_Processing();

            Console.WriteLine("\n================================================================================");
            if (allPassed)
            {
                Console.WriteLine("✓ All tests PASSED");
            }
            else
            {
                Console.WriteLine("✗ Some tests FAILED");
            }
            Console.WriteLine("================================================================================\n");
        }

        /// <summary>
        /// Demonstration: Print descriptions of all modes
        /// </summary>
        public static void PrintModeDescriptions()
        {
            Console.WriteLine("\n=== Exception Modes ===");
            for (byte i = 0; i <= 2; i++)
            {
                Console.WriteLine($"  {i}: {HybridCpuCompilerDirectives.GetExceptionModeDescription(i)}");
            }

            Console.WriteLine("\n=== Rounding Modes ===");
            for (byte i = 0; i <= 4; i++)
            {
                Console.WriteLine($"  {i}: {HybridCpuCompilerDirectives.GetRoundingModeDescription(i)}");
            }
        }
    }
}
