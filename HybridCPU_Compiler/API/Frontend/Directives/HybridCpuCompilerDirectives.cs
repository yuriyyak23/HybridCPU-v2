using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU;

namespace HybridCPU.Compiler.Core
{
    /// <summary>
    /// Assembly directive parser for HybridCPU compiler.
    /// Handles .excmode and .roundmode directives for vector exception handling and FP rounding.
    /// </summary>
    public class HybridCpuCompilerDirectives
    {
        /// <summary>
        /// Result of directive parsing
        /// </summary>
        public struct DirectiveParseResult
        {
            public bool Success;
            public string ErrorMessage;
            public DirectiveType Type;
            public byte Value;
        }

        /// <summary>
        /// Supported directive types
        /// </summary>
        public enum DirectiveType
        {
            Unknown = 0,
            ExceptionMode = 1,    // .excmode
            RoundingMode = 2,     // .roundmode
            CSRRead = 3,          // csrr (Read CSR)
            CSRWrite = 4          // csrw (Write CSR)
        }

        /// <summary>
        /// CSR (Control and Status Register) addresses for vector operations
        /// </summary>
        public enum VectorCSR : ushort
        {
            VXSTAT = 0x008,   // Vector exception status register
            VRM = 0x009,      // Vector rounding mode register
            VXMODE = 0x00A    // Vector exception mode register
        }

        private readonly Processor.CPU_Core core;

        public HybridCpuCompilerDirectives(Processor.CPU_Core core)
        {
            this.core = core;
        }

        /// <summary>
        /// Parse an assembly directive line.
        /// Examples:
        ///   .excmode 0     ; Set to accumulate mode
        ///   .excmode 1     ; Set to trap-on-first mode
        ///   .excmode 2     ; Set to trap-on-any mode
        ///   .roundmode 0   ; Set to RNE (round to nearest, ties to even)
        ///   .roundmode 1   ; Set to RTZ (round towards zero)
        ///   .roundmode 2   ; Set to RDN (round down)
        ///   .roundmode 3   ; Set to RUP (round up)
        ///   .roundmode 4   ; Set to RMM (round to nearest, ties to max magnitude)
        /// </summary>
        /// <param name="line">Assembly directive line (without leading whitespace)</param>
        /// <returns>Parse result with success status, error message, and parsed values</returns>
        public DirectiveParseResult ParseDirective(string line)
        {
            DirectiveParseResult result = new DirectiveParseResult();
            result.Success = false;
            result.Type = DirectiveType.Unknown;
            result.Value = 0;
            result.ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(line))
            {
                result.ErrorMessage = "Empty directive line";
                return result;
            }

            // Trim and normalize
            line = line.Trim();

            // Remove comments (everything after ';')
            int commentIndex = line.IndexOf(';');
            if (commentIndex >= 0)
            {
                line = line.Substring(0, commentIndex).Trim();
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                result.ErrorMessage = "Directive line contains only comments";
                return result;
            }

            // Split directive and arguments. Commas are treated as separators too so both
            // `csrr x1, vxstat` and `csrr x1,vxstat` remain valid.
            string[] parts = line.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 1)
            {
                result.ErrorMessage = "No directive found";
                return result;
            }

            string directive = parts[0].ToLowerInvariant();

            // Parse .excmode directive
            if (directive == ".excmode")
            {
                return ParseExceptionModeDirective(parts);
            }

            // Parse .roundmode directive
            if (directive == ".roundmode")
            {
                return ParseRoundingModeDirective(parts);
            }

            // Parse csrr (CSR read) instruction
            if (directive == "csrr")
            {
                return ParseCSRReadInstruction(parts);
            }

            // Parse csrw (CSR write) instruction
            if (directive == "csrw")
            {
                return ParseCSRWriteInstruction(parts);
            }

