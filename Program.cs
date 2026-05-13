using Microsoft.CSharp.RuntimeBinder;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

Environment.ExitCode = await RunAsync();

static async Task<int> RunAsync()
{
    const string scriptSource = """
        var collection = new List<dynamic> { new { Id = 1, Title = "Test 1" } };
        var myVal = collection.Select(x => GetValue(x)).First();
        return myVal;

        string GetValue(dynamic obj)
        {
            return obj.Title;
        }
        """;

    var options = ScriptOptions.Default
        .AddImports("System", "System.Collections.Generic", "System.Linq")
        .AddReferences(
            typeof(object).Assembly,
            typeof(List<>).Assembly,
            typeof(Enumerable).Assembly,
            typeof(Binder).Assembly);

    var script = CSharpScript.Create<string>(scriptSource, options);
    var diagnostics = script.Compile();
    var errors = diagnostics.Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToArray();

    Console.WriteLine("Roslyn runtime repro");
    Console.WriteLine("Pinned .NET SDK: 10.0.203");
    Console.WriteLine("Pinned package: Microsoft.CodeAnalysis.CSharp.Scripting 5.3.0");
    Console.WriteLine();

    if (errors.Length > 0)
    {
        Console.WriteLine("Compile step: FAILED");

        foreach (var error in errors)
        {
            Console.WriteLine(error.ToString());
        }

        return 1;
    }

    Console.WriteLine("Compile step: SUCCEEDED");
    Console.WriteLine("Execution step: STARTED");

    try
    {
        var state = await script.RunAsync();
        Console.WriteLine("Execution step: SUCCEEDED");
        Console.WriteLine($"Unexpected result: {state.ReturnValue}");
        return 2;
    }
    catch (RuntimeBinderException ex)
    {
        Console.WriteLine("Execution step: FAILED AT RUNTIME");
        Console.WriteLine($"Exception type: {ex.GetType().FullName}");
        Console.WriteLine($"Exception message: {ex.Message}");
        Console.WriteLine($"Contains 'An object reference is required': {ex.Message.Contains("An object reference is required", StringComparison.Ordinal)}");
        Console.WriteLine($"Contains 'Submission#0.GetValue(object)': {ex.Message.Contains("Submission#0.GetValue(object)", StringComparison.Ordinal)}");
        Console.WriteLine("Full exception: " + ex);
        Console.WriteLine("Observed expected Roslyn script-submission failure.");
        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine("Execution step: FAILED AT RUNTIME");
        Console.WriteLine($"Unexpected exception type: {ex.GetType().FullName}");
        Console.WriteLine($"Unexpected exception message: {ex.Message}");
        return 3;
    }
}
