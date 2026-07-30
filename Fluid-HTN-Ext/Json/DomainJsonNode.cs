using System;
using System.Collections.Generic;

namespace FluidHTN.Json
{
    /// <summary>
    ///     Pure data model for domain JSON structures.
    ///     Decoupled from any specific JSON library (System.Text.Json, Newtonsoft.Json, etc.).
    ///     Provides convenient accessors for common JSON domain structure patterns.
    /// </summary>
    public class DomainJsonNode
    {
        private readonly Dictionary<string, string> _scalars;
        private readonly Dictionary<string, DomainJsonNode> _children;
        private readonly Dictionary<string, List<DomainJsonNode>> _arrays;

        /// <summary>
        ///     Creates a DomainJsonNode with the provided dictionaries.
        ///     Internal constructor used by IDomainJsonProvider implementations.
        /// </summary>
        public DomainJsonNode(
            Dictionary<string, string> scalars,
            Dictionary<string, DomainJsonNode> children,
            Dictionary<string, List<DomainJsonNode>> arrays)
        {
            _scalars = scalars ?? new Dictionary<string, string>();
            _children = children ?? new Dictionary<string, DomainJsonNode>();
            _arrays = arrays ?? new Dictionary<string, List<DomainJsonNode>>();
        }

        /// <summary>Gets a string value from a property.</summary>
        public string GetString(string propertyName, string defaultValue = null)
        {
            if (_scalars != null && _scalars.TryGetValue(propertyName, out var value))
            {
                return value;
            }
            return defaultValue;
        }

        /// <summary>Gets a numeric value from a property.</summary>
        public T GetValue<T>(string propertyName, T defaultValue = default)
        {
            var str = GetString(propertyName);
            if (string.IsNullOrEmpty(str))
                return defaultValue;

            try
            {
                return (T)Convert.ChangeType(str, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>Gets an enum value from a property.</summary>
        public T GetEnum<T>(string propertyName, T defaultValue = default) where T : struct, Enum
        {
            var str = GetString(propertyName);
            if (string.IsNullOrEmpty(str))
                return defaultValue;

            if (Enum.TryParse<T>(str, ignoreCase: true, out var result))
                return result;

            return defaultValue;
        }

        /// <summary>Gets a child node by property name.</summary>
        public DomainJsonNode GetChild(string propertyName)
        {
            if (_children != null && _children.TryGetValue(propertyName, out var child))
            {
                return child;
            }
            return null;
        }

        /// <summary>Gets an array of child nodes from a property.</summary>
        public IEnumerable<DomainJsonNode> GetArray(string propertyName)
        {
            if (_arrays != null && _arrays.TryGetValue(propertyName, out var arr))
            {
                return arr;
            }
            return new List<DomainJsonNode>();
        }

        /// <summary>Checks if a property exists and is not null.</summary>
        public bool HasProperty(string propertyName)
        {
            return (_scalars != null && _scalars.ContainsKey(propertyName)) ||
                   (_children != null && _children.ContainsKey(propertyName)) ||
                   (_arrays != null && _arrays.ContainsKey(propertyName));
        }

        /// <summary>Gets the "type" property (standard dispatch key).</summary>
        public string Type => GetString("type");

        /// <summary>Gets the "name" property (task name).</summary>
        public string Name => GetString("name");

        /// <summary>Gets the "subtasks" array.</summary>
        public IEnumerable<DomainJsonNode> Subtasks => GetArray("subtasks");

        /// <summary>Gets the "conditions" array.</summary>
        public IEnumerable<DomainJsonNode> Conditions => GetArray("conditions");

        /// <summary>Gets the "operator" child object.</summary>
        public DomainJsonNode Operator => GetChild("operator");

        /// <summary>Gets the "effects" array.</summary>
        public IEnumerable<DomainJsonNode> Effects => GetArray("effects");
    }
}
