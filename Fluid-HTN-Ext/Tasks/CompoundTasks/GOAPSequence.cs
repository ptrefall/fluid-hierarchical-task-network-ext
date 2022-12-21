using System.Collections.Generic;
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
        private Dictionary<byte, byte> _goal = new Dictionary<byte, byte>();

        public void AddGoalState(byte state, byte value)
        {
            _goal.Add(state, value);
        }

        // ========================================================= DECOMPOSITION

        protected override DecompositionStatus OnDecompose(IContext ctx, int startIndex, out Queue<ITask> result)
        {
            Plan.Clear();

            var leaves = ctx.Factory.CreateList<GOAPNode>();
            var start = ctx.Factory.Create<GOAPNode>();
            {
                start.Parent = null;
                start.RunningCost = 0f;
                start.Task = null;
            }

            if (TryBuildGraph(ctx, start, leaves, Subtasks))
            {
                GeneratePlan(ctx, GetCheapestLeaf(leaves));
            }

            foreach (var leaf in leaves)
            {
                FreeNode(ctx, leaf);
            }

            ctx.Factory.FreeList(ref leaves);

            result = Plan;

            return result.Count == 0 ? DecompositionStatus.Failed : DecompositionStatus.Succeeded;
        }

        private GOAPNode GetCheapestLeaf(List<GOAPNode> leaves)
        {
            GOAPNode cheapestLeaf = null;

            foreach (var leaf in leaves)
            {
                if (cheapestLeaf != null)
                {
                    if (leaf.RunningCost < cheapestLeaf.RunningCost)
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
        private void GeneratePlan(IContext ctx, GOAPNode node)
        {
            if (node != null && node.Task != null)
            {
                GeneratePlan(ctx, node.Parent);

                node.Task.ApplyEffects(ctx);
                Plan.Enqueue(node.Task);
            }

        }

        private void FreeNode(IContext ctx, GOAPNode node)
        {
            var nextNode = node.Parent;
            ctx.Factory.Free(ref node);

            if (nextNode != null)
            {
                FreeNode(ctx, nextNode);
            }
        }

        private bool TryBuildGraph(IContext ctx, GOAPNode parent, List<GOAPNode> leaves, List<ITask> openSubtasks)
        {
            var foundLeaf = false;

            for (var taskIndex = 0; taskIndex < openSubtasks.Count; taskIndex++)
            {
                var task = openSubtasks[taskIndex];

                if (task.IsValid(ctx) == false)
                {
                    continue;
                }

                if (task is IPrimitiveTask primitiveTask)
                {
                    // Due to branching permutations of state, and multiple possible solutions
                    // to the goal, where we want to end up with the shortest path, we need to
                    // reset the state stack for every task we operate on.
                    var oldStackDepth = ctx.GetWorldStateChangeDepth(ctx.Factory);

                    primitiveTask.ApplyEffects(ctx);

                    var node = ctx.Factory.Create<GOAPNode>();
                    {
                        node.Parent = parent;

                        if (primitiveTask is IGOAPTask goapTask)
                        {
                            node.RunningCost = parent.RunningCost + goapTask.Cost(ctx);
                        }
                        else
                        {
                            node.RunningCost = parent.RunningCost + 1f; // Default cost is 1 when task is not a GOAP Task.
                        }

                        node.Task = primitiveTask;
                    }

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
                        else
                        {
                            // If we failed to find a valid branch for this node,
                            // then it will no longer be referenced after this point.
                            // Otherwise its still used as a parent reference in the
                            // leaves list, and we can't return it to the factory yet.
                            ctx.Factory.Free(ref node);
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
                {
                    continue;
                }

                subset.Add(task);
            }

            return subset;
        }
    }

    
}
