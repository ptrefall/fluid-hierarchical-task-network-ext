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
            ///     A,B,C => A,A,B,B,C,C
            /// </summary>
            Blockwise
        }

        /// <summary>
        ///     The index in the world state from where we want to read the repetition value.
        /// </summary>
        protected readonly uint WorldStateIndex;

        /// <summary>
        ///     How to repeat the subtasks.
        /// </summary>
        private readonly RepetitionType _type;

        /// <summary>
        ///     A sequence that repeats its sub-tasks a number of times.
        /// </summary>
        public RepeatSequence(uint worldStateIndex, RepetitionType type = RepetitionType.Interleaved)
        {
            WorldStateIndex = worldStateIndex;
            _type = type;
        }

        protected override DecompositionStatus OnDecompose(IContext ctx, int startIndex, out Queue<ITask> result)
        {
            Plan.Clear();

            if (IsValidWorldStateIndex(ctx) == false)
            {
                result = Plan;
                return DecompositionStatus.Failed;
            }

            var repetitions = ctx.GetState((int)WorldStateIndex);

            switch (_type)
            {
                case RepetitionType.Interleaved:
                default:
                {
                    return DecomposeInterleaved(ctx, startIndex, repetitions, out result);
                }

                case RepetitionType.Blockwise:
                {
                    return DecomposeBlockwise(ctx, startIndex, repetitions, out result);
                }
            }
        }

        private DecompositionStatus DecomposeInterleaved(IContext ctx, int startIndex, byte repetitions, out Queue<ITask> result)
        {
            var oldStackDepth = ctx.GetWorldStateChangeDepth(ctx.Factory);

            for (int i = 0; i < repetitions; ++i)
            {
                for (var taskIndex = startIndex; taskIndex < Subtasks.Count; taskIndex++)
                {
                    var task = Subtasks[taskIndex];
                    var status = OnDecomposeTask(ctx, task, taskIndex, oldStackDepth, out result);
                    switch (status)
                    {
                        case DecompositionStatus.Rejected:
                        case DecompositionStatus.Failed:
                        {
                            ctx.Factory.FreeArray(ref oldStackDepth);
                            return status;
                        }

                        //TODO: Repeat sequences does not support partials yet!
                        case DecompositionStatus.Partial:
                        {
                            ctx.Factory.FreeArray(ref oldStackDepth);
                            return DecompositionStatus.Failed;
                        }
                    }
                }
            }

            ctx.Factory.FreeArray(ref oldStackDepth);

            result = Plan;

            return result.Count == 0 ? DecompositionStatus.Failed : DecompositionStatus.Succeeded;
        }

        private DecompositionStatus DecomposeBlockwise(IContext ctx, int startIndex, byte repetitions, out Queue<ITask> result)
        {
            var oldStackDepth = ctx.GetWorldStateChangeDepth(ctx.Factory);

            for (var taskIndex = startIndex; taskIndex < Subtasks.Count; taskIndex++)
            {
                var task = Subtasks[taskIndex];

                for (int i = 0; i < repetitions; ++i)
                {
                    var status = OnDecomposeTask(ctx, task, taskIndex, oldStackDepth, out result);
                    switch (status)
                    {
                        case DecompositionStatus.Rejected:
                        case DecompositionStatus.Failed:
                        {
                            ctx.Factory.FreeArray(ref oldStackDepth);
                            return status;
                        }

                        //TODO: Repeat sequences does not support partials yet!
                        case DecompositionStatus.Partial:
                        {
                            ctx.Factory.FreeArray(ref oldStackDepth);
                            return DecompositionStatus.Failed;
                        }
                    }
                }
            }

            ctx.Factory.FreeArray(ref oldStackDepth);

            result = Plan;
            return result.Count == 0 ? DecompositionStatus.Failed : DecompositionStatus.Succeeded;
        }

        private bool IsValidWorldStateIndex(IContext ctx)
        {
            return WorldStateIndex < ctx.WorldState.Length;
        }
    }
}
