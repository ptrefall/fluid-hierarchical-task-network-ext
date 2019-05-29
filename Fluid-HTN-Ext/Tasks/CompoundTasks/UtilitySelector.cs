using System.Collections.Generic;
using FluidHTN.PrimitiveTasks;

namespace FluidHTN.Compounds
{
    /// <summary>
    ///     A compound task that will pick the sub-task with the highest utility score.
    ///     It requires sub-tasks to implement the IUtilityTask interface.
    /// </summary>
    public class UtilitySelector : Selector
    {
        // ========================================================= DECOMPOSITION

        /// <summary>
        ///     In a Utility Selector decomposition, we select a single sub-task based on utility theory.
        ///     If the sub-task fail to decompose, that means the entire Selector failed to decompose (we don't try to decompose
        ///     any other sub-tasks).
        ///     Because of the nature of the Utility Selector, we don't do any MTR tracking for it, since it doesn't do any real
        ///     branching.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        protected override DecompositionStatus OnDecompose(IContext ctx, int startIndex, out Queue<ITask> result)
        {
            Plan.Clear();

            var task = FindBestTask(ctx, startIndex);
            if (task == null)
            {
                result = Plan;
                return DecompositionStatus.Failed;
            }

            if (task is ICompoundTask compoundTask)
            {
                var status = compoundTask.Decompose(ctx, 0, out var subPlan);

                // If result is null, that means the entire planning procedure should cancel.
                if (status == DecompositionStatus.Rejected)
                {
                    result = null;
                    return DecompositionStatus.Rejected;
                }

                // If the decomposition failed
                if (status == DecompositionStatus.Failed)
                {
                    result = Plan;
                    return DecompositionStatus.Failed;
                }

                while (subPlan.Count > 0)
                {
                    Plan.Enqueue(subPlan.Dequeue());
                }
            }
            else if (task is IPrimitiveTask primitiveTask)
            {
                primitiveTask.ApplyEffects(ctx);
                Plan.Enqueue(task);
            }

            result = Plan;
            return result.Count == 0 ? DecompositionStatus.Failed : DecompositionStatus.Succeeded;
        }

        // ========================================================= INTERNAL FUNCTIONALITY

        /// <summary>
        ///     We compare the utility among all sub-tasks and pick the best. We require these sub-tasks to implement the
        ///     IUtilityTask interface, and that they pass the IsValid check.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="startIndex"></param>
        /// <returns></returns>
        protected virtual ITask FindBestTask(IContext ctx, int startIndex)
        {
            var bestScore = 0f;
            ITask bestTask = null;

            for (var taskIndex = startIndex; taskIndex < Subtasks.Count; taskIndex++)
            {
                var task = Subtasks[taskIndex];
                if (task is IUtilityTask utilityTask)
                {
                    if (utilityTask.IsValid(ctx) == false)
                        continue;

                    var score = utilityTask.Score(ctx);
                    if (bestScore < score)
                    {
                        bestScore = score;
                        bestTask = task;
                    }
                }
            }

            return bestTask;
        }
    }
}