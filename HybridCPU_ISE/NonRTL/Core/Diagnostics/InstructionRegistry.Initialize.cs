
using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.Core
{
    public static partial class InstructionRegistry
    {
        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            RegisterScalarArithmeticInstructions();
            RegisterVectorInstructions();
            RegisterBaseInstructionSet();
        }

    }
}
