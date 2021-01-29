using FluidHTN;
using FluidHTN.Compounds;
using FluidHTN.PrimitiveTasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fluid_HTN_Ext.UnitTests
{
    [TestClass]
    public class UtilitySelectorTest
    {
        /// <summary>
        /// Test whether the task with the highest utility is selected among the sub-tasks.
        /// </summary>
        [TestMethod]
        public void BestUtilityIsSelected_ExpectedBehavior()
        {
            var domain = new DomainBuilder<MyContext>("test")
                .UtilitySelect<DomainBuilder<MyContext>, MyContext>("utility select")
                    .UtilityAction<DomainBuilder<MyContext>, MyContext, UtilityActionLowUtility>("low utility")
                        .Condition("Has not A", context => !context.HasState(MyWorldState.HasA))
                        .Do(context =>
                        {
                            context.SetState(MyWorldState.HasA, true, EffectType.Permanent);
                            return TaskStatus.Success;
                        })
                        .Effect("Has A", EffectType.PlanOnly, (context, type) => context.SetState(MyWorldState.HasA, true, type))
                    .End()
                    .UtilityAction<DomainBuilder<MyContext>, MyContext, UtilityActionHighUtility>("high utility")
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
            Assert.IsTrue(plan.Count == 1);
            Assert.IsTrue(plan.Peek().Name == "high utility");
        }

        class UtilityActionLowUtility : PrimitiveTask, IUtilityTask
        {
            public float Score(IContext ctx)
            {
                return 1f;
            }
        }

        class UtilityActionHighUtility : PrimitiveTask, IUtilityTask
        {
            public float Score(IContext ctx)
            {
                return 10f;
            }
        }
    }
}
