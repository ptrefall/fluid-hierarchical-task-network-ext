using System;
using System.Collections.Generic;
using FluidHTN.PrimitiveTasks;

namespace FluidHTN.Compounds
{
    /// <summary>
    ///     A compound task that will pick a random sub-task to decompose.
    /// </summary>
    public class RandomSelector : Selector
    {
        // ========================================================= FIELDS

        protected Random _random = new Random();

        // ========================================================= DECOMPOSITION

        /// <summary>
        ///     In a Random Selector decomposition, we simply select a sub-task randomly, and stick with it for the duration of the
        ///     plan as if it was the only sub-task.
        ///     So if the sub-task fail to decompose, that means the entire Selector failed to decompose (we don't try to decompose
        ///     any other sub-tasks).
        ///     Because of the nature of the Random Selector, we don't do any MTR tracking for it, since it doesn't do any real
        ///     branching.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        protected override DecompositionStatus OnDecompose(IContext ctx, int startIndex, out Queue<ITask> result)
        {
            Plan.Clear();

            var taskIndex = _random.Next(startIndex, Subtasks.Count);
            var task = Subtasks[taskIndex];

            return OnDecomposeTask(ctx, task, taskIndex, null, out result);
        }
    }
}
