using System;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Thrown when a runtime surface that previously fell back to Processor.MainMemory
    /// is invoked without an explicit bound memory instance.
    /// </summary>
    public sealed class MainMemoryBindingUnavailableException : InvalidOperationException
    {
        public MainMemoryBindingUnavailableException(string bindingSurface, string operation)
            : base(
                $"{Normalize(bindingSurface)} requires an explicit MainMemory binding for {Normalize(operation)}. " +
                "Implicit fallback to Processor.MainMemory is disabled; the runtime must fail closed instead of aliasing a mutable global memory surface.")
        {
            BindingSurface = Normalize(bindingSurface);
            Operation = Normalize(operation);
        }

        public string BindingSurface { get; }

        public string Operation { get; }

        private static string Normalize(string value) =>
            string.IsNullOrWhiteSpace(value)
                ? "<unknown>"
                : value;
    }
}
