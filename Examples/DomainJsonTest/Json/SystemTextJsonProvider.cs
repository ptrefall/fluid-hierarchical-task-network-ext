using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace FluidHTN.Json
{
    /// <summary>
    ///     IDomainJsonProvider implementation using System.Text.Json.
    ///     This provider bridges System.Text.Json JsonNode structures to the platform-agnostic DomainJsonNode model.
    /// </summary>
    public class SystemTextJsonProvider : IDomainJsonProvider
    {
        /// <summary>
        ///     Parses a JSON string using System.Text.Json and converts it to a DomainJsonNode.
        /// </summary>
        /// <param name="json">The JSON string to parse</param>
        /// <returns>A DomainJsonNode representing the parsed structure</returns>
        public DomainJsonNode Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentException("JSON string cannot be null or empty", nameof(json));

            var node = JsonNode.Parse(json);
            if (node == null)
                throw new ArgumentException("Failed to parse JSON", nameof(json));

            return ConvertNode(node);
        }

        /// <summary>
        ///     Recursively converts a System.Text.Json JsonNode to a DomainJsonNode.
        /// </summary>
        private DomainJsonNode ConvertNode(JsonNode node)
        {
            var scalars = new Dictionary<string, string>();
            var children = new Dictionary<string, DomainJsonNode>();
            var arrays = new Dictionary<string, List<DomainJsonNode>>();

            if (node is JsonObject obj)
            {
                foreach (var prop in obj)
                {
                    AddProperty(prop.Key, prop.Value, scalars, children, arrays);
                }
            }

            return new DomainJsonNode(scalars, children, arrays);
        }

        /// <summary>
        ///     Sort a single JSON property into the scalar, child-object or array bucket. A JSON null is
        ///     treated as absent so HasProperty ("exists and is not null") stays accurate.
        /// </summary>
        private void AddProperty(
            string key,
            JsonNode value,
            Dictionary<string, string> scalars,
            Dictionary<string, DomainJsonNode> children,
            Dictionary<string, List<DomainJsonNode>> arrays)
        {
            if (value is null)
            {
                return;
            }

            if (value is JsonObject childObj)
            {
                children[key] = ConvertNode(childObj);
                return;
            }

            if (value is JsonArray arr)
            {
                arrays[key] = ConvertArray(arr);
                return;
            }

            if (value is JsonValue jsonValue)
            {
                // Convert scalar values to string representation
                scalars[key] = jsonValue.GetValue<object>()?.ToString() ?? string.Empty;
            }
        }

        /// <summary>Convert a JSON array's non-null items into a list of DomainJsonNode.</summary>
        private List<DomainJsonNode> ConvertArray(JsonArray arr)
        {
            var list = new List<DomainJsonNode>();
            foreach (var item in arr)
            {
                if (item is not null)
                {
                    list.Add(ConvertNode(item));
                }
            }

            return list;
        }
    }
}
