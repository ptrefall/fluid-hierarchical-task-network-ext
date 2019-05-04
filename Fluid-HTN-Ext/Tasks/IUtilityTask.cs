namespace FluidHTN
{
    /// <summary>
    ///     Extend a task with the Utility interface, making it viable for use with the UtilitySelector extension.
    /// </summary>
    public interface IUtilityTask : ITask
    {
        /// <summary>
        ///     A Utility function that scores its vitality against other utility tasks.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        float Score(IContext ctx);
    }
}