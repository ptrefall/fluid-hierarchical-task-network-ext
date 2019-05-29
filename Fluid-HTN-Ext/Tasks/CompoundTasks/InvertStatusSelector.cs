using System;
using System.Collections.Generic;
using FluidHTN.PrimitiveTasks;

namespace FluidHTN.Compounds
{
    /// <summary>
    ///     A compound task that will return success on failure and failure on success, rejected is still returned as rejected.
    /// </summary>
    public class InvertStatusSelector : Selector
    {
        // ========================================================= FIELDS

        protected Random _random = new Random();

        // ========================================================= DECOMPOSITION

        /// <summary>
        ///     In an Invert Status decomposition, we invert success/failure.
        ///     Note that Rejected is still returned as Rejected.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        protected override DecompositionStatus OnDecompose(IContext ctx, int startIndex, out Queue<ITask> result)
        {
            Plan.Clear();

            var taskIndex = _random.Next(startIndex, Subtasks.Count);
            var task = Subtasks[taskIndex];

            if (task.IsValid(ctx) == false)
            {
                result = Plan;
                return DecompositionStatus.Succeeded;
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
                    return DecompositionStatus.Succeeded;
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
            return DecompositionStatus.Failed;
        }
    }
}