            result.ErrorMessage = $"Unknown directive: {directive}";
            return result;
        }

        /// <summary>
        /// Parse .excmode directive
        /// Syntax: .excmode <mode>
        /// Where mode is: 0 (accumulate), 1 (trap-on-first), 2 (trap-on-any)
        /// </summary>
        private DirectiveParseResult ParseExceptionModeDirective(string[] parts)
        {
            DirectiveParseResult result = new DirectiveParseResult();
            result.Type = DirectiveType.ExceptionMode;
            result.Success = false;

            if (parts.Length < 2)
            {
                result.ErrorMessage = ".excmode directive requires a mode argument (0, 1, or 2)";
                return result;
            }

            if (!TryParseDirectiveByte(parts[1], out byte mode))
            {
                result.ErrorMessage = $".excmode: Invalid mode value '{parts[1]}'. Expected integer literal 0, 1, or 2";
                return result;
            }

            if (mode > 2)
            {
                result.ErrorMessage = $".excmode: Mode {mode} out of range. Valid values: 0 (accumulate), 1 (trap-on-first), 2 (trap-on-any)";
                return result;
            }

            result.Value = mode;
            result.Success = true;
            return result;
        }

        /// <summary>
        /// Parse .roundmode directive
        /// Syntax: .roundmode <mode>
        /// Where mode is: 0 (RNE), 1 (RTZ), 2 (RDN), 3 (RUP), 4 (RMM)
        /// </summary>
        private DirectiveParseResult ParseRoundingModeDirective(string[] parts)
        {
            DirectiveParseResult result = new DirectiveParseResult();
            result.Type = DirectiveType.RoundingMode;
            result.Success = false;

            if (parts.Length < 2)
            {
                result.ErrorMessage = ".roundmode directive requires a mode argument (0-4)";
                return result;
            }

            if (!TryParseDirectiveByte(parts[1], out byte mode))
            {
                result.ErrorMessage = $".roundmode: Invalid mode value '{parts[1]}'. Expected integer literal 0-4";
                return result;
            }

            if (mode > 4)
            {
                result.ErrorMessage = $".roundmode: Mode {mode} out of range. Valid values: 0 (RNE), 1 (RTZ), 2 (RDN), 3 (RUP), 4 (RMM)";
                return result;
            }

            result.Value = mode;
            result.Success = true;
            return result;
        }

        /// <summary>
        /// Parse CSR read instruction
        /// Syntax: csrr <dest_reg>, <csr_name>
        /// Example: csrr x1, vxstat
        /// </summary>
        private DirectiveParseResult ParseCSRReadInstruction(string[] parts)
        {
            DirectiveParseResult result = new DirectiveParseResult();
            result.Type = DirectiveType.CSRRead;
            result.Success = false;

            if (parts.Length < 3)
            {
                result.ErrorMessage = "csrr instruction requires: csrr <dest_reg>, <csr_name>";
                return result;
            }

            string destReg = parts[1];
            string csrName = parts[2].ToLowerInvariant();

            // Validate CSR name
            VectorCSR csrAddr;
            if (!TryParseCSRName(csrName, out csrAddr))
            {
                result.ErrorMessage = $"csrr: Unknown CSR '{csrName}'. Valid: vxstat, vrm, vxmode";
                return result;
            }

            result.Value = (byte)csrAddr;
            result.Success = true;
            result.ErrorMessage = $"Read CSR {csrName} (0x{(ushort)csrAddr:X3}) to register {destReg}";
            return result;
        }

        /// <summary>
        /// Parse CSR write instruction
        /// Syntax: csrw <csr_name>, <src_reg>
        /// Example: csrw vxmode, x1
        /// </summary>
        private DirectiveParseResult ParseCSRWriteInstruction(string[] parts)
        {
            DirectiveParseResult result = new DirectiveParseResult();
            result.Type = DirectiveType.CSRWrite;
            result.Success = false;

            if (parts.Length < 3)
            {
                result.ErrorMessage = "csrw instruction requires: csrw <csr_name>, <src_reg>";
                return result;
            }

            string csrName = parts[1].ToLowerInvariant();
            string srcReg = parts[2];

            // Validate CSR name
            VectorCSR csrAddr;
            if (!TryParseCSRName(csrName, out csrAddr))
            {
                result.ErrorMessage = $"csrw: Unknown CSR '{csrName}'. Valid: vxstat, vrm, vxmode";
                return result;
            }

            result.Value = (byte)csrAddr;
            result.Success = true;
            result.ErrorMessage = $"Write register {srcReg} to CSR {csrName} (0x{(ushort)csrAddr:X3})";
            return result;
        }

        /// <summary>
        /// Try to parse CSR name to CSR address
        /// </summary>
        private bool TryParseCSRName(string csrName, out VectorCSR csrAddr)
        {
            csrAddr = 0;
            switch (csrName)
            {
                case "vxstat":
                    csrAddr = VectorCSR.VXSTAT;
                    return true;
                case "vrm":
                    csrAddr = VectorCSR.VRM;
                    return true;
                case "vxmode":
                    csrAddr = VectorCSR.VXMODE;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseDirectiveByte(string token, out byte value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return byte.TryParse(token.AsSpan(2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out value);
            }

            return byte.TryParse(token, out value);
        }

        /// <summary>
        /// Apply a parsed directive to the CPU core's vector exception status.
        /// This modifies the runtime state based on the directive.
        /// </summary>
        /// <param name="result">Parsed directive result</param>
        /// <returns>True if applied successfully, false otherwise</returns>
        public bool ApplyDirective(DirectiveParseResult result)
        {
            if (!result.Success)
            {
                return false;
            }

            switch (result.Type)
            {
                case DirectiveType.ExceptionMode:
                    return core.ExceptionStatus.SetExceptionMode(result.Value);

                case DirectiveType.RoundingMode:
                    return core.ExceptionStatus.SetRoundingMode(result.Value);

                case DirectiveType.CSRRead:
                case DirectiveType.CSRWrite:
                    // CSR instructions are handled by the execution unit, not directives
                    // These are just parsed for syntax validation
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Get human-readable description of exception mode
        /// </summary>
        public static string GetExceptionModeDescription(byte mode)
        {
            switch (mode)
            {
                case 0: return "Accumulate (count exceptions, no trap)";
                case 1: return "Trap on first exception";
                case 2: return "Trap on any exception";
                default: return "Invalid mode";
            }
        }

        /// <summary>
        /// Get human-readable description of rounding mode
        /// </summary>
        public static string GetRoundingModeDescription(byte mode)
        {
            switch (mode)
            {
                case 0: return "RNE (Round to Nearest, ties to Even)";
                case 1: return "RTZ (Round Towards Zero)";
                case 2: return "RDN (Round Down, towards -infinity)";
                case 3: return "RUP (Round Up, towards +infinity)";
                case 4: return "RMM (Round to Nearest, ties to Max Magnitude)";
                default: return "Invalid mode";
            }
        }

        /// <summary>
        /// Batch process multiple directive lines from assembly source.
        /// Processes directives sequentially and stops on first error.
        /// </summary>
        /// <param name="lines">Array of assembly directive lines</param>
        /// <param name="applyDirectives">If true, apply directives to core state</param>
        /// <returns>List of parse results for each line</returns>
        public List<DirectiveParseResult> ProcessDirectives(string[] lines, bool applyDirectives = true)
        {
            List<DirectiveParseResult> results = new List<DirectiveParseResult>();

            foreach (string line in lines)
            {
                // Skip empty lines and pure comment lines
                string trimmedLine = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith(";"))
                {
                    continue;
                }

                // Parse directive
                DirectiveParseResult result = ParseDirective(trimmedLine);
                results.Add(result);

                // Apply directive if requested and parsing succeeded
                if (applyDirectives && result.Success)
                {
                    if (!ApplyDirective(result))
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Failed to apply directive: {result.ErrorMessage}";
                    }
                }

                // Stop on first error
                if (!result.Success)
                {
                    break;
                }
            }

            return results;
        }
    }
}
