using FluidHTN;
using FluidHTN.Compounds;
using FluidHTN.Contexts;
using FluidHTN.Debug;
using FluidHTN.Factory;

// Implicit usings pull in System.Threading.Tasks, which has a TaskStatus of its own.
using TaskStatus = FluidHTN.TaskStatus;

var demo = new ParallelTaskDemo();
demo.Run();

/// <summary>
///     Shows what a Parallel Task actually buys you: a set of branches that are planned like a
///     sequence, but ticked all at once. The interesting moment is tick 3, where an optional branch
///     becomes plannable and the resulting replan opens a new lane WITHOUT restarting the long
///     branch that is already running.
/// </summary>
public class ParallelTaskDemo
{
    private readonly List<string> _tickLog = new();

    public void Run()
    {
        Console.WriteLine("=== Fluid HTN Parallel Task ===\n");

        var context = new CampContext();
        context.Init();

        var domain = BuildDomain();
        var planner = new Planner<CampContext>();

        for (var tick = 1; tick <= 6; tick++)
        {
            // Halfway through the job, the agent cheers up. That makes the optional branch
            // plannable, dirties the context, and forces a replan of a plan that is already running.
            if (tick == 3)
            {
                Console.WriteLine("  (the agent cheers up - the optional branch becomes plannable)");
                context.SetState(CampWorldState.Cheerful, true, EffectType.Permanent);
            }

            _tickLog.Clear();
            planner.Tick(domain, context, allowImmediateReplanAndExecute: false);

            Console.WriteLine($"Tick {tick}: {(_tickLog.Count == 0 ? "(idle)" : string.Join(" | ", _tickLog))}");

            if (context.PlannerState.CurrentTask == null && context.PlannerState.Plan.Count == 0 && tick > 1)
            {
                Console.WriteLine("\nEvery branch has drained, so the parallel task is done.");
                break;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Chop Wood was started {context.ChopStarts} time(s) across {context.ChopUpdates} updates.");
        Console.WriteLine($"Walk To River was started {context.WalkStarts} time(s).");
        Console.WriteLine($"Sing ran {context.SongCount} time(s).");
        Console.WriteLine();

        if (context.ChopStarts == 1 && context.WalkStarts == 1)
        {
            Console.WriteLine("✓ The replan on tick 3 opened a new lane without restarting the running ones.");
        }
        else
        {
            Console.WriteLine("✗ A running lane was restarted by the replan!");
        }
    }

    private Domain<CampContext> BuildDomain()
    {
        return new DomainBuilder<CampContext>("camp")
            .Parallel<DomainBuilder<CampContext>, CampContext>("Make Camp")

                // A long, durative branch. It outlives the replan on tick 3.
                .Action("Chop Wood")
                    .Do(context =>
                        {
                            context.ChopUpdates++;
                            Report($"Chop Wood {context.ChopUpdates}/4");
                            return context.ChopUpdates >= 4 ? TaskStatus.Success : TaskStatus.Continue;
                        },
                        context =>
                        {
                            context.ChopStarts++;
                            return TaskStatus.Continue;
                        })
                .End()

                // A branch with several steps of its own. Its lane keeps its own partially consumed
                // plan, so the replan does not send it back to the river.
                .Sequence("Fetch Water")
                    .Action("Walk To River")
                        .Do(context =>
                            {
                                context.WalkUpdates++;
                                Report($"Walk To River {context.WalkUpdates}/2");
                                return context.WalkUpdates >= 2 ? TaskStatus.Success : TaskStatus.Continue;
                            },
                            context =>
                            {
                                context.WalkStarts++;
                                return TaskStatus.Continue;
                            })
                    .End()
                    .Action("Fill Bucket")
                        .Do(context =>
                        {
                            Report("Fill Bucket");
                            return TaskStatus.Success;
                        })
                    .End()
                .End()

                // Optional work: an Always Succeed Selector contributes no lane while its contents
                // cannot be planned, and a lane of its own once they can.
                .AlwaysSucceedSelect<DomainBuilder<CampContext>, CampContext>("Optional Song")
                    .Action("Sing")
                        .Condition("is cheerful", context => context.HasState(CampWorldState.Cheerful))
                        .Do(context =>
                        {
                            context.SongCount++;
                            Report("Sing");
                            return TaskStatus.Success;
                        })
                    .End()
                .End()

            .End()
            .Build();
    }

    private void Report(string what)
    {
        _tickLog.Add(what);
    }
}

public enum CampWorldState : byte
{
    Cheerful
}

public class CampContext : BaseContext
{
    private readonly byte[] _worldState = new byte[Enum.GetValues(typeof(CampWorldState)).Length];

    public override IFactory Factory { get; protected set; } = new DefaultFactory();
    public override IPlannerState PlannerState { get; protected set; } = new DefaultPlannerState();
    public override List<string> MTRDebug { get; set; } = null!;
    public override List<string> LastMTRDebug { get; set; } = null!;
    public override bool DebugMTR { get; } = false;
    public override Queue<IBaseDecompositionLogEntry> DecompositionLog { get; set; } = null!;
    public override bool LogDecomposition { get; } = false;
    public override byte[] WorldState => _worldState;

    // Plain counters, so the demo can show how often each operator was started rather than only
    // what the world state ended up as.
    public int ChopStarts { get; set; }
    public int ChopUpdates { get; set; }
    public int WalkStarts { get; set; }
    public int WalkUpdates { get; set; }
    public int SongCount { get; set; }

    public bool HasState(CampWorldState state)
    {
        return HasState((int) state, 1);
    }

    public void SetState(CampWorldState state, bool value, EffectType type)
    {
        SetState((int) state, (byte) (value ? 1 : 0), true, type);
    }
}
