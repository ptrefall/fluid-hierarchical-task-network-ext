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
        ///     Note that Partial is still returned as Partial (it doesn't really make sense to use with this selector).
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        protected override DecompositionStatus OnDecompose(IContext ctx, int startIndex, out Queue<ITask> result)
        {
            Plan.Clear();

            for (var taskIndex = startIndex; taskIndex < Subtasks.Count; taskIndex++)
            {
                var task = Subtasks[taskIndex];

                var status = OnDecomposeTask(ctx, task, taskIndex, null, out result);
                switch (status)
                {
                    case DecompositionStatus.Succeeded:
                        return DecompositionStatus.Failed;

                    // We treat this as a selector and will try until we decompose successfully
                    case DecompositionStatus.Failed:
                        continue;
                }

                // Rejected or Partial is not inverted.
                return status;
            }

            // If we failed to find a valid decomposition, we revert to Success.
            result = Plan;
            return DecompositionStatus.Succeeded;
        }
    }
}
