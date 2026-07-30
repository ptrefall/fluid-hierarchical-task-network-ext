using System;
using System.Collections.Generic;
using FluidHTN.Conditions;
using FluidHTN.Operators;
using FluidHTN.PrimitiveTasks;

namespace FluidHTN.Compounds
{
    /// <summary>
    ///     The primitive task that represents a <see cref="ParallelTask"/> in a plan, and the operator that
    ///     drives its lanes. A plan is a flat queue of primitive tasks, so this is how a parallel task gets
    ///     to occupy a single slot in one: the planner ticks this like any other action, and it ticks every
    ///     open lane in turn.
    ///
    ///     <para>
    ///     A LANE is one independent thread of execution through a branch plan: the queue of tasks the
    ///     branch decomposed into, and the one currently in flight. Ticking a lane mirrors what the planner
    ///     does with the main plan — select the next task, validate its conditions, start its operator,
    ///     update it, apply its plan-and-execute effects when it completes — so a lane makes exactly the
    ///     same per-tick progress it would have made had its branch been the whole plan.
    ///     </para>
    ///
    ///     <para>
    ///     <b>Lanes survive a replan that keeps the parallel task.</b> The planner adopts the new plan
    ///     BEFORE stopping the outgoing task, so <see cref="Stop"/> can ask whether the new plan still
    ///     contains this runner. If it does, the lanes are left in flight and <see cref="Start"/> reconciles
    ///     them branch by branch the next time the parallel task is reached — which is what lets a long
    ///     durative branch survive the replan that opens a sibling. If the new plan has dropped the parallel
    ///     task, the lanes are stopped and closed.
    ///     </para>
    /// </summary>
    public class ParallelTaskRunner : IPrimitiveTask, IOperator
    {
        // ========================================================= LANE

        private sealed class Lane
        {
            /// <summary>
            ///     The authored branch index this lane executes. This is the lane's IDENTITY across a
            ///     replan: a re-decomposed parallel task reuses the open lane with the matching branch
            ///     index rather than restarting it.
            /// </summary>
            public int BranchIndex;

            /// <summary>The lane's own copy of the branch plan it walks.</summary>
            public readonly Queue<ITask> Plan = new Queue<ITask>();

            /// <summary>The task this lane is currently executing.</summary>
            public ITask CurrentTask;

            /// <summary>
            ///     True once this lane has finished or been abandoned. A closed lane is skipped when
            ///     ticking and is a candidate for reuse the next time lanes are opened.
            /// </summary>
            public bool IsClosed = true;
        }

        // ========================================================= FIELDS

        private readonly ParallelTask _parallel;
        private readonly List<Lane> _lanes = new List<Lane>();

        // ========================================================= CONSTRUCTION

        public ParallelTaskRunner(ParallelTask parallel)
        {
            _parallel = parallel ?? throw new ArgumentNullException(nameof(parallel));
        }

        // ========================================================= PROPERTIES

        public string Name { get; set; }
        public ICompoundTask Parent { get; set; }

        /// <summary>
        ///     Forwarded from the parallel task, so that the planner re-validates its authored conditions
        ///     when this is selected from the plan, exactly as it does for any other task in a plan.
        /// </summary>
        public List<ICondition> Conditions => _parallel.Conditions;

        public List<ICondition> ExecutingConditions { get; } = new List<ICondition>();
        public List<IEffect> Effects { get; } = new List<IEffect>();

        /// <summary>This task is its own operator.</summary>
        public IOperator Operator => this;

