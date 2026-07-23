![Fluid Hierarchical Task Network Extensions](https://i.imgur.com/xKfIV0f.png)
# Fluid Extensions
Work in progress extensions to the [Fluid Hierarchical Task Network](https://github.com/ptrefall/fluid-hierarchical-task-network).

![MIT License](https://img.shields.io/badge/license-MIT-blue.svg)

## Features
* Random Selector selects among sub-tasks randomly.
* Utility Selector selects the sub-task with the best utility. Requires sub-tasks to implement the IUtilityTask interface.
* Invert Status Selector inverts the result from its decomposition.
* Always Succeed Selector will always succeed, even when its internal decomposition fail. Useful for optional branching in a sequence.
* Repeat Sequence will repeat decomposition over its sub-tasks as many times as defined by the given world state value.
* GOAP Sequence takes a goal state set and will try to find the shortest path to that goal state through its sub-tasks. Requires sub-tasks to implement the IGOAPTask interface.
* Parallel Task decomposes like a sequence, but ticks all its branches at once. Each branch becomes its own execution lane; the task completes when every lane has drained, and fails when any one of them fails.

### Parallel Task

Think of it as a sequence you tick all at once. Every branch must be valid and decomposable at plan
time or the whole task fails, and one branch failing at execution fails it too - exactly the contract
a sequence already has. Wrap a branch in an Always Succeed Selector to make it optional.

```csharp
new DomainBuilder<MyContext>("camp")
    .Parallel<DomainBuilder<MyContext>, MyContext>("make camp")
        .Action("chop wood")
            .Do(ctx => ctx.WoodChopped >= 4 ? TaskStatus.Success : TaskStatus.Continue)
        .End()
        .Sequence("fetch water")
            .Action("walk to river").Do(ctx => TaskStatus.Success).End()
            .Action("fill bucket").Do(ctx => TaskStatus.Success).End()
        .End()
    .End()
    .Build();
```

The one place it has to diverge from a sequence is decomposition. A sequence deliberately applies each
step's effects so the next step can validate against them, but concurrent branches start together, so a
branch must not be planned assuming a sibling has already finished. The world state change stack is
therefore rolled back between branches, and all of their effects are applied together afterwards - so
whatever is sequenced *after* the parallel task still validates against the joint outcome.

Lanes survive a replan that keeps the parallel task in the plan: a long durative branch keeps running,
and its own partially consumed plan, while a newly plannable sibling opens a lane beside it. A replan
that drops the parallel task stops its lanes. Two branches writing the same world state index is
allowed - concurrent lanes racing for one fact is the author's business. Partial plans (`PausePlan()`)
are not supported inside a branch, since a paused remainder has no lane to resume into.

See `Examples/ParallelTaskTest` for a runnable demonstration.

## Support
Join the [discord channel](https://discord.gg/MuccnAz) to share your experience and get support on the usage of Fluid HTN.

## TODO
* JSON serialization that opens up Fluid HTN to more editor possibilities.
* Documentation
