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
                    var value = prop.Value;

                    if (value is null)
                    {
                        // JSON null: treat as absent so HasProperty ("exists and is not null") stays accurate.
                    }
                    else if (value is JsonObject childObj)
                    {
                        children[prop.Key] = ConvertNode(childObj);
                    }
                    else if (value is JsonArray arr)
                    {
                        var list = new List<DomainJsonNode>();
                        foreach (var item in arr)
                        {
                            if (item is not null)
                            {
                                list.Add(ConvertNode(item));
                            }
                        }
                        arrays[prop.Key] = list;
                    }
                    else if (value is JsonValue jsonValue)
                    {
                        // Convert scalar values to string representation
                        scalars[prop.Key] = jsonValue.GetValue<object>()?.ToString() ?? string.Empty;
                    }
                }
            }

            return new DomainJsonNode(scalars, children, arrays);
        }
    }
}
