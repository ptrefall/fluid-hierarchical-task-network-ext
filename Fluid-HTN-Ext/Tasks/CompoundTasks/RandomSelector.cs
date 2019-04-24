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
        protected override Queue<ITask> OnDecompose(IContext ctx, int startIndex)
        {
            Plan.Clear();

            var taskIndex = _random.Next(startIndex, Subtasks.Count - 1);
            var task = Subtasks[taskIndex];

            if (task.IsValid(ctx) == false)
                return Plan;

            if (task is ICompoundTask compoundTask)
            {
                var result = compoundTask.Decompose(ctx, 0);

                // If result is null, that means the entire planning procedure should cancel.
                if (result == null) return null;

                // If the decomposition failed
                if (result.Count == 0) return Plan;

                while (result.Count > 0)
                {
                    var res = result.Dequeue();
                    Plan.Enqueue(res);
                }
            }
            else if (task is IPrimitiveTask primitiveTask)
            {
                primitiveTask.ApplyEffects(ctx);
                Plan.Enqueue(task);
            }

            return Plan;
        }
    }
}