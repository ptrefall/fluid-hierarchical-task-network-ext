using FluidHTN.PrimitiveTasks;

namespace FluidHTN
{
    /// <summary>
    /// Extend a task with the GOAP interface, making it viable for use with the GOAPSequence extension.
    /// </summary>
    public interface IGOAPTask : IPrimitiveTask
    {
        /// <summary>
        /// A heuristic function used by GOAP when looking for the shortest path / path of lowest cost.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        float Cost(IContext ctx);
    }
}