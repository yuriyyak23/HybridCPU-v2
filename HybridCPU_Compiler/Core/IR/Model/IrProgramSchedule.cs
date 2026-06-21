using System;
using System.Collections.Generic;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// Local scheduling result for one IR program.
    /// </summary>
    public sealed class IrProgramSchedule
    {
        private readonly Dictionary<int, IrBasicBlockSchedule> _blockSchedulesById = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="IrProgramSchedule"/> class.
        /// </summary>
        public IrProgramSchedule(
            IrProgram program,
            IrProgramDependencyGraph dependencyGraph,
            IReadOnlyList<IrBasicBlockSchedule> blockSchedules)
        {
            ArgumentNullException.ThrowIfNull(program);
            ArgumentNullException.ThrowIfNull(dependencyGraph);
            ArgumentNullException.ThrowIfNull(blockSchedules);

            Program = program;
            DependencyGraph = dependencyGraph;
            BlockSchedules = blockSchedules;

            foreach (IrBasicBlockSchedule blockSchedule in blockSchedules)
            {
                _blockSchedulesById[blockSchedule.BlockId] = blockSchedule;
            }
        }

        /// <summary>
        /// Gets the program that was scheduled.
        /// </summary>
        public IrProgram Program { get; }

        /// <summary>
        /// Gets the dependence graph consumed by the scheduler.
        /// </summary>
        public IrProgramDependencyGraph DependencyGraph { get; }

        /// <summary>
        /// Gets local block schedules for the program.
        /// </summary>
        public IReadOnlyList<IrBasicBlockSchedule> BlockSchedules { get; }

        /// <summary>
        /// Tries to get one block schedule by block identifier.
        /// </summary>
        public bool TryGetBlockSchedule(int blockId, out IrBasicBlockSchedule? blockSchedule)
        {
            return _blockSchedulesById.TryGetValue(blockId, out blockSchedule);
        }
    }
}
