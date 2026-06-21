using HybridCPU_ISE.Core;
using System.Text;

namespace YAKSys_Hybrid_CPU
{
    public partial class PerformanceReport
    {
        #region Pipeline Statistics

        /// <summary>
        /// Total instructions executed
        /// </summary>
        public long TotalInstructions { get; set; }

        /// <summary>
        /// Total cycles elapsed
        /// </summary>
        public long TotalCycles { get; set; }

        /// <summary>
        /// Instructions Per Cycle (IPC)
        /// </summary>
        public double IPC
        {
            get
            {
                if (TotalCycles == 0) return 0.0;
                return (double)TotalInstructions / TotalCycles;
            }
        }

        /// <summary>
        /// Pipeline stalls
        /// </summary>
        public long PipelineStalls { get; set; }

        /// <summary>
        /// Branch mispredictions
        /// </summary>
        public long BranchMispredictions { get; set; }

        #endregion

        #region Vector Engine Statistics

        /// <summary>
        /// Vector operations executed
        /// </summary>
        public long VectorOperations { get; set; }

        /// <summary>
        /// Elements processed by vector engine
        /// </summary>
        public long VectorElementsProcessed { get; set; }

        /// <summary>
        /// Vector exceptions
        /// </summary>
        public long VectorExceptions { get; set; }

        #endregion
    }
}
