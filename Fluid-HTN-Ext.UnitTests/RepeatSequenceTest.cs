using FluidHTN;
using FluidHTN.Compounds;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fluid_HTN_Ext.UnitTests
{
    [TestClass]
    public class RepeatSequenceTest
    {
        [TestMethod]
        public void RepeatSequence_ExpectedBehavior()
        {
            var domain = new DomainBuilder<MyContext>("test")
                .Repeat<DomainBuilder<MyContext>, MyContext>("repeat", (uint)MyWorldState.HasA)
                    .Action("increment b")
                        .Do(context =>
                        {
                            var b = context.GetState(MyWorldState.HasB);
                            context.SetState(MyWorldState.HasB, b + 1, EffectType.Permanent);
                            return TaskStatus.Success;
                        })
                    .End()
                .End()
                .Build();

            var ctx = new MyContext();
            ctx.Init();
            ctx.SetState(MyWorldState.HasA, 3, EffectType.Permanent);

            var status = domain.FindPlan(ctx, out var plan);
            Assert.IsTrue(status == DecompositionStatus.Succeeded);
            Assert.IsTrue(plan != null);
            Assert.IsTrue(plan.Count == 3);
            Assert.IsTrue(plan.Peek().Name == "increment b");
            plan.Dequeue();
            Assert.IsTrue(plan.Peek().Name == "increment b");
            plan.Dequeue();
            Assert.IsTrue(plan.Peek().Name == "increment b");
        }
    }
}
