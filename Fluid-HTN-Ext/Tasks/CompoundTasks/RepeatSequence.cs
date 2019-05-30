using System;
using System.Collections.Generic;

namespace FluidHTN.Compounds
{
    /// <summary>
    ///     A sequence that repeats its subtasks a number of times.
    /// </summary>
    public class RepeatSequence : Sequence
    {
        /// <summary>
        ///     Possible repetition types for RepeatSequences.
        /// </summary>
        public enum RepetitionType : byte
        {
            /// <summary>
            ///     Entire sequence is repeated.
            ///     A,B,C => A,B,C,A,B,C
            /// </summary>
            Interleaved,
            /// <summary>
            ///     Individual tasks are repeated.
            ///     A,B.C => A,A,B,B,C,C
            /// </summary>
            Blockwise
        }

        protected readonly int Repetitions;
        private readonly RepetitionType Type;

        /// <summary>
        ///     A sequence that repeats its subtasks a number of times.
        /// </summary>
        /// <param name="repetitions">How many repetitions to perform. (>=1)</param>
        /// <param name="type">How to repeat the tasks.</param>
        public RepeatSequence(int repetitions, RepetitionType type = RepetitionType.Interleaved)
        {
            if (repetitions < 1)
            {
                throw new ArgumentException("Cannot have fewer than 1 repetitions!", "repetitions");
            }
            else
            {
                Repetitions = repetitions;
                Type = type;
            }
        }

        protected override DecompositionStatus OnDecompose(IContext ctx, int startIndex, out Queue<ITask> result)
        {
            if (Type == RepetitionType.Interleaved)
            {
                PreDecomposeInterleaved();
            }
            else
            {
                PreDecomposeBlockwise();
            }

            return base.OnDecompose(ctx, startIndex, out result);
        }

        /// <summary>
        ///     Duplicates every entry in the Subtasks list such that the entire
        ///     sequence is performed the specified number of times.
        /// </summary>
        private void PreDecomposeInterleaved()
        {
            int num = Subtasks.Count;
            for (int i = 0; i < Repetitions - 1; ++i)
            {
                for (int j = 0; j < num; ++j)
                {
                    Subtasks.Add(Subtasks[j]);
                }
            }
        }

        /// <summary>
        ///     Duplicates every entry in the Subtasks list such that every task
        ///     is repeated the specified number of times before the agent moves
        ///     on to the next task.
        /// </summary>
        private void PreDecomposeBlockwise()
        {
            int num = Subtasks.Count;
            for (int i = num - 1; i >= 0; --i)
            {
                for (int j = 0; j < Repetitions - 1; ++j)
                {
                    Subtasks.Insert(i, Subtasks[i]);
                }
            }
        }
    }
}
