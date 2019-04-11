namespace FluidHTN
{
    public static class DomainExt
    {
        // ========================================================= SERIALIZATION

        /// <summary>
        ///     Save the domain to a json file
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="domain"></param>
        /// <param name="fileName"></param>
        public static void Save<T>(this Domain<T> domain, string fileName) where T : IContext
        {
        }

        /// <summary>
        ///     Load the domain from a json file
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="domain"></param>
        /// <param name="fileName"></param>
        public static void Load<T>(this Domain<T> domain, string fileName) where T : IContext
        {
        }
    }
}