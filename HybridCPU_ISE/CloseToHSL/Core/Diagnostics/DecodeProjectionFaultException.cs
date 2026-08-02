using System;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Typed fail-closed fault for decode projection/materialization seams.
    /// This is narrower than a generic InvalidOperationException so decode fallback
    /// does not silently absorb unrelated internal bugs.
    /// </summary>
    internal sealed class DecodeProjectionFaultException : InvalidOperationException
    {
        internal DecodeProjectionFaultException(string message)
            : base(message)
        {
        }

        internal DecodeProjectionFaultException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
