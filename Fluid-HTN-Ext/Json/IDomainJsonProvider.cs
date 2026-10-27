namespace FluidHTN.Json
{
    /// <summary>
    ///     Interface for pluggable JSON parsing providers.
    ///     Implementations can use System.Text.Json, Newtonsoft.Json, Unity's JSON utilities, or custom parsers.
    /// </summary>
    public interface IDomainJsonProvider
    {
        /// <summary>
        ///     Parses a JSON string into a DomainJsonNode.
        /// </summary>
        /// <param name="json">The JSON string to parse</param>
        /// <returns>A DomainJsonNode representing the parsed structure</returns>
        DomainJsonNode Parse(string json);
    }
}
