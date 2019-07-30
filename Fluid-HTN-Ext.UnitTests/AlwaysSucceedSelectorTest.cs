using FluidHTN;
using FluidHTN.Compounds;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fluid_HTN_Ext.UnitTests
{
    [TestClass]
    public class AlwaysSucceedSelectorTest
    {
        /// <summary>
        /// We test whether using Always Succeed Selector will allow us to have optional branching that doesn't
        /// invalidate a sequence if it fails internally.
        /// We expect the plan to return successfully with the single task "get c".
        /// </summary>
        [TestMethod]
        public void AlwaysSucceed_ExpectedBehavior()
        {
            var domain = new DomainBuilder<MyContext>("test")
                .Sequence("test sequence")
                    .AlwaysSucceedSelect<DomainBuilder<MyContext>, MyContext>("test")
                        .Action("always fail")
                            .Condition("always fail", context => context.HasState(MyWorldState.HasA))
                            .Do(context => TaskStatus.Failure)
                        .End()
                        .Action("always fail 2")
                            .Condition("always fail", context => context.HasState(MyWorldState.HasB))
                            .Do(context => TaskStatus.Failure)
                        .End()
                    .End()
                    .Action("get c")
                        .Condition("has not C", context => !context.HasState(MyWorldState.HasC))
                        .Do(context => TaskStatus.Success)
                    .End()
                .End()
                .Build();

            var ctx = new MyContext();
            ctx.Init();

            var status = domain.FindPlan(ctx, out var plan);
            Assert.IsTrue(status == DecompositionStatus.Succeeded);
            Assert.IsTrue(plan != null);
            Assert.IsTrue(plan.Count == 1);
            Assert.IsTrue(plan.Peek().Name == "get c");
        }
    }
}
