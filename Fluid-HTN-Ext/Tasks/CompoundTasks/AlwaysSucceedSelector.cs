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

        // ========================================================= VALIDITY

        public override DecompositionStatus OnIsValidFailed(IContext ctx)
        {
            return DecompositionStatus.Succeeded;
        }

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
            result = null;

            for (var taskIndex = startIndex; taskIndex < Subtasks.Count; taskIndex++)
            {
                var task = Subtasks[taskIndex];
                var status = OnDecomposeTask(ctx, task, taskIndex, null, out result);

                // Even though we always return success, we still treat this as a selector.
                if (status == DecompositionStatus.Failed)
                {
                    continue;
                }

                break;
            }

            if (result == null)
            {
                result = Plan;
            }

            return DecompositionStatus.Succeeded;
        }
    }
}
