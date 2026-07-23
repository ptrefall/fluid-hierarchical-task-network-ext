using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace FluidHTN.Json
{
    /// <summary>
    ///     Base class for JSON domain factories.
    ///     Parses a JSON domain structure and dispatches each node's "type" to a builder method.
    ///
    ///     The dispatch table is populated in two ways:
    ///     1. Reflection (convenience): every method on <typeparamref name="TBuilder"/> marked with
    ///        <see cref="JsonDomainMethodAttribute"/> is auto-registered under its JSON name.
    ///     2. Manual registration: call <see cref="Register"/> (typically from <see cref="OnConfigure"/>)
    ///        to add new handlers or override auto-registered ones.
    /// </summary>
    /// <typeparam name="TBuilder">The custom domain builder type</typeparam>
    /// <typeparam name="TContext">The context type</typeparam>
    public class DomainJsonFactory<TBuilder, TContext>
        where TBuilder : BaseDomainBuilder<TBuilder, TContext>
        where TContext : IContext
    {
        /// <summary>
        ///     The builder instance used for constructing the domain.
        /// </summary>
        protected TBuilder Builder { get; private set; }

        /// <summary>
        ///     The JSON provider used for parsing JSON strings.
        /// </summary>
        protected IDomainJsonProvider Provider { get; private set; }

        /// <summary>
        ///     Maps a JSON "type" name to a builder-method invocation. Built once at construction
        ///     from reflection and extended/overridden via <see cref="Register"/>.
        /// </summary>
        private readonly Dictionary<string, Func<DomainJsonNode, TBuilder>> _dispatch =
            new Dictionary<string, Func<DomainJsonNode, TBuilder>>();

        /// <summary>
        ///     Creates a new factory instance.
        /// </summary>
        /// <param name="builder">The domain builder instance</param>
        /// <param name="provider">The JSON provider for parsing</param>
        protected DomainJsonFactory(TBuilder builder, IDomainJsonProvider provider)
        {
            Builder = builder ?? throw new ArgumentNullException(nameof(builder));
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            RegisterAttributedMethods();
        }

        /// <summary>
        ///     Creates a new factory instance with a builder factory function.
        /// </summary>
        /// <param name="builderFactory">Function that creates a new builder instance</param>
        /// <param name="provider">The JSON provider for parsing</param>
        protected DomainJsonFactory(Func<TBuilder> builderFactory, IDomainJsonProvider provider)
        {
            builderFactory = builderFactory ?? throw new ArgumentNullException(nameof(builderFactory));
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Builder = builderFactory();
            RegisterAttributedMethods();
        }

        /// <summary>
        ///     Registers (or overrides) a handler for a JSON "type" name.
        ///     Call this from <see cref="OnConfigure"/> to extend the dispatch table with handlers
        ///     that aren't expressible as simple attributed builder methods, or to override one.
        /// </summary>
        /// <param name="type">The JSON dispatch key (the "type" value in the JSON)</param>
        /// <param name="handler">Invoked with the JSON node; must call a builder method and return the builder</param>
        public void Register(string type, Func<DomainJsonNode, TBuilder> handler)
        {
            if (string.IsNullOrEmpty(type))
                throw new ArgumentException("Dispatch type cannot be null or empty", nameof(type));
            _dispatch[type] = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        ///     Optional hook for users to configure the factory (e.g. call <see cref="Register"/>).
        ///     Called once at the start of <see cref="Build"/>, before parsing.
        /// </summary>
        protected virtual void OnConfigure()
        {
            // Can be overridden in derived classes
        }

        /// <summary>
        ///     Loads and builds a domain from a JSON string.
        /// </summary>
        /// <param name="json">The JSON domain definition</param>
        /// <returns>The constructed Domain instance</returns>
        public Domain<TContext> Build(string json)
        {
            OnConfigure();

            var rootNode = Provider.Parse(json);

            // Process the root task
            if (rootNode.GetChild("root") is { } rootChild)
            {
                ProcessNode(rootChild);
            }

            return Builder.Build();
        }

        /// <summary>
        ///     Loads and builds a domain from a file.
        /// </summary>
        /// <param name="filePath">Path to the JSON file</param>
        /// <returns>The constructed Domain instance</returns>
        public Domain<TContext> BuildFromFile(string filePath)
        {
            var json = System.IO.File.ReadAllText(filePath);
            return Build(json);
        }

        /// <summary>
        ///     Recursively processes a JSON node, dispatching to builder methods
        ///     and handling conditions, operators, effects, and subtasks.
        /// </summary>
        private void ProcessNode(DomainJsonNode node)
        {
            if (node == null)
                return;

            var nodeType = node.Type;

            if (string.IsNullOrEmpty(nodeType))
            {
                throw new InvalidOperationException("JSON node is missing required 'type' field");
            }

            if (!_dispatch.TryGetValue(nodeType, out var handler))
            {
                throw new InvalidOperationException($"Unknown domain node type: {nodeType}");
            }

            // Track pointer before calling the handler. Some methods are self-contained
            // (they open AND close their own scope internally). If the pointer hasn't
            // changed after the handler returns, the scope is already balanced and we
            // must not add another End().
            var pointerBefore = Builder.Pointer;

            // Call the handler — opens the task scope for non-self-contained methods
            handler(node);

            // If the pointer changed, the handler pushed a new task onto the stack.
            // We are now responsible for applying its JSON-defined children and closing it.
            if (!ReferenceEquals(Builder.Pointer, pointerBefore))
            {
                // Process conditions (preconditions on the task just opened)
                foreach (var condition in node.Conditions)
                {
                    ProcessNode(condition);
                }

                // Process the operator (primitive task operator)
                if (node.Operator is var op)
                {
                    ProcessNode(op);
                }

                // Process effects
                foreach (var effect in node.Effects)
                {
                    ProcessNode(effect);
                }

                // Process subtasks (compound task children)
                foreach (var subtask in node.Subtasks)
                {
                    ProcessNode(subtask);
                }

                // Close the scope that was opened by the handler
                Builder.End();
            }
        }

        // ========================================================= REFLECTION

        /// <summary>
        ///     Scans <typeparamref name="TBuilder"/> for methods marked with
        ///     <see cref="JsonDomainMethodAttribute"/> and registers a handler for each.
        /// </summary>
        private void RegisterAttributedMethods()
        {
            var methods = typeof(TBuilder).GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<JsonDomainMethodAttribute>();
                if (attr == null)
                    continue;

                // Capture in a local so each closure binds its own MethodInfo.
                var captured = method;
                _dispatch[attr.Name] = node => (TBuilder) captured.Invoke(Builder, ExtractArguments(captured, node));
            }
        }

        /// <summary>
        ///     Builds the argument array for an attributed builder method by pulling each parameter
        ///     from the JSON node using the parameter's camelCased name as the JSON key.
        /// </summary>
        private static object[] ExtractArguments(MethodInfo method, DomainJsonNode node)
        {
            var parameters = method.GetParameters();
            if (parameters.Length == 0)
                return Array.Empty<object>();

            var args = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                args[i] = ExtractArgument(parameters[i], node);
            }
            return args;
        }

        /// <summary>
        ///     Extracts a single parameter value from the node, falling back to the parameter's
        ///     compile-time default (if any) or the type default when the JSON key is absent.
        /// </summary>
        private static object ExtractArgument(ParameterInfo parameter, DomainJsonNode node)
        {
            var key = ToCamelCase(parameter.Name);
            var type = parameter.ParameterType;
            var raw = node.GetString(key);

            if (raw == null)
            {
                if (parameter.HasDefaultValue)
                    return parameter.DefaultValue;
                return type.IsValueType ? Activator.CreateInstance(type) : null;
            }

            if (type == typeof(string))
                return raw;

            if (type.IsEnum)
                return Enum.Parse(type, raw, ignoreCase: true);

            return Convert.ChangeType(raw, type, CultureInfo.InvariantCulture);
        }

        private static string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
                return name;
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}
