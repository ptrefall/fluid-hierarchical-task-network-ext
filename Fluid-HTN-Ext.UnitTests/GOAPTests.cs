using System;
using System.Collections.Generic;
using FluidHTN;
using FluidHTN.Compounds;
using FluidHTN.PrimitiveTasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fluid_HTN_Ext.UnitTests
{
    [TestClass]
    public class GOAPTests
    {
        /// <summary>
        /// We test whether the GOAP sequence is able to find a path from Get A to Get C (via Get B)
        /// while respecting the preconditions applied to each GOAP Action.
        /// We expect Get A -> Get B -> Get C no matter the order in which the sub-tasks we declared in.
        /// </summary>
        [TestMethod]
        public void PreconditionsAffectTaskOrder_ExpectedBehavior()
        {
            var domain = new DomainBuilder<MyContext>("test")
                .GOAPSequence<DomainBuilder<MyContext>, MyContext>("goap sequence",
                    new KeyValuePair<byte, byte>((byte)MyWorldState.HasC, 1))
                    .GOAPAction<DomainBuilder<MyContext>, MyContext, GOAPTaskAction>("Get C")
                        .Condition("Has B", context => context.HasState(MyWorldState.HasB))
                        .Condition("Has not C", context => !context.HasState(MyWorldState.HasC))
                        .Do(context =>
                        {
                            context.SetState(MyWorldState.HasC, true, EffectType.Permanent);
                            return TaskStatus.Success;
                        })
                        .Effect("Has C", EffectType.PlanOnly, (context, type) => context.SetState(MyWorldState.HasC, true, type))
                    .End()
                    .GOAPAction<DomainBuilder<MyContext>, MyContext, GOAPTaskAction>("Get A")
                        .Condition("Has not A", context => !context.HasState(MyWorldState.HasA))
                        .Do(context =>
                        {
                            context.SetState(MyWorldState.HasA, true, EffectType.Permanent);
                            return TaskStatus.Success;
                        })
                        .Effect("Has A", EffectType.PlanOnly, (context, type) => context.SetState(MyWorldState.HasA, true, type))
                    .End()
                    .GOAPAction<DomainBuilder<MyContext>, MyContext, GOAPTaskAction>("Get B")
                        .Condition("Has A", context => context.HasState(MyWorldState.HasA))
                        .Condition("Has not B", context => !context.HasState(MyWorldState.HasB))
                        .Do(context =>
                        {
                            context.SetState(MyWorldState.HasB, true, EffectType.Permanent);
                            return TaskStatus.Success;
                        })
                        .Effect("Has B", EffectType.PlanOnly, (context, type) => context.SetState(MyWorldState.HasB, true, type))
                    .End()
                .End()
                .Build();

            var ctx = new MyContext();
            ctx.Init();

            var status = domain.FindPlan(ctx, out var plan);
            Assert.IsTrue(status == DecompositionStatus.Succeeded);
            Assert.IsTrue(plan != null);
            Assert.IsTrue(plan.Count == 3);
            Assert.IsTrue(plan.Peek().Name == "Get A");
            plan.Dequeue();
            Assert.IsTrue(plan.Peek().Name == "Get B");
            plan.Dequeue();
            Assert.IsTrue(plan.Peek().Name == "Get C");
        }

        /// <summary>
        /// We test whether cost is taken into account when choosing the shortest path to "Get C".
        /// Get B has a higher cost than Get A, thus we expect Get A to be picked first, then Get C, to fulfill the goal of Get C.
        /// </summary>
        [TestMethod]
        public void PreferLowCostTasks_ExpectedBehavior()
        {
            var domain = new DomainBuilder<MyContext>("test")
                .GOAPSequence<DomainBuilder<MyContext>, MyContext>("goap sequence",
                    new KeyValuePair<byte, byte>((byte) MyWorldState.HasC, 1))
                    .GOAPAction<DomainBuilder<MyContext>, MyContext, GOAPTaskAction>("Get C")
                        .Condition("Has A or B", context => context.HasState(MyWorldState.HasA) || context.HasState(MyWorldState.HasB))
                        .Condition("Has not C", context => !context.HasState(MyWorldState.HasC))
                        .Do(context =>
                        {
                            context.SetState(MyWorldState.HasC, true, EffectType.Permanent);
                            return TaskStatus.Success;
                        })
                        .Effect("Has C", EffectType.PlanOnly, (context, type) => context.SetState(MyWorldState.HasC, true, type))
                    .End()
                    .GOAPAction<DomainBuilder<MyContext>, MyContext, GOAPTaskActionHighCost>("Get B")
                        .Condition("Has not B", context => !context.HasState(MyWorldState.HasB))
                        .Do(context =>
                        {
                            context.SetState(MyWorldState.HasB, true, EffectType.Permanent);
                            return TaskStatus.Success;
                        })
                        .Effect("Has B", EffectType.PlanOnly, (context, type) => context.SetState(MyWorldState.HasB, true, type))
                    .End()
                    .GOAPAction<DomainBuilder<MyContext>, MyContext, GOAPTaskAction>("Get A")
                        .Condition("Has not A", context => !context.HasState(MyWorldState.HasA))
                        .Do(context =>
                        {
                            context.SetState(MyWorldState.HasA, true, EffectType.Permanent);
                            return TaskStatus.Success;
                        })
                        .Effect("Has A", EffectType.PlanOnly, (context, type) => context.SetState(MyWorldState.HasA, true, type))
                    .End()
                .End()
                .Build();

            var ctx = new MyContext();
            ctx.Init();

            var status = domain.FindPlan(ctx, out var plan);
            Assert.IsTrue(status == DecompositionStatus.Succeeded);
            Assert.IsTrue(plan != null);
            Assert.IsTrue(plan.Count == 2);
            Assert.IsTrue(plan.Peek().Name == "Get A");
            plan.Dequeue();
            Assert.IsTrue(plan.Peek().Name == "Get C");
        }

        class GOAPTaskAction : PrimitiveTask, IGOAPTask
        {
            public float Cost(IContext ctx)
            {
                return 1f;
            }
        }

        class GOAPTaskActionHighCost : PrimitiveTask, IGOAPTask
        {
            public float Cost(IContext ctx)
            {
                return 10f;
            }
        }
    }
}
