using System;
using System.Collections.Generic;
using FluidHTN.PrimitiveTasks;

namespace FluidHTN.Compounds
{
    /// <summary>
    ///     A task that decomposes exactly like a <see cref="Sequence"/> — every sub-task must be
    ///     decomposable, and a branch that fails fails the whole task — but whose branches then all run
    ///     CONCURRENTLY instead of one after the other. Each decomposed sub-task becomes its own execution
    ///     LANE with its own plan and its own task in flight, and every open lane is ticked each time the
    ///     planner ticks.
    ///
    ///     <para>
    ///     <b>Think of it as a sequence you tick all at once.</b> That is the whole difference. The
    ///     contracts an author already knows from a sequence carry over unchanged:
    ///     </para>
    ///     <list type="bullet">
    ///     <item>every sub-task must be valid and decomposable at plan time, or the parallel task fails;</item>
    ///     <item>one branch failing at execution fails the whole parallel task;</item>
    ///     <item>the parallel task completes when every branch has run to completion.</item>
    ///     </list>
    ///
    ///     <para>
    ///     <b>Optional branches are not this task's concern.</b> Wrap a branch in an
    ///     <see cref="AlwaysSucceedSelector"/> to make it optional; it reports a successful decomposition
    ///     whether or not its contents could be planned, contributing no lane when they could not. That
    ///     keeps optionality an isolated, reusable property of the branch rather than a special case baked
    ///     into this task.
    ///     </para>
    ///
    ///     <para>
    ///     <b>The one unavoidable divergence from a sequence: branches are decomposed INDEPENDENTLY.</b>
    ///     A sequence deliberately applies each step's effects so the NEXT step's conditions can validate
    ///     against them. That would be unsound here — concurrent branches start together, so a branch must
    ///     not be planned on the assumption that a sibling has already finished. The world state change
    ///     stack is therefore rolled back between branches, and every branch's effects are re-applied once
    ///     afterwards, so tasks sequenced AFTER the parallel task still validate against the joint outcome.
    ///     If a branch genuinely needs a sibling's result first, it belongs in a sequence, not a parallel.
    ///     </para>
    ///
    ///     <para>
    ///     <b>Partial plans are not supported inside a branch.</b> A paused remainder has no lane to resume
    ///     into, so a branch that produces a partial plan fails the decomposition.
    ///     </para>
    ///
    ///     <para>
    ///     Nothing stops two branches from writing the same world state index. Concurrent lanes racing for
    ///     one fact is the author's business — it may well be intentional — so it is not treated as an error.
    ///     </para>
    /// </summary>
    public class ParallelTask : Sequence
    {
        // ========================================================= FIELDS

        /// <summary>
        ///     Reusable per-lane plan buffers. Grown on demand and reused across decompositions. Indexed by
        ///     LANE, not by authored sub-task index — <see cref="_branchIndices"/> maps one to the other.
        /// </summary>
        private readonly List<Queue<ITask>> _branchPlans = new List<Queue<ITask>>();

        /// <summary>The authored sub-task index behind each lane, in lane order.</summary>
        private readonly List<int> _branchIndices = new List<int>();

        private readonly ParallelTaskRunner _runner;

        // ========================================================= CONSTRUCTION

        public ParallelTask()
        {
            _runner = new ParallelTaskRunner(this);
        }

        // ========================================================= LANE ACCESS

        /// <summary>The number of lanes the most recent decomposition produced.</summary>
        public int BranchCount => _branchIndices.Count;

        /// <summary>The decomposed plan the given lane executes.</summary>
        public Queue<ITask> BranchPlanAt(int lane) => _branchPlans[lane];

        /// <summary>The authored sub-task index behind the given lane (its identity across replans).</summary>
        public int BranchIndexAt(int lane) => _branchIndices[lane];

        /// <summary>The authored branch task behind the given lane.</summary>
        public ITask BranchTaskAt(int lane) => Subtasks[_branchIndices[lane]];

        /// <summary>The primitive task that represents this parallel task in a plan, and drives its lanes.</summary>
        public ParallelTaskRunner Runner => _runner;

        // ========================================================= DECOMPOSITION

