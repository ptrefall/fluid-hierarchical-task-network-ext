using FluidHTN;
using FluidHTN.Examples.DomainJsonTest;
using FluidHTN.Json;

var app = new DomainJsonTestApp();
app.Run();
app.RunManualRegistrationDemo();

public class DomainJsonTestApp
{
    public void Run()
    {
        Console.WriteLine("=== Fluid HTN JSON Domain Test ===\n");

        // Create a context with initial state
        var context = new MyContext();
        context.Init();
        Console.WriteLine($"Context initialized:");
        Console.WriteLine($"  Hungry: {context.Hungry}");
        Console.WriteLine($"  Tired: {context.Tired}\n");

        // Load the domain from JSON
        try
        {
            // Determine the domain.json path
            var jsonPath = Path.Combine(AppContext.BaseDirectory, "domain.json");
            if (!File.Exists(jsonPath))
            {
                // Try relative path if running from project directory
                jsonPath = "domain.json";
            }

            Console.WriteLine($"Loading domain from: {jsonPath}");
            if (!File.Exists(jsonPath))
            {
                Console.WriteLine($"ERROR: domain.json not found at {jsonPath}");
                return;
            }

            // Create the factory and build the domain
            var builder = new MyDomainBuilder("TestDomain");
            var factory = new MyDomainBuilderJsonFactory(builder);
            Domain<MyContext> domain = factory.BuildFromFile(jsonPath);

            Console.WriteLine($"Domain '{domain.Root.Name}' loaded successfully!");
            Console.WriteLine($"Root task has {domain.Root.Subtasks.Count} direct subtasks:\n");

            // Print the domain structure
            PrintTaskTree(domain.Root, 0);
            Console.WriteLine();

            // Create a planner
            var planner = new Planner<MyContext>();

            // Capture the COMPLETE plan the moment it's found. We do this via the OnNewPlan hook
            // because Tick() immediately pulls the first task into CurrentTask and executes its
            // operator — so by the time Tick() returns, that first task is no longer in the queue.
            var fullPlan = new List<string>();
            context.PlannerState.OnNewPlan = plan =>
            {
                foreach (var task in plan)
                {
                    fullPlan.Add(task.Name);
                }
            };

            // Tick the planner to decompose the domain once
            Console.WriteLine("Ticking planner...");
            planner.Tick(domain, context);

            Console.WriteLine($"Planner result: {context.PlannerState.LastStatus}\n");

            // Display the full plan as decomposed (the Eat/Sleep conditions and effects below were
            // authored entirely in domain.json — see the "Survival (data-driven)" branch).
            Console.WriteLine("Planned tasks:");
            if (fullPlan.Count > 0)
            {
                for (var i = 0; i < fullPlan.Count; i++)
                {
                    Console.WriteLine($"  {i + 1}. {fullPlan[i]}");
                }
            }
            else
            {
                Console.WriteLine("  (empty)");
            }

            Console.WriteLine("\n✓ Test completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine($"Details: {ex}");
        }
    }

    /// <summary>
    ///     Demonstrates the two dispatch paths that domain.json doesn't exercise:
    ///     manually registering a custom "type" via factory.Register, and reflection-based
    ///     parameter extraction for a method with uint + enum parameters (Repeat).
    /// </summary>
    public void RunManualRegistrationDemo()
    {
        Console.WriteLine("\n=== Manual Registration + Parameterized Task Demo ===\n");

        // "Greet" is not an attributed builder method — we teach the factory about it at runtime.
        // "Repeat" IS attributed; here its worldStateIndex (uint) and repetitionType (enum) are
        // pulled straight from the JSON by the reflection dispatcher. Reading the wrong key would
        // make Enum.Parse throw on "Repeat", so a clean build is proof the extraction is correct.
        var json = @"{
            ""name"": ""manual-demo"",
            ""root"": {
                ""type"": ""Sequence"",
                ""name"": ""Root"",
                ""subtasks"": [
                    { ""type"": ""Greet"", ""name"": ""Say Hello"" },
                    {
                        ""type"": ""Repeat"",
                        ""name"": ""Idle Loop"",
                        ""worldStateIndex"": 0,
                        ""repetitionType"": ""Blockwise"",
                        ""subtasks"": [ { ""type"": ""Idle"" } ]
                    }
                ]
            }
        }";

        try
        {
            var builder = new MyDomainBuilder("ManualDemo");
            var factory = new MyDomainBuilderJsonFactory(builder);

            // Manual registration: a self-contained action for a type the builder never declared.
            factory.Register("Greet", node =>
                builder.Action(node.GetString("name", "Greet"))
                    .Do(ctx => FluidHTN.TaskStatus.Success)
                    .End());

            var domain = factory.Build(json);

            Console.WriteLine($"Domain '{domain.Root.Name}' built via manual + reflection dispatch:");
            PrintTaskTree(domain.Root, 0);
            Console.WriteLine("\n✓ Manual registration and parameterized (uint/enum) dispatch verified!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine($"Details: {ex}");
        }
    }

    private void PrintTaskTree(ITask task, int depth)
    {
        string indent = new string(' ', depth * 2);
        string taskType = task.GetType().Name;

        // Check if the task has a Subtasks property (indicates it's a compound task)
        var subtasksProperty = task.GetType().GetProperty("Subtasks");
        bool isCompound = subtasksProperty != null && subtasksProperty.CanRead;

        Console.WriteLine($"{indent}[{(isCompound ? "Compound" : "Primitive")}] {task.Name} ({taskType})");

        PrintConditionsAndEffects(task, indent);

        if (isCompound)
        {
            PrintSubtasks(task, subtasksProperty!, depth);
        }
    }

    // Conditions can sit on any task; effects only on primitive tasks. Printing them makes the
    // JSON-authored "conditions"/"effects" nodes visible in the decomposed domain.
    private void PrintConditionsAndEffects(ITask task, string indent)
    {
        foreach (var condition in task.Conditions)
        {
            Console.WriteLine($"{indent}  · condition: {condition.Name}");
        }

        if (task is FluidHTN.PrimitiveTasks.IPrimitiveTask primitive)
        {
            foreach (var effect in primitive.Effects)
            {
                Console.WriteLine($"{indent}  · effect: {effect.Name} [{effect.Type}]");
            }
        }
    }

    private void PrintSubtasks(ITask task, System.Reflection.PropertyInfo subtasksProperty, int depth)
    {
        if (!(subtasksProperty.GetValue(task) is System.Collections.IEnumerable subtasks))
        {
            return;
        }

        foreach (ITask subtask in subtasks)
        {
            PrintTaskTree(subtask, depth + 1);
        }
    }
}
