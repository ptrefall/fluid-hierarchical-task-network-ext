
using FluidHTN;
using FluidHTN.Contexts;
using FluidHTN.Debug;
using FluidHTN.Factory;
using FluidHTN.Json;
using FluidHTN.PrimitiveTasks;

namespace FluidHTN.Examples.DomainJsonTest
{
    /// <summary>
    ///     Example custom domain builder with [JsonDomainMethod] attributes.
    ///     The source generator will scan these attributes and generate a factory class
    ///     that maps JSON type names to these methods.
    /// </summary>
    public class MyDomainBuilder : BaseDomainBuilder<MyDomainBuilder, MyContext>
    {
        public MyDomainBuilder(string name) : base(name, new DefaultFactory())
        {
        }

        // ========================================================= BASE BUILDER METHODS (WITH ATTRIBUTES)

        /// <summary>Creates a Sequence compound task.</summary>
        [JsonDomainMethod("Sequence")]
        public new MyDomainBuilder Sequence(string name)
        {
            return base.Sequence(name);
        }

        /// <summary>Creates a Selector compound task.</summary>
        [JsonDomainMethod("Selector")]
        public new MyDomainBuilder Select(string name)
        {
            return base.Select(name);
        }

        /// <summary>Creates a primitive Action task.</summary>
        [JsonDomainMethod("Action")]
        public new MyDomainBuilder Action(string name)
        {
            return base.PrimitiveTask<SimpleAction>(name);
        }

        // ========================================================= EXTENSION METHODS (WITH ATTRIBUTES)

        /// <summary>Creates a RandomSelector compound task.</summary>
        [JsonDomainMethod("RandomSelect")]
        public MyDomainBuilder RandomSelect(string name)
        {
            return this.RandomSelect<MyDomainBuilder, MyContext>(name);
        }

        /// <summary>Creates a UtilitySelector compound task.</summary>
        [JsonDomainMethod("UtilitySelect")]
        public MyDomainBuilder UtilitySelect(string name)
        {
            return this.UtilitySelect<MyDomainBuilder, MyContext>(name);
        }

        /// <summary>Creates an InvertStatusSelector compound task.</summary>
        [JsonDomainMethod("InvertStatusSelect")]
        public MyDomainBuilder InvertStatusSelect(string name)
        {
            return this.InvertStatusSelect<MyDomainBuilder, MyContext>(name);
        }

        /// <summary>
        ///     Creates a ParallelTask compound task. Its sub-tasks are authored exactly like a
        ///     sequence's, but they run concurrently, each in its own lane.
        /// </summary>
        [JsonDomainMethod("Parallel")]
        public MyDomainBuilder Parallel(string name)
        {
            return this.Parallel<MyDomainBuilder, MyContext>(name);
        }

        /// <summary>Creates a RepeatSequence compound task.</summary>
        [JsonDomainMethod("Repeat")]
        public MyDomainBuilder Repeat(string name, uint worldStateIndex,
            FluidHTN.Compounds.RepeatSequence.RepetitionType repetitionType =
                FluidHTN.Compounds.RepeatSequence.RepetitionType.Interleaved)
        {
            return this.Repeat<MyDomainBuilder, MyContext>(name, worldStateIndex, repetitionType);
        }

        /// <summary>
        ///     A self-contained action, built in C#, for getting food. Its condition and effect act on
        ///     the HTN world state (MyWorldState.Hungry) — not a plain C# field — so the effect is tracked
        ///     on the world-state stack during planning and rolled back correctly if a branch fails.
        ///     Compare with the JSON-authored "Rest" action in domain.json, which does the same thing
        ///     for fatigue but declares its condition/operator/effect entirely as data.
        /// </summary>
        [JsonDomainMethod("GetFood")]
        public MyDomainBuilder GetFood()
        {
            return Action("Get Food")
                .Condition("is hungry", ctx => ctx.HasState(MyWorldState.Hungry))
                .Do(ctx => TaskStatus.Success)
                .Effect("ate food", EffectType.PlanAndExecute,
                    (ctx, type) => ctx.SetState(MyWorldState.Hungry, false, type))
                .End();
        }

        /// <summary>
        ///     A simple action that always succeeds (for testing).
        ///     This demonstrates parameterless attributed methods that are self-contained.
        /// </summary>
        [JsonDomainMethod("Idle")]
        public MyDomainBuilder Idle()
        {
            return Action("Idle")
                .Do(ctx => TaskStatus.Success)
                .End();
        }

        /// <summary>
        ///     A patrol action. Always available, makes the agent tired.
        /// </summary>
        [JsonDomainMethod("Patrol")]
        public MyDomainBuilder Patrol()
        {
            return Action("Patrol")
                .Do(ctx => TaskStatus.Success)
                .Effect("become tired", EffectType.PlanAndExecute,
                    (ctx, type) => ctx.SetState(MyWorldState.Tired, true, type))
                .End();
        }

