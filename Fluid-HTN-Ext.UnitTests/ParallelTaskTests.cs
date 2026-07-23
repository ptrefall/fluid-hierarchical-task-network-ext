using System.Collections.Generic;
using FluidHTN;
using FluidHTN.Compounds;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fluid_HTN_Ext.UnitTests
{
    [TestClass]
    public class ParallelTaskTests
    {
        // ========================================================= HELPERS

        private static ParallelTask FindParallel(ITask task)
        {
            if (task is ParallelTask parallel)
            {
                return parallel;
            }

            if (task is ICompoundTask compound)
            {
                foreach (var subtask in compound.Subtasks)
                {
                    var found = FindParallel(subtask);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        // ========================================================= EXECUTION

        /// <summary>
        /// The whole point of a parallel task: every branch makes progress on the same tick, rather
        /// than one branch running to completion before the next one starts.
        /// </summary>
        [TestMethod]
        public void AllBranches_AdvanceWithinASingleTick_Ok()
        {
            var aTicks = 0;
            var bTicks = 0;

            var domain = new DomainBuilder<MyContext>("test")
                .Parallel<DomainBuilder<MyContext>, MyContext>("par")
                    .Action("a")
                        .Do(context => { aTicks++; return aTicks >= 2 ? TaskStatus.Success : TaskStatus.Continue; })
                    .End()
                    .Action("b")
                        .Do(context => { bTicks++; return bTicks >= 2 ? TaskStatus.Success : TaskStatus.Continue; })
                    .End()
                .End()
                .Build();

            var ctx = new MyContext();
            ctx.Init();
            var planner = new Planner<MyContext>();

            planner.Tick(domain, ctx, allowImmediateReplanAndExecute: false);
            Assert.AreEqual(1, aTicks);
            Assert.AreEqual(1, bTicks);

            planner.Tick(domain, ctx, allowImmediateReplanAndExecute: false);
            Assert.AreEqual(2, aTicks);
            Assert.AreEqual(2, bTicks);

            // Both branches drained on the same tick, so the parallel task is done.
            Assert.IsNull(ctx.PlannerState.CurrentTask);
            Assert.AreEqual(0, ctx.PlannerState.Plan.Count);
        }

        /// <summary>
        /// A parallel task completes when every branch has run to completion, no matter how uneven
        /// the branches are.
        /// </summary>
        [TestMethod]
        public void CompletesWhenEveryBranchHasDrained_Ok()
        {
            var log = new List<string>();

            var domain = new DomainBuilder<MyContext>("test")
                .Parallel<DomainBuilder<MyContext>, MyContext>("par")
                    .Action("a1")
                        .Do(context => { log.Add("a1"); return TaskStatus.Success; })
                    .End()
                    .Sequence("long branch")
                        .Action("b1")
                            .Do(context => { log.Add("b1"); return TaskStatus.Success; })
                        .End()
                        .Action("b2")
                            .Do(context => { log.Add("b2"); return TaskStatus.Success; })
                        .End()
                        .Action("b3")
                            .Do(context => { log.Add("b3"); return TaskStatus.Success; })
                        .End()
                    .End()
                .End()
                .Build();

            var ctx = new MyContext();
            ctx.Init();
            var planner = new Planner<MyContext>();

            planner.Tick(domain, ctx, allowImmediateReplanAndExecute: false);
            CollectionAssert.AreEqual(new[] { "a1", "b1" }, log);
            Assert.IsNotNull(ctx.PlannerState.CurrentTask);

            planner.Tick(domain, ctx, allowImmediateReplanAndExecute: false);
            planner.Tick(domain, ctx, allowImmediateReplanAndExecute: false);

            CollectionAssert.AreEqual(new[] { "a1", "b1", "b2", "b3" }, log);
            Assert.IsNull(ctx.PlannerState.CurrentTask);
            Assert.AreEqual(0, ctx.PlannerState.Plan.Count);
        }

        /// <summary>
        /// All-or-nothing at execution, just like a sequence: one branch failing fails the whole plan.
        /// </summary>
        [TestMethod]
        public void OneBranchFailingAtRuntime_FailsTheWholePlan_Ok()
        {
            var aTicks = 0;

            var domain = new DomainBuilder<MyContext>("test")
                .Parallel<DomainBuilder<MyContext>, MyContext>("par")
                    .Action("never ends")
                        .Do(context => { aTicks++; return TaskStatus.Continue; })
                    .End()
                    .Action("fails")
                        .Do(context => TaskStatus.Failure)
                    .End()
                .End()
                .Build();

            var ctx = new MyContext();
            ctx.Init();
            var planner = new Planner<MyContext>();

            planner.Tick(domain, ctx, allowImmediateReplanAndExecute: false);

            Assert.AreEqual(TaskStatus.Failure, ctx.PlannerState.LastStatus);
            Assert.IsNull(ctx.PlannerState.CurrentTask);
            Assert.AreEqual(0, ctx.PlannerState.Plan.Count);

            var parallel = FindParallel(domain.Root);
            Assert.AreEqual(0, parallel.Runner.OpenLaneCount);
            Assert.AreEqual(1, aTicks);
        }

        /// <summary>
        /// A parallel task in the middle of a sequence is still a single step of that sequence: it
        /// runs after the step before it, and the step after it waits for every branch.
        /// </summary>
        [TestMethod]
        public void ParallelMidSequence_RunsAfterTheStepBeforeAndBeforeTheStepAfter_Ok()
        {
            var log = new List<string>();

            var domain = new DomainBuilder<MyContext>("test")
                .Sequence("chores")
                    .Action("before")
                        .Do(context => { log.Add("before"); return TaskStatus.Success; })
                    .End()
                    .Parallel<DomainBuilder<MyContext>, MyContext>("par")
                        .Action("a")
                            .Do(context => { log.Add("a"); return TaskStatus.Success; })
                        .End()
                        .Action("b")
                            .Do(context => { log.Add("b"); return TaskStatus.Success; })
                        .End()
                    .End()
                    .Action("after")
                        .Do(context => { log.Add("after"); return TaskStatus.Success; })
                    .End()
                .End()
                .Build();

            var ctx = new MyContext();
            ctx.Init();
            var planner = new Planner<MyContext>();

            planner.Tick(domain, ctx, allowImmediateReplanAndExecute: false);
            planner.Tick(domain, ctx, allowImmediateReplanAndExecute: false);
            planner.Tick(domain, ctx, allowImmediateReplanAndExecute: false);

            CollectionAssert.AreEqual(new[] { "before", "a", "b", "after" }, log);
        }

        /// <summary>
        /// An inner parallel task is just another primitive task in its lane's plan, so nesting works
        /// without any special handling.
        /// </summary>
        [TestMethod]
        public void NestedParallel_RunsItsInnerLanes_Ok()
        {
            var log = new List<string>();

            var domain = new DomainBuilder<MyContext>("test")
                .Parallel<DomainBuilder<MyContext>, MyContext>("outer")
                    .Action("x")
                        .Do(context => { log.Add("x"); return TaskStatus.Success; })
                    .End()
                    .Parallel<DomainBuilder<MyContext>, MyContext>("inner")
                        .Action("y")
                            .Do(context => { log.Add("y"); return TaskStatus.Success; })
                        .End()
                        .Action("z")
                            .Do(context => { log.Add("z"); return TaskStatus.Success; })
                        .End()
                    .End()
                .End()
                .Build();

            var ctx = new MyContext();
            ctx.Init();
            var planner = new Planner<MyContext>();

            planner.Tick(domain, ctx, allowImmediateReplanAndExecute: false);

            CollectionAssert.AreEquivalent(new[] { "x", "y", "z" }, log);
            Assert.IsNull(ctx.PlannerState.CurrentTask);
        }

        // ========================================================= DECOMPOSITION

        /// <summary>
        /// All-or-nothing at plan time, just like a sequence: a branch that cannot be planned fails
        /// the whole parallel task.
        /// </summary>
        [TestMethod]
        public void ABranchThatCannotBePlanned_FailsTheWholeParallel_Ok()
        {
            var domain = new DomainBuilder<MyContext>("test")
                .Parallel<DomainBuilder<MyContext>, MyContext>("par")
                    .Action("plannable")
                        .Do(context => TaskStatus.Success)
                    .End()
                    .Action("not plannable")
                        .Condition("has A", context => context.HasState(MyWorldState.HasA))
                        .Do(context => TaskStatus.Success)
                    .End()
                .End()
                .Build();

            var ctx = new MyContext();
            ctx.Init();

            var status = domain.FindPlan(ctx, out var plan);

            // A failed decomposition with an empty traversal record is reported as Rejected by
            // Domain.FindPlan, so what matters here is that no plan came out of it.
            Assert.AreNotEqual(DecompositionStatus.Succeeded, status);
            Assert.IsTrue(plan == null || plan.Count == 0);
        }

        /// <summary>
        /// Optional branches are not the parallel task's concern: an Always Succeed Selector reports a
        /// successful decomposition whether or not its contents could be planned, contributing no lane
        /// when they could not.
        /// </summary>
        [TestMethod]
        public void AnAlwaysSucceedBranch_MakesAnUnplannableBranchOptional_Ok()
        {
            var log = new List<string>();

            var domain = new DomainBuilder<MyContext>("test")
                .Parallel<DomainBuilder<MyContext>, MyContext>("par")
                    .Action("required")
                        .Do(context => { log.Add("required"); return TaskStatus.Success; })
                    .End()
                    .AlwaysSucceedSelect<DomainBuilder<MyContext>, MyContext>("optional")
                        .Action("optional")
                            .Condition("has A", context => context.HasState(MyWorldState.HasA))
                            .Do(context => { log.Add("optional"); return TaskStatus.Success; })
                        .End()
                    .End()
                .End()
                .Build();

            var ctx = new MyContext();
            ctx.Init();

            var status = domain.FindPlan(ctx, out var plan);

            Assert.AreEqual(DecompositionStatus.Succeeded, status);
            Assert.AreEqual(1, plan.Count);

            var parallel = FindParallel(domain.Root);
            Assert.AreEqual(1, parallel.BranchCount);
            Assert.AreEqual(0, parallel.BranchIndexAt(0));

            var planner = new Planner<MyContext>();
            planner.Tick(domain, ctx, allowImmediateReplanAndExecute: false);

            CollectionAssert.AreEqual(new[] { "required" }, log);
        }

        /// <summary>
        /// The same optional branch contributes a lane as normal once its contents can be planned.
        /// </summary>
        [TestMethod]
        public void AnAlwaysSucceedBranch_RunsNormallyWhenItsContentsArePlannable_Ok()
        {
            var log = new List<string>();

            var domain = new DomainBuilder<MyContext>("test")
                .Parallel<DomainBuilder<MyContext>, MyContext>("par")
                    .Action("required")
                        .Do(context => { log.Add("required"); return TaskStatus.Success; })
                    .End()
                    .AlwaysSucceedSelect<DomainBuilder<MyContext>, MyContext>("optional")
                        .Action("optional")
                            .Condition("has A", context => context.HasState(MyWorldState.HasA))
                            .Do(context => { log.Add("optional"); return TaskStatus.Success; })
                        .End()
                    .End()
                .End()
                .Build();

            var ctx = new MyContext();
            ctx.Init();
            ctx.SetState(MyWorldState.HasA, true, EffectType.Permanent);

            var status = domain.FindPlan(ctx, out _);
            Assert.AreEqual(DecompositionStatus.Succeeded, status);

            var parallel = FindParallel(domain.Root);
            Assert.AreEqual(2, parallel.BranchCount);

            var planner = new Planner<MyContext>();
            planner.Tick(domain, ctx, allowImmediateReplanAndExecute: false);

            CollectionAssert.AreEquivalent(new[] { "required", "optional" }, log);
        }

        /// <summary>
        /// The one unavoidable divergence from a sequence. Concurrent branches start together, so a
        /// branch must not be planned on the assumption that a sibling has already finished. The same
        /// hierarchy authored as a sequence fails, because there the prediction is exactly the point.
        /// </summary>
        [TestMethod]
        public void Branches_DoNotSeeEachOthersPlanOnlyPredictions_Ok()
        {
            var parallelDomain = new DomainBuilder<MyContext>("test")
                .Parallel<DomainBuilder<MyContext>, MyContext>("par")
                    .Action("sets A")
                        .Effect("set A", EffectType.PlanOnly, (context, type) => context.SetState(MyWorldState.HasA, true, type))
                        .Do(context => TaskStatus.Success)
                    .End()
                    .Action("requires no A")
                        .Condition("has not A", context => !context.HasState(MyWorldState.HasA))
                        .Do(context => TaskStatus.Success)
                    .End()
                .End()
                .Build();

            var sequenceDomain = new DomainBuilder<MyContext>("test")
                .Sequence("seq")
                    .Action("sets A")
                        .Effect("set A", EffectType.PlanOnly, (context, type) => context.SetState(MyWorldState.HasA, true, type))
                        .Do(context => TaskStatus.Success)
                    .End()
                    .Action("requires no A")
                        .Condition("has not A", context => !context.HasState(MyWorldState.HasA))
                        .Do(context => TaskStatus.Success)
                    .End()
                .End()
                .Build();

            var parallelCtx = new MyContext();
            parallelCtx.Init();
            var parallelStatus = parallelDomain.FindPlan(parallelCtx, out _);

            var sequenceCtx = new MyContext();
            sequenceCtx.Init();
            var sequenceStatus = sequenceDomain.FindPlan(sequenceCtx, out _);

            Assert.AreEqual(DecompositionStatus.Succeeded, parallelStatus);
            Assert.AreEqual(2, FindParallel(parallelDomain.Root).BranchCount);

            Assert.AreNotEqual(DecompositionStatus.Succeeded, sequenceStatus);
        }

        /// <summary>
        /// Branches are decomposed independently of each other, but their predicted outcomes are
        /// applied together afterwards, so whatever is sequenced after the parallel task validates
        /// against the joint result.
        /// </summary>
        [TestMethod]
        public void TheJointOutcome_IsVisibleToATaskSequencedAfterTheParallel_Ok()
        {
            var domain = new DomainBuilder<MyContext>("test")
                .Sequence("seq")
                    .Parallel<DomainBuilder<MyContext>, MyContext>("par")
                        .Action("sets A")
                            .Effect("set A", EffectType.PlanOnly, (context, type) => context.SetState(MyWorldState.HasA, true, type))
                            .Do(context => TaskStatus.Success)
                        .End()
                        .Action("sets B")
                            .Effect("set B", EffectType.PlanOnly, (context, type) => context.SetState(MyWorldState.HasB, true, type))
                            .Do(context => TaskStatus.Success)
                        .End()
                    .End()
                    .Action("requires A and B")
                        .Condition("has A", context => context.HasState(MyWorldState.HasA))
                        .Condition("has B", context => context.HasState(MyWorldState.HasB))
                        .Do(context => TaskStatus.Success)
                    .End()
                .End()
                .Build();

            var ctx = new MyContext();
            ctx.Init();

            var status = domain.FindPlan(ctx, out var plan);

            Assert.AreEqual(DecompositionStatus.Succeeded, status);
            Assert.AreEqual(2, plan.Count);
            Assert.AreEqual("par", plan.Dequeue().Name);
            Assert.AreEqual("requires A and B", plan.Dequeue().Name);
        }

        /// <summary>
        /// Two branches writing the same world state index is the author's business - it may well be
        /// intentional - so it is not treated as an error.
        /// </summary>
        [TestMethod]
        public void BranchesWritingTheSameStateIndex_AreAllowed_Ok()
        {
            var domain = new DomainBuilder<MyContext>("test")
                .Parallel<DomainBuilder<MyContext>, MyContext>("par")
                    .Action("sets A to 1")
                        .Effect("set A", EffectType.PlanAndExecute, (context, type) => context.SetState(MyWorldState.HasA, 1, type))
                        .Do(context => TaskStatus.Success)
                    .End()
                    .Action("sets A to 2")
                        .Effect("set A", EffectType.PlanAndExecute, (context, type) => context.SetState(MyWorldState.HasA, 2, type))
                        .Do(context => TaskStatus.Success)
                    .End()
                .End()
                .Build();

            var ctx = new MyContext();
            ctx.Init();

            var status = domain.FindPlan(ctx, out _);

            Assert.AreEqual(DecompositionStatus.Succeeded, status);
            Assert.AreEqual(2, FindParallel(domain.Root).BranchCount);
        }

        /// <summary>
        /// A paused remainder has no lane to resume into, so a branch that produces a partial plan
        /// fails the decomposition rather than leaving the planner holding a partial plan it cannot
        /// continue.
        /// </summary>
        [TestMethod]
        public void PausePlanInsideABranch_FailsDecomposition_Ok()
        {
            var domain = new DomainBuilder<MyContext>("test")
                .Parallel<DomainBuilder<MyContext>, MyContext>("par")
                    .Action("a")
                        .Do(context => TaskStatus.Success)
                    .End()
                    .Sequence("paused branch")
                        .Action("b1")
                            .Do(context => TaskStatus.Success)
                        .End()
                        .PausePlan()
                        .Action("b2")
                            .Do(context => TaskStatus.Success)
                        .End()
                    .End()
                .End()
                .Build();

            var ctx = new MyContext();
            ctx.Init();

            var status = domain.FindPlan(ctx, out _);

            Assert.AreNotEqual(DecompositionStatus.Succeeded, status);
            Assert.IsFalse(ctx.HasPausedPartialPlan);
            Assert.AreEqual(0, ctx.PartialPlanQueue.Count);
        }

        /// <summary>
        /// A parallel task records no branch choice of its own, exactly like a sequence: it takes every
        /// branch, so its branch count must not sway the traversal record that plan priority is
        /// compared with.
        /// </summary>
        [TestMethod]
        public void MethodTraversalRecord_IsUnaffectedByBranchCount_Ok()
        {
            var oneBranch = new DomainBuilder<MyContext>("test")
                .Parallel<DomainBuilder<MyContext>, MyContext>("par")
                    .Action("a")
                        .Do(context => TaskStatus.Success)
                    .End()
                .End()
                .Build();

            var threeBranches = new DomainBuilder<MyContext>("test")
                .Parallel<DomainBuilder<MyContext>, MyContext>("par")
                    .Action("a")
                        .Do(context => TaskStatus.Success)
                    .End()
                    .Action("b")
                        .Do(context => TaskStatus.Success)
                    .End()
                    .Action("c")
                        .Do(context => TaskStatus.Success)
                    .End()
                .End()
                .Build();

            var oneCtx = new MyContext();
            oneCtx.Init();
            Assert.AreEqual(DecompositionStatus.Succeeded, oneBranch.FindPlan(oneCtx, out _));

            var threeCtx = new MyContext();
            threeCtx.Init();
            Assert.AreEqual(DecompositionStatus.Succeeded, threeBranches.FindPlan(threeCtx, out _));

            CollectionAssert.AreEqual(oneCtx.MethodTraversalRecord, threeCtx.MethodTraversalRecord);
        }

        // ========================================================= REPLANNING

        /// <summary>
        /// A replan that still contains the parallel task must leave its lanes in flight. This is what
        /// lets a long durative branch survive the replan that opens a sibling: the running branch is
        /// never restarted, it just keeps going while the new lane opens beside it.
        /// </summary>
        [TestMethod]
        public void ReplanKeepingTheParallel_LeavesRunningLanesInFlight_Ok()
        {
            var longStarts = 0;
            var longUpdates = 0;
            var optionalRan = 0;

            var domain = new DomainBuilder<MyContext>("test")
                .Parallel<DomainBuilder<MyContext>, MyContext>("par")
                    .Action("long")
                        .Do(context => { longUpdates++; return TaskStatus.Continue; },
                            context => { longStarts++; return TaskStatus.Continue; })
                    .End()
                    .AlwaysSucceedSelect<DomainBuilder<MyContext>, MyContext>("optional")
                        .Action("optional")
                            .Condition("has B", context => context.HasState(MyWorldState.HasB))
                            .Do(context => { optionalRan++; return TaskStatus.Success; })
                        .End()
                    .End()
                .End()
                .Build();

            var ctx = new MyContext();
            ctx.Init();
            var planner = new Planner<MyContext>();
            var parallel = FindParallel(domain.Root);

            planner.Tick(domain, ctx, allowImmediateReplanAndExecute: false);
            planner.Tick(domain, ctx, allowImmediateReplanAndExecute: false);

            Assert.AreEqual(1, longStarts);
            Assert.AreEqual(2, longUpdates);
            Assert.AreEqual(1, parallel.Runner.OpenLaneCount);
            Assert.AreEqual(0, optionalRan);

            // The optional branch becomes plannable, which dirties the context and yields a new plan
            // that still contains the parallel task.
            ctx.SetState(MyWorldState.HasB, true, EffectType.Permanent);
            Assert.IsTrue(ctx.IsDirty);

            planner.Tick(domain, ctx, allowImmediateReplanAndExecute: false);

            Assert.AreEqual(2, parallel.BranchCount);
            Assert.AreEqual(1, optionalRan);

            // The running branch was never restarted - it just kept ticking alongside the new lane.
            Assert.AreEqual(1, longStarts);
            Assert.AreEqual(3, longUpdates);
        }

        /// <summary>
        /// A replan that has dropped the parallel task tears its lanes down, so nothing is left
        /// running in the background of a plan that no longer contains it.
        /// </summary>
        [TestMethod]
        public void ReplanDroppingTheParallel_StopsLaneTasks_Ok()
        {
            var longStops = 0;
            var urgentRan = 0;

            var domain = new DomainBuilder<MyContext>("test")
                .Select("root")
                    .Sequence("urgent")
                        .Condition("has C", context => context.HasState(MyWorldState.HasC))
                        .Action("urgent")
                            .Do(context => { urgentRan++; return TaskStatus.Success; })
                        .End()
                    .End()
                    .Parallel<DomainBuilder<MyContext>, MyContext>("par")
                        .Action("long")
                            .Do(context => TaskStatus.Continue, null, context => longStops++)
                        .End()
                        .Action("long 2")
                            .Do(context => TaskStatus.Continue)
                        .End()
                    .End()
                .End()
                .Build();

            var ctx = new MyContext();
            ctx.Init();
            var planner = new Planner<MyContext>();
            var parallel = FindParallel(domain.Root);

            planner.Tick(domain, ctx, allowImmediateReplanAndExecute: false);
            Assert.AreEqual(2, parallel.Runner.OpenLaneCount);

            // The urgent branch becomes valid, and beats the parallel task in the root selector.
            ctx.SetState(MyWorldState.HasC, true, EffectType.Permanent);
            planner.Tick(domain, ctx, allowImmediateReplanAndExecute: false);

            Assert.AreEqual(1, longStops);
            Assert.AreEqual(0, parallel.Runner.OpenLaneCount);

            // The same tick adopted the new plan and ran it, so the parallel task is well and truly gone.
            Assert.AreEqual(1, urgentRan);
        }
    }
}
