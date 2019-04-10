
using FluidHTN.Compounds;

namespace FluidHTN
{
    public static class DomainBuilderExt
    {
        // ========================================================= COMPOUND TASKS

        /// <summary>
        /// A compound task that will pick a random sub-task to decompose.
        /// Sub-tasks can be sequences, selectors or actions.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static DomainBuilder<T> RandomSelect<T>(this DomainBuilder<T> builder, string name) where T : IContext
        {
            return builder.CompoundTask<RandomSelector>(name);
        }

        /// <summary>
        /// A compound task that will pick the sub-task with the highest utility score.
        /// Sub-tasks can be sequences, selectors or actions that implement the IUtilityTask interface.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static DomainBuilder<T> UtilitySelect<T>(this DomainBuilder<T> builder, string name) where T : IContext
        {
            return builder.CompoundTask<UtilitySelector>(name);
        }

        // ========================================================= SERIALIZATION

        /// <summary>
		/// Builds the designed domain and saves it to a json file, then returns the domain instance.
		/// </summary>
		/// <param name="fileName"></param>
		public static Domain<T> BuildAndSave<T>(this DomainBuilder<T> builder, string fileName) where T : IContext
        {
            var domain = builder.Build();
            domain.Save(fileName);
            return domain;
        }

        /// <summary>
        /// Loads a designed domain from a json file and returns a domain instance of it.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static Domain<T> Load<T>(this DomainBuilder<T> builder, string fileName) where T : IContext
        {
            var domain = new Domain<T>(string.Empty);
            domain.Load(fileName);
            return domain;
        }
    }
}