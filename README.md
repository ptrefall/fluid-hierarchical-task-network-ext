![Fluid Hierarchical Task Network Extensions](https://i.imgur.com/xKfIV0f.png)
# Fluid Extensions
Work in progress extensions to the [Fluid Hierarchical Task Network](https://github.com/ptrefall/fluid-hierarchical-task-network).

## Features
* Random Selector selects among sub-tasks randomly.
* Utility Selector selects the sub-task with the best utility. Requires sub-tasks to implement the IUtilityTask interface.
* Invert Status Selector inverts the result from its decomposition.
* Always Succeed Selector will always succeed, even when its internal decomposition fail. Useful for optional branching in a sequence.
* Repeat Sequence will repeat decomposition over its sub-tasks as many times as defined by the given world state value.
* GOAP Sequence takes a goal state set and will try to find the shortest path to that goal state through its sub-tasks. Requires sub-tasks to implement the IGOAPTask interface.

## Support
Join the [discord channel](https://discord.gg/MuccnAz) to share your experience and get support on the usage of Fluid HTN.

## TODO
* JSON serialization that opens up Fluid HTN to more editor possibilities.
* Documentation
