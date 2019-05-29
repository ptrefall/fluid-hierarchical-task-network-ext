using System;
using System.Collections.Generic;
using FluidHTN.PrimitiveTasks;

namespace FluidHTN.Compounds
{
    /// <summary>
    ///     A compound task that will always succeed, even if its children fail decomposition.
    /// </summary>
    public class AlwaysSucceedSelector : Selector
    {
        // ========================================================= FIELDS

        protected Random _random = new Random();

        // ========================================================= DECOMPOSITION

        /// <summary>
        ///     In an Always Succeed decomposition, we always return success. This allows it to be used
        ///     as an "optional" branch during decomposition, without invalidating a Sequence.
        ///     Note that this selector makes sense only in a Sequence.
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
                    result = Plan;
                    return DecompositionStatus.Succeeded;
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
            return DecompositionStatus.Succeeded;
        }
    }
}