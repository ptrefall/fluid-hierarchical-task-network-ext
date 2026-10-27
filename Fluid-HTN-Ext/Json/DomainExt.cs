using FluidHTN.Json;

// Kept in the FluidHTN namespace, not FluidHTN.Json, so Load shows up on Domain<T> without a using.
namespace FluidHTN
{
    public static class DomainExt
    {
        // ========================================================= SERIALIZATION

        /// <summary>
        ///     Load a domain from a json file. A convenience over
        ///     <see cref="DomainJsonFactory{TBuilder,TContext}.BuildFromFile"/>, which it calls.
        ///
        ///     The loaded domain is returned as a new instance; the domain this is called on is not
        ///     modified. A factory is single-use, since building frees its builder.
        /// </summary>
        /// <typeparam name="TBuilder"></typeparam>
        /// <typeparam name="TContext"></typeparam>
        /// <param name="domain"></param>
        /// <param name="factory">The json factory whose builder methods the file's node types dispatch to</param>
        /// <param name="fileName"></param>
        /// <returns>The loaded domain</returns>
        public static Domain<TContext> Load<TBuilder, TContext>(this Domain<TContext> domain,
            DomainJsonFactory<TBuilder, TContext> factory, string fileName)
            where TBuilder : BaseDomainBuilder<TBuilder, TContext>
            where TContext : IContext
        {
            return factory.BuildFromFile(fileName);
        }
    }
}
