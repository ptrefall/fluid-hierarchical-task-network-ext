using FluidHTN;
using FluidHTN.Compounds;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fluid_HTN_Ext.UnitTests
{
    [TestClass]
    public class InvertStatusSelectorTest
    {
        /// <summary>
        /// We test whether invert status selector will revert both sub-tasks that succeed and fail decomposition.
        /// We expect inverting the 'get a' to go from successful to failure
        /// We expect inverting the 'get b' to go from failure to success
        /// We expect the plan to only hold 'get c'.
        /// </summary>
        [TestMethod]
        public void InvertStatus_ExpectedBehavior()
        {
            var domain = new DomainBuilder<MyContext>("test")
                .InvertStatusSelect<DomainBuilder<MyContext>, MyContext>("test")
                    .Action("get a")
                        .Condition("has not A", context => !context.HasState(MyWorldState.HasA))
                        .Do(context => TaskStatus.Success)
                    .End()
                .End()
                .Sequence("test sequence")
                    .InvertStatusSelect<DomainBuilder<MyContext>, MyContext>("test")
                        .Action("always fail")
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
