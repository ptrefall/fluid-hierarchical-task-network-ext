using System.Collections.Generic;
using FluidHTN.Conditions;
using FluidHTN.PrimitiveTasks;

namespace FluidHTN.Compounds
{
    /// <summary>
    /// A compound task that will use GOAP to find a shortest path
    /// through a set of sub-tasks to reach a goal state. It will
    /// return a sequence of tasks validated for use.
    /// </summary>
    public class GOAPSequence : Sequence
    {
        // ========================================================= FIELDS
        private Dictionary<byte, byte> _goal;

        // ========================================================= DECOMPOSITION

        protected override Queue<ITask> OnDecompose(IContext ctx, int startIndex)
        {
            Plan.Clear();

            var leaves = ctx.Factory.CreateList<GOAPNode>();
            var start = new GOAPNode() {Parent = null, RunningCost = 0f, Task = null};
            if (TryBuildGraph(ctx, start, leaves, Subtasks))
            {
                GeneratePlan(ctx, GetCheapestLeaf(leaves));
            }
            ctx.Factory.FreeList(ref leaves);

            return Plan;
        }

        private GOAPNode? GetCheapestLeaf(List<GOAPNode> leaves)
        {
            GOAPNode? cheapestLeaf = null;
            foreach (var leaf in leaves)
            {
                if (cheapestLeaf.HasValue)
                {
                    if (leaf.RunningCost < cheapestLeaf.Value.RunningCost)
                    {
                        cheapestLeaf = leaf;
                    }
                }
                else
                {
                    cheapestLeaf = leaf;
                }
            }

            return cheapestLeaf;
        }

        /// <summary>
        /// We first traverse to the root, then apply tasks from there to get them in the right order
        /// such that the last task on the queue is the task of our leaf node that we started with.
        /// </summary>
        /// <param name="node"></param>
        private void GeneratePlan(IContext ctx, GOAPNode? node)
        {
            if (node.HasValue)
            {
                GeneratePlan(ctx, node.Value.Parent);

                node.Value.Task.ApplyEffects(ctx);
                Plan.Enqueue(node.Value.Task);
            }

        }

        private bool TryBuildGraph(IContext ctx, GOAPNode parent, List<GOAPNode> leaves, List<ITask> openSubtasks)
        {
            var foundLeaf = false;

            for (var taskIndex = 0; taskIndex < openSubtasks.Count; taskIndex++)
            {
                var task = Subtasks[taskIndex];
                if (task.IsValid(ctx) == false)
                {
                    continue;
                }

                if (task is IGOAPTask goapTask)
                {
                    // Due to branching permutations of state, and multiple possible solutions
                    // to the goal, where we want to end up with the shortest path, we need to
                    // reset the state stack for every task we operate on.
                    var oldStackDepth = ctx.GetWorldStateChangeDepth(ctx.Factory);

                    goapTask.ApplyEffects(ctx);

                    var node = new GOAPNode() { Parent = parent, RunningCost = parent.RunningCost + goapTask.Cost(ctx), Task = goapTask};
                    if (ValidatesGoal(ctx))
                    {
                        leaves.Add(node);
                        foundLeaf = true;
                    }
                    else
                    {
                        var subset = GetSubset(ctx, task, openSubtasks);
                        if (TryBuildGraph(ctx, node, leaves, subset))
                        {
                            foundLeaf = true;
                        }
                        ctx.Factory.FreeList(ref subset);
                    }

                    // Because of the branching permutations of state with GOAP, 
                    // we must always reset the state stack back to the state it
                    // was in previous to the changes applied by this particular
                    // permutation.
                    ctx.TrimToStackDepth(oldStackDepth);
                }
            }

            return foundLeaf;
        }

        private bool ValidatesGoal(IContext ctx)
        {
            foreach (var kvp in _goal)
            {
                if (ctx.GetState((int)kvp.Key) != kvp.Value)
                {
                    return false;
                }
            }

            return true;
        }

        private List<ITask> GetSubset(IContext ctx, ITask currentTask, List<ITask> tasks)
        {
            var subset = ctx.Factory.CreateList<ITask>();
            foreach (var task in tasks)
            {
                if (task == currentTask)
                    continue;

                subset.Add(task);
            }

            return subset;
        }

        private struct GOAPNode
        {
            public GOAPNode? Parent;
            public float RunningCost;
            public IGOAPTask Task;
        }
    }
}