        // ================================================= DATA-DRIVEN CONDITIONS / OPERATOR / EFFECTS
        //
        // The methods above bake their conditions/effects into C#. The handlers below instead let a
        // condition, operator, or effect be authored *entirely in JSON* as child nodes of a task:
        //
        //   {
        //     "type": "Action", "name": "Eat",
        //     "conditions": [ { "type": "Condition", "state": "Hungry", "value": true } ],
        //     "operator":     { "type": "Do" },
        //     "effects":    [ { "type": "Effect", "state": "Hungry", "value": false } ]
        //   }
        //
        // DomainJsonFactory.ProcessNode already walks a task's "conditions", "operator" and "effects"
        // and dispatches each by its "type" — the same way it dispatches tasks. These handlers add a
        // condition/operator/effect to the CURRENT task (they don't open a new scope), so the factory
        // sees the builder pointer unchanged and correctly skips a matching End() for them.

        /// <summary>
        ///     A precondition authored in JSON: gates the current task on a named world-state flag.
        ///     JSON: { "type": "Condition", "state": "Hungry", "value": true }
        /// </summary>
        [JsonDomainMethod("Condition")]
        public MyDomainBuilder WorldStateCondition(string state, bool value = true)
        {
            var ws = Enum.Parse<MyWorldState>(state, ignoreCase: true);
            return this.Condition($"{state} == {value}", ctx => ctx.HasState(ws) == value);
        }

        /// <summary>
        ///     A default operator for JSON-authored actions. JSON can't carry C# logic, so this simply
        ///     succeeds — a real project would register a named operator that does actual work.
        ///     JSON: { "type": "Do" }
        /// </summary>
        [JsonDomainMethod("Do")]
        public MyDomainBuilder DoSucceed()
        {
            return this.Do(ctx => TaskStatus.Success);
        }

        /// <summary>
        ///     An effect authored in JSON: writes a named world-state flag during planning (and,
        ///     for PlanAndExecute, at runtime). JSON:
        ///     { "type": "Effect", "state": "Hungry", "value": false, "effectType": "PlanAndExecute" }
        /// </summary>
        [JsonDomainMethod("Effect")]
        public MyDomainBuilder WorldStateEffect(string state, bool value = false,
            EffectType effectType = EffectType.PlanAndExecute)
        {
            var ws = Enum.Parse<MyWorldState>(state, ignoreCase: true);
            return this.Effect($"{state} = {value}", effectType, (ctx, type) => ctx.SetState(ws, value, type));
        }
    }

    /// <summary>
    ///     Simple action implementation for the example.
    ///     In a real application, you'd implement the actual task logic here.
    /// </summary>
    public class SimpleAction : PrimitiveTask
    {
        public SimpleAction()
        {
        }
    }

    /// <summary>
    ///     Example context with world state and custom properties.
    ///     This matches the pattern shown in Fluid-HTN-Ext.UnitTests/MyContext.cs
    /// </summary>
    public enum MyWorldState : byte
    {
        Hungry,
        Tired,
    }

    public class MyContext : BaseContext
    {
        private byte[] _worldState = new byte[Enum.GetValues(typeof(MyWorldState)).Length];

        public override IFactory Factory { get; protected set; } = new DefaultFactory();
        public override IPlannerState PlannerState { get; protected set; } = new DefaultPlannerState();
        public override List<string> MTRDebug { get; set; } = null!;
        public override List<string> LastMTRDebug { get; set; } = null!;
        public override bool DebugMTR { get; } = false;
        public override Queue<IBaseDecompositionLogEntry> DecompositionLog { get; set; } = null!;
        public override bool LogDecomposition { get; } = false;
        public override byte[] WorldState => _worldState;

        // Convenience read-only views over the world state, so there is a single source of truth.
        // Tasks read/write via HasState/SetState (which the planner can track and roll back); these
        // are just for display/inspection.
        public bool Hungry => HasState(MyWorldState.Hungry);
        public bool Tired => HasState(MyWorldState.Tired);

        public override void Init()
        {
            base.Init();

            // Seed the world state so the JSON-authored conditions ("Hungry", "Tired") have
            // something to gate on. During planning these are read from the world-state change
            // stack, which falls back to this seeded WorldState array when no effect has run yet.
            _worldState[(int) MyWorldState.Hungry] = 1;
            _worldState[(int) MyWorldState.Tired] = 1;
        }

        public bool HasState(MyWorldState state)
        {
            return HasState((int) state, 1);
        }

        public void SetState(MyWorldState state, bool value, EffectType type)
        {
            SetState((int) state, (byte) (value ? 1 : 0), true, type);
        }

        public byte GetState(MyWorldState state)
        {
            return GetState((int) state);
        }
    }
}
