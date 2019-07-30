using FluidHTN;
using FluidHTN.Compounds;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fluid_HTN_Ext.UnitTests
{
    [TestClass]
    public class RandomSelectorTest
    {
        [TestMethod]
        public void RandomSelect_ExpectedBehavior()
        {
            var domain = new DomainBuilder<MyContext>("test")
                .RandomSelect<DomainBuilder<MyContext>, MyContext>("random")
                    .Action("get a")
                        .Condition("has not A", context => !context.HasState(MyWorldState.HasA))
                        .Do(context => TaskStatus.Success)
                    .End()
                    .Action("get b")
                        .Condition("has not B", context => !context.HasState(MyWorldState.HasB))
                        .Do(context => TaskStatus.Success)
                    .End()
                    .Action("get c")
                        .Condition("has not C", context => !context.HasState(MyWorldState.HasC))
                        .Do(context => TaskStatus.Success)
                    .End()
                .End()
                .Build();

            var ctx = new MyContext();
            ctx.Init();

            int aCount = 0;
            int bCount = 0;
            int cCount = 0;
            for (var i = 0; i < 1000; i++)
            {
                var status = domain.FindPlan(ctx, out var plan);
                Assert.IsTrue(status == DecompositionStatus.Succeeded);
                Assert.IsTrue(plan != null);
                Assert.IsTrue(plan.Count == 1);

                var name = plan.Peek().Name;
                if (name == "get a") aCount++;
                if (name == "get b") bCount++;
                if (name == "get c") cCount++;

                Assert.IsTrue(
                    name == "get a" ||
                    name == "get b" ||
                    name == "get c");
                plan.Clear();
            }

            // With 1000 iterations, the chance of any of these counts being 0 is suuuper slim.
            Assert.IsTrue(aCount > 0 && bCount > 0 && cCount > 0);
        }
    }
}
