using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            /// <summary>
            /// Branch prediction state
            /// Simple static prediction: predict not taken
            /// </summary>
            public struct BranchPredictor
            {
                public bool PredictTaken;       // Prediction for current branch
                public ulong PredictedPC;       // Predicted next PC
                public bool Active;             // Is there an active branch prediction?

                public void Clear()
                {
                    PredictTaken = false;
                    PredictedPC = 0;
                    Active = false;
                }

                /// <summary>
                /// Make prediction for a branch instruction (simple: always predict not taken)
                /// </summary>
                public void Predict(ulong currentPC, ulong fallThroughPC)
                {
                    PredictTaken = false;
                    PredictedPC = fallThroughPC;
                    Active = true;
                }

                /// <summary>
                /// Check if prediction was correct
                /// </summary>
                public bool WasCorrect(bool actualTaken, ulong actualPC)
                {
                    return (PredictTaken == actualTaken) && (PredictedPC == actualPC);
                }
            }
        }
    }
}