        /// <summary>The number of lanes currently open. Useful for debugging and tests.</summary>
        public int OpenLaneCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _lanes.Count; i++)
                {
                    if (_lanes[i].IsClosed == false)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        // ========================================================= ADDERS

        public ITask AddCondition(ICondition condition)
        {
            throw new Exception("Add conditions to the Parallel Task itself, not to its runner!");
        }

        public ITask AddExecutingCondition(ICondition condition)
        {
            ExecutingConditions.Add(condition);
            return this;
        }

        public ITask AddEffect(IEffect effect)
        {
            throw new Exception("A Parallel Task's effects are the effects of the tasks in its branches!");
        }

        public void SetOperator(IOperator action)
        {
            throw new Exception("A Parallel Task provides its own operator!");
        }

        // ========================================================= VALIDITY

        public bool IsValid(IContext ctx)
        {
            foreach (var condition in Conditions)
            {
                if (condition.IsValid(ctx) == false)
                {
                    return false;
                }
            }

            return true;
        }

        public DecompositionStatus OnIsValidFailed(IContext ctx)
        {
            return DecompositionStatus.Failed;
        }

        public void ApplyEffects(IContext ctx)
        {
            // A parallel task has no effects of its own. Its branches' tasks apply theirs, both during
            // decomposition and — for plan-and-execute effects — as each lane completes them.
        }

        // ========================================================= EXECUTION

        /// <summary>
        ///     Reconcile the open lanes against the parallel task's freshly decomposed branches, then tick
        ///     nothing yet — the planner calls <see cref="Update"/> on this same tick.
        /// </summary>
        public TaskStatus Start(IContext ctx)
        {
            OpenLanes(ctx);

            return OpenLaneCount == 0 ? TaskStatus.Success : TaskStatus.Continue;
        }

        /// <summary>
        ///     Drive every open lane one step. All branches are peers: one of them failing fails the whole
        ///     parallel task, exactly as a failed step fails a sequence. The task completes when every
        ///     branch has drained.
        /// </summary>
        public TaskStatus Update(IContext ctx)
        {
            TickOpenLanes(ctx, out var anyLaneFailed, out var anyLaneOpen);

            // All-or-nothing, like a sequence: one branch failing fails the parallel task. An author who
            // wants a branch that cannot do this wraps it in an AlwaysSucceedSelector.
            if (anyLaneFailed)
            {
                // The planner responds to a failure by aborting us, which tears every lane down.
                return TaskStatus.Failure;
            }

            if (anyLaneOpen)
            {
                return TaskStatus.Continue;
            }

            CloseAllLanes(ctx, abort: false);
            return TaskStatus.Success;
        }

        /// <summary>
        ///     Drive every open lane a single step, reporting whether any lane broke and whether any lane
        ///     is still in flight afterwards.
        /// </summary>
        private void TickOpenLanes(IContext ctx, out bool anyLaneFailed, out bool anyLaneOpen)
        {
            anyLaneFailed = false;
            anyLaneOpen = false;

            for (var i = 0; i < _lanes.Count; i++)
            {
                var lane = _lanes[i];
                if (lane.IsClosed)
                {
                    continue;
                }

                if (TickLane(ctx, lane) == false)
                {
                    anyLaneFailed = true;
                    continue;
                }

                if (lane.IsClosed == false)
                {
                    anyLaneOpen = true;
                }
            }
        }

        /// <summary>
        ///     Graceful end of execution. Called by the planner when a new plan replaces the running one.
        ///     If the new plan still contains us, the lanes are left in flight for <see cref="Start"/> to
        ///     reconcile; otherwise they are stopped and closed.
        /// </summary>
        public void Stop(IContext ctx)
        {
            // The planner enqueues the new plan before stopping the outgoing task, so this reads the plan
            // we are about to be judged against.
            var plan = ctx.PlannerState?.Plan;
            if (plan != null && plan.Contains(this))
            {
                return;
            }

            CloseAllLanes(ctx, abort: false);
        }

        /// <summary>Forced termination of execution. Always tears every lane down.</summary>
        public void Abort(IContext ctx)
        {
            CloseAllLanes(ctx, abort: true);
        }

        // ========================================================= LANE HANDLING

        /// <summary>
        ///     Reconcile the open lanes against the parallel task's freshly decomposed branches. A lane
        ///     whose branch is still planned AND whose in-flight task still passes its executing conditions
        ///     is KEPT AS IS — that is what lets a long durative branch survive the replan that opens a
        ///     sibling. Everything else is aborted and closed, and any branch without a lane gets a fresh one.
        /// </summary>
        private void OpenLanes(IContext ctx)
        {
            CloseStaleLanes(ctx);
            OpenMissingLanes();
        }

        /// <summary>
        ///     Abort and close any open lane whose branch is no longer planned, or whose in-flight task no
        ///     longer passes its executing conditions.
        /// </summary>
        private void CloseStaleLanes(IContext ctx)
        {
            for (var i = 0; i < _lanes.Count; i++)
            {
                var lane = _lanes[i];
                if (lane.IsClosed)
                {
                    continue;
                }

                var stillPlanned = ParallelPlansBranch(lane.BranchIndex);
                var taskStillValid = lane.CurrentTask == null || IsExecutingConditionsValid(ctx, lane.CurrentTask);

                if (stillPlanned == false || taskStillValid == false)
                {
                    CloseLane(ctx, lane, abort: true);
                }
            }
        }

        /// <summary>Open a fresh lane for every planned branch that does not already have one in flight.</summary>
        private void OpenMissingLanes()
        {
            for (var laneIndex = 0; laneIndex < _parallel.BranchCount; laneIndex++)
            {
                var branchIndex = _parallel.BranchIndexAt(laneIndex);
                if (FindOpenLane(branchIndex) != null)
                {
                    // Survives the replan untouched: keep its remaining plan and its in-flight task.
                    continue;
                }

                var lane = RentLane();
                lane.BranchIndex = branchIndex;
                CopyPlanInto(lane, _parallel.BranchPlanAt(laneIndex));
            }
        }

        /// <summary>
        ///     Tick one lane a single step, mirroring what the planner does with the main plan. Returns
        ///     false when the lane broke, which fails the whole parallel task.
        /// </summary>
        private bool TickLane(IContext ctx, Lane lane)
        {
            if (lane.CurrentTask == null)
            {
                var started = StartNextLaneTask(ctx, lane);
                if (started.HasValue)
                {
                    return started.Value;
                }
            }

            // The task might have completed on start (started == null means it is continuing), in which
            // case we update it this same tick — exactly as the planner does with the main plan.
            return UpdateLaneTask(ctx, lane);
        }

        /// <summary>
        ///     Pull the lane's next task off its plan, validate its conditions and start its operator.
        ///     Returns <c>true</c>/<c>false</c> when the tick is already decided (lane drained, task
        ///     completed on start, or a break), or <c>null</c> when the task started and is continuing —
        ///     in which case the caller falls through to the update phase on this same tick.
        /// </summary>
        private bool? StartNextLaneTask(IContext ctx, Lane lane)
        {
            if (lane.Plan.Count == 0)
            {
                CloseLane(ctx, lane, abort: false);
                return true;
            }

            lane.CurrentTask = lane.Plan.Dequeue();
            ctx.PlannerState.OnNewTask?.Invoke(lane.CurrentTask);

            if (LaneTaskConditionFailed(ctx, lane.CurrentTask))
            {
                return false;
            }

            // A plan only ever contains primitive tasks with operators. Anything else is a domain that
            // was not set up properly.
            if (!(lane.CurrentTask is IPrimitiveTask taskToStart) || taskToStart.Operator == null)
            {
                return false;
            }

            var startStatus = taskToStart.Operator.Start(ctx);

            if (startStatus == TaskStatus.Failure)
            {
                ctx.PlannerState.OnCurrentTaskFailed?.Invoke(taskToStart);
                return false;
            }

            // We have to first report that the operator has run its start function successfully,
            // before we report that the operator finished.
            ctx.PlannerState.OnCurrentTaskStarted?.Invoke(taskToStart);

            if (startStatus == TaskStatus.Success)
            {
                OnLaneTaskSucceeded(ctx, lane, taskToStart);
                return true;
            }

            return null;
        }

        /// <summary>Validate a task's preconditions, reporting the first failure. Returns true on failure.</summary>
        private static bool LaneTaskConditionFailed(IContext ctx, ITask task)
        {
            foreach (var condition in task.Conditions)
            {
                if (condition.IsValid(ctx) == false)
                {
                    ctx.PlannerState.OnNewTaskConditionFailed?.Invoke(task, condition);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Validate the lane's in-flight task against its executing conditions and drive its operator
        ///     one update. Returns false when the lane broke, which fails the whole parallel task.
        /// </summary>
        private bool UpdateLaneTask(IContext ctx, Lane lane)
        {
            if (!(lane.CurrentTask is IPrimitiveTask task))
            {
                return true;
            }

            foreach (var condition in task.ExecutingConditions)
            {
                if (condition.IsValid(ctx) == false)
                {
                    ctx.PlannerState.OnCurrentTaskExecutingConditionFailed?.Invoke(task, condition);
                    return false;
                }
            }

            var status = task.Operator.Update(ctx);

            if (status == TaskStatus.Success)
            {
                OnLaneTaskSucceeded(ctx, lane, task);
                return true;
            }

            if (status == TaskStatus.Failure)
            {
                ctx.PlannerState.OnCurrentTaskFailed?.Invoke(task);
                return false;
            }

            ctx.PlannerState.OnCurrentTaskContinues?.Invoke(task);
            return true;
        }

        /// <summary>
        ///     A lane's task completed. Apply its plan-and-execute effects and move the lane along, closing
        ///     it right away if that was its last task, so the parallel task can complete as soon as its
        ///     last branch is done rather than a tick later.
        /// </summary>
        private void OnLaneTaskSucceeded(IContext ctx, Lane lane, IPrimitiveTask task)
        {
            ctx.PlannerState.OnCurrentTaskCompletedSuccessfully?.Invoke(task);

            foreach (var effect in task.Effects)
            {
                if (effect.Type == EffectType.PlanAndExecute)
                {
                    ctx.PlannerState.OnApplyEffect?.Invoke(effect);
                    effect.Apply(ctx);
                }
            }

            lane.CurrentTask = null;

            if (lane.Plan.Count == 0)
            {
                CloseLane(ctx, lane, abort: false);
            }
        }

        private static bool IsExecutingConditionsValid(IContext ctx, ITask task)
        {
            if (!(task is IPrimitiveTask primitiveTask))
            {
                return true;
            }

            foreach (var condition in primitiveTask.ExecutingConditions)
            {
                if (condition.IsValid(ctx) == false)
                {
                    return false;
                }
            }

            return true;
        }

        private bool ParallelPlansBranch(int branchIndex)
        {
            for (var lane = 0; lane < _parallel.BranchCount; lane++)
            {
                if (_parallel.BranchIndexAt(lane) == branchIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private Lane FindOpenLane(int branchIndex)
        {
            for (var i = 0; i < _lanes.Count; i++)
            {
                if (_lanes[i].IsClosed == false && _lanes[i].BranchIndex == branchIndex)
                {
                    return _lanes[i];
                }
            }

            return null;
        }

        /// <summary>
        ///     Take a closed lane from the pool (or grow it by one). Lanes and their plan buffers are
        ///     reused, so opening a lane allocates nothing once the pool has warmed up.
        /// </summary>
        private Lane RentLane()
        {
            for (var i = 0; i < _lanes.Count; i++)
            {
                if (_lanes[i].IsClosed)
                {
                    var reused = _lanes[i];
                    reused.CurrentTask = null;
                    reused.Plan.Clear();
                    reused.IsClosed = false;
                    return reused;
                }
            }

            var lane = new Lane { IsClosed = false };
            _lanes.Add(lane);
            return lane;
        }

        /// <summary>
        ///     Give the lane its OWN copy of the branch plan. A parallel task reuses its branch buffers on
        ///     every decomposition, so a lane that aliased one would have its partially consumed plan reset
        ///     by the next replan — the copy is what makes lane survival mean anything.
        /// </summary>
        private static void CopyPlanInto(Lane lane, Queue<ITask> branchPlan)
        {
            lane.Plan.Clear();
            foreach (var task in branchPlan)
            {
                lane.Plan.Enqueue(task);
            }
        }

        private void CloseLane(IContext ctx, Lane lane, bool abort)
        {
            if (lane.IsClosed)
            {
                return;
            }

            if (lane.CurrentTask is IPrimitiveTask task)
            {
                if (abort)
                {
                    task.Abort(ctx);
                }
                else
                {
                    task.Stop(ctx);
                }
            }

            lane.CurrentTask = null;
            lane.Plan.Clear();
            lane.IsClosed = true;
        }

        private void CloseAllLanes(IContext ctx, bool abort)
        {
            for (var i = 0; i < _lanes.Count; i++)
            {
                CloseLane(ctx, _lanes[i], abort);
            }
        }
    }
}
