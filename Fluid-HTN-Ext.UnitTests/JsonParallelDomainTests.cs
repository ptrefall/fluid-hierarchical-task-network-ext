using FluidHTN;
using FluidHTN.Compounds;
using FluidHTN.Factory;
using FluidHTN.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fluid_HTN_Ext.UnitTests
{
    /// <summary>
    ///     A minimal JSON-dispatchable builder, so a parallel task can be authored as data. Only the
    ///     handful of node types these tests need are declared; see Examples/DomainJsonTest for the
    ///     fuller version.
    /// </summary>
    internal class MyJsonDomainBuilder : BaseDomainBuilder<MyJsonDomainBuilder, MyContext>
    {
        public MyJsonDomainBuilder(string name) : base(name, new DefaultFactory())
        {
        }

        [JsonDomainMethod("Sequence")]
        public new MyJsonDomainBuilder Sequence(string name)
        {
            return base.Sequence(name);
        }

        [JsonDomainMethod("Parallel")]
        public MyJsonDomainBuilder Parallel(string name)
        {
            return this.Parallel<MyJsonDomainBuilder, MyContext>(name);
        }

        [JsonDomainMethod("Action")]
        public MyJsonDomainBuilder JsonAction(string name)
        {
            return Action(name).Do(context => TaskStatus.Success);
        }
    }

    internal class MyJsonDomainFactory : DomainJsonFactory<MyJsonDomainBuilder, MyContext>
    {
        public MyJsonDomainFactory(string name, IDomainJsonProvider provider)
            : base(new MyJsonDomainBuilder(name), provider)
        {
        }
    }

    [TestClass]
    public class JsonParallelDomainTests
    {
        private const string Json = @"{
            ""name"": ""json-parallel"",
            ""root"": {
                ""type"": ""Sequence"",
                ""name"": ""Root"",
                ""subtasks"": [
                    {
                        ""type"": ""Parallel"",
                        ""name"": ""par"",
                        ""subtasks"": [
                            { ""type"": ""Action"", ""name"": ""a"" },
                            { ""type"": ""Action"", ""name"": ""b"" }
                        ]
                    },
                    { ""type"": ""Action"", ""name"": ""after"" }
                ]
            }
        }";

        [TestMethod]
        public void JsonAuthoredParallel_BuildsAndDecomposes_Ok()
        {
            var factory = new MyJsonDomainFactory("json-parallel", new SystemTextJsonProvider());
            var domain = factory.Build(Json);

            // The JSON "root" node builds a Sequence underneath the domain's TaskRoot.
            var jsonRoot = (ICompoundTask) domain.Root.Subtasks[0];
            var parallel = jsonRoot.Subtasks[0] as ParallelTask;
            Assert.IsNotNull(parallel);
            Assert.AreEqual("par", parallel.Name);
            Assert.AreEqual(2, parallel.Subtasks.Count);

            var ctx = new MyContext();
            ctx.Init();

            var status = domain.FindPlan(ctx, out var plan);

            Assert.AreEqual(DecompositionStatus.Succeeded, status);
            Assert.AreEqual(2, parallel.BranchCount);

            // The parallel task occupies a single slot in the plan; its lanes hang off it.
            Assert.AreEqual(2, plan.Count);
            Assert.AreEqual("par", plan.Dequeue().Name);
            Assert.AreEqual("after", plan.Dequeue().Name);
        }

        [TestMethod]
        public void JsonAuthoredParallel_RunsItsLanes_Ok()
        {
            var factory = new MyJsonDomainFactory("json-parallel", new SystemTextJsonProvider());
            var domain = factory.Build(Json);

            var ctx = new MyContext();
            ctx.Init();

            var completed = new System.Collections.Generic.List<string>();
            ctx.PlannerState.OnCurrentTaskCompletedSuccessfully = task => completed.Add(task.Name);

            var planner = new Planner<MyContext>();
            planner.Tick(domain, ctx, allowImmediateReplanAndExecute: false);
            planner.Tick(domain, ctx, allowImmediateReplanAndExecute: false);

            // Both lanes drain on the first tick, which is what completes the parallel task itself.
            CollectionAssert.AreEqual(new[] { "a", "b", "par", "after" }, completed);
        }
    }
}
