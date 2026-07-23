using FluidHTN.Json;

namespace FluidHTN.Examples.DomainJsonTest
{
    /// <summary>
    ///     Concrete JSON domain factory for <see cref="MyDomainBuilder"/>.
    ///
    ///     The base <see cref="DomainJsonFactory{TBuilder,TContext}"/> auto-registers every
    ///     [JsonDomainMethod]-attributed method on MyDomainBuilder via reflection, so this class
    ///     usually just needs to supply a JSON provider. Override <see cref="OnConfigure"/> to
    ///     register additional handlers or override auto-registered ones with
    ///     <see cref="DomainJsonFactory{TBuilder,TContext}.Register"/>.
    /// </summary>
    public class MyDomainBuilderJsonFactory : DomainJsonFactory<MyDomainBuilder, MyContext>
    {
        /// <summary>
        ///     Convenience constructor that uses <see cref="SystemTextJsonProvider"/> by default.
        ///     Pass a different <see cref="IDomainJsonProvider"/> to use Newtonsoft, Unity JsonUtility, etc.
        /// </summary>
        public MyDomainBuilderJsonFactory(MyDomainBuilder builder)
            : base(builder, new SystemTextJsonProvider())
        {
        }

        /// <summary>
        ///     Uses the supplied builder and JSON provider.
        /// </summary>
        public MyDomainBuilderJsonFactory(MyDomainBuilder builder, IDomainJsonProvider provider)
            : base(builder, provider)
        {
        }
    }
}