        /// <summary>
        ///     Decomposes every sub-task — all-or-nothing, like a sequence — into its own lane plan, rolling
        ///     the world state change stack back between them so they stay independent, then pushes its
        ///     runner onto the plan as a single element. The runner opens the lanes at execution time.
        /// </summary>
        protected override DecompositionStatus OnDecompose(IContext ctx, int startIndex, out Queue<ITask> result)
        {
            _branchIndices.Clear();

            var oldStackDepth = ctx.GetWorldStateChangeDepth(ctx.Factory);

            // NOTE: this loop takes no part in the Method Traversal Record, exactly like a Sequence. The
            // record is a path of branch CHOICES, and a parallel task — like a sequence — makes no choice:
            // it takes every branch. Selectors inside a branch still record their own choices as normal.
            for (var taskIndex = startIndex; taskIndex < Subtasks.Count; taskIndex++)
            {
                var task = Subtasks[taskIndex];

                if (ctx.LogDecomposition)
                {
                    Log(ctx, $"ParallelTask.OnDecompose:Branch index: {taskIndex}: {task?.Name}");
                }

                // The inherited Plan buffer is our per-branch scratch. Each branch is decomposed into it,
                // then copied off into that lane's own buffer.
                Plan.Clear();

                var branchDepth = ctx.GetWorldStateChangeDepth(ctx.Factory);
                var status = OnDecomposeTask(ctx, task, taskIndex, branchDepth, out _);
                ctx.Factory.FreeArray(ref branchDepth);

                switch (status)
                {
                    case DecompositionStatus.Rejected:
                    {
                        // A rejection cancels the entire planning procedure, exactly as elsewhere.
                        Abandon(ctx, ref oldStackDepth);
                        result = null;
                        return DecompositionStatus.Rejected;
                    }

                    case DecompositionStatus.Partial:
                    {
                        // A paused remainder has no lane to resume into. Move the pause outside the
                        // parallel task.
                        if (ctx.LogDecomposition)
                        {
                            Log(ctx,
                                $"ParallelTask.OnDecompose:Failed:Branch {task?.Name} produced a partial plan, which is not supported inside a parallel branch!",
                                ConsoleColor.Red);
                        }

                        ctx.HasPausedPartialPlan = false;
                        ctx.PartialPlanQueue.Clear();

                        Abandon(ctx, ref oldStackDepth);
                        result = Plan;
                        return DecompositionStatus.Failed;
                    }

                    case DecompositionStatus.Failed:
                    {
                        // All-or-nothing, exactly like a sequence: a branch that cannot be taken fails the
                        // task. Wrap a branch in an AlwaysSucceedSelector to make it optional.
                        Abandon(ctx, ref oldStackDepth);
                        result = Plan;
                        return DecompositionStatus.Failed;
                    }
                }

                // Succeeded. An EMPTY contribution is legal and means "no lane for this branch" — that is
                // how an AlwaysSucceedSelector reports optional work that could not be planned this time.
                if (Plan.Count > 0)
                {
                    CopyInto(RequireBranchPlan(_branchIndices.Count), Plan);
                    _branchIndices.Add(taskIndex);
                }

                // INDEPENDENCE: roll this branch's effects off the stack before decomposing the next one,
                // so concurrent branches never validate against each other.
                ctx.TrimToStackDepth(oldStackDepth);
            }

            ctx.Factory.FreeArray(ref oldStackDepth);

            // Now that the branches are all decomposed independently, apply their effects together, so a
            // task sequenced AFTER this parallel task validates against the joint result.
            ApplyJointOutcome(ctx);

            Plan.Clear();

            if (_branchIndices.Count == 0)
            {
                if (ctx.LogDecomposition)
                {
                    Log(ctx, $"ParallelTask.OnDecompose:Failed:No branches produced a lane!", ConsoleColor.Red);
                }

                result = Plan;
                return DecompositionStatus.Failed;
            }

            // The parallel task occupies a SINGLE slot in the linear plan; its lanes hang off its runner.
            _runner.Name = Name;
            _runner.Parent = this;
            Plan.Enqueue(_runner);

            if (ctx.LogDecomposition)
            {
                Log(ctx, $"ParallelTask.OnDecompose:Succeeded:{_branchIndices.Count} lanes!", ConsoleColor.Green);
            }

            result = Plan;
            return DecompositionStatus.Succeeded;
        }

        /// <summary>
        ///     Re-apply every lane's effects, in lane order, now that the branches have been decomposed
        ///     independently of each other. This is what makes the joint outcome visible to whatever is
        ///     sequenced after the parallel task.
        /// </summary>
        private void ApplyJointOutcome(IContext ctx)
        {
            for (var lane = 0; lane < _branchIndices.Count; lane++)
            {
                foreach (var task in _branchPlans[lane])
                {
                    if (task is IPrimitiveTask primitiveTask)
                    {
                        primitiveTask.ApplyEffects(ctx);
                    }
                }
            }
        }

        /// <summary>Roll back everything this decomposition attempt did, leaving no lanes.</summary>
        private void Abandon(IContext ctx, ref int[] oldStackDepth)
        {
            _branchIndices.Clear();
            Plan.Clear();
            ctx.TrimToStackDepth(oldStackDepth);
            ctx.Factory.FreeArray(ref oldStackDepth);
        }

        /// <summary>Get (growing the pool if needed) the reusable plan buffer for the given lane.</summary>
        private Queue<ITask> RequireBranchPlan(int lane)
        {
            while (_branchPlans.Count <= lane)
            {
                _branchPlans.Add(new Queue<ITask>());
            }

            return _branchPlans[lane];
        }

        private static void CopyInto(Queue<ITask> dest, Queue<ITask> src)
        {
            dest.Clear();
            foreach (var task in src)
            {
                dest.Enqueue(task);
            }
        }
    }
}
