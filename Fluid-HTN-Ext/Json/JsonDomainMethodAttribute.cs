using System;

namespace FluidHTN.Json
{
    /// <summary>
    ///     Marks a method on a custom domain builder as JSON-dispatchable.
    ///     The source generator will create a factory class that calls these methods
    ///     based on the "type" field in the JSON domain definition.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class JsonDomainMethodAttribute : Attribute
    {
        /// <summary>
        ///     Gets the JSON dispatch key (the "type" value in the JSON that triggers this method).
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///     Gets whether this method is expected to have subtasks in the "subtasks" JSON array.
        ///     If not explicitly set, this is inferred from the JSON structure at runtime.
        /// </summary>
        public bool HasSubtasks { get; set; } = true;

        /// <summary>
        ///     Creates a new JsonDomainMethodAttribute.
        /// </summary>
        /// <param name="name">The JSON dispatch key for this method</param>
        public JsonDomainMethodAttribute(string name)
        {
            Name = name;
        }
    }
}
