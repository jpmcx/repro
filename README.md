# Roslyn Script Runtime Repro

This is a standalone repro for a Roslyn script-submission bug where the script compiles successfully and then fails at runtime with `Microsoft.CSharp.RuntimeBinder.RuntimeBinderException`.

## Contents

- `Program.cs`: runs the minimal failing `CSharpScript` example
- `roslyn-runtime-repro.csproj`: pins `Microsoft.CodeAnalysis.CSharp.Scripting` to `5.3.0`
- `global.json`: pins the SDK to `10.0.203`
- `version-sources.md`: records the exact version baseline and primary sources
- `environment-summary.md`: summarizes the validation machine
- `run-output.txt`: saved output from the validated run

## Validation Commands

```powershell
dotnet restore
dotnet build -v minimal
dotnet run
```

Expected behavior for this repro package:

1. The script compile step succeeds.
2. The execution step fails at runtime.
3. The thrown exception is `Microsoft.CSharp.RuntimeBinder.RuntimeBinderException`.
4. The message contains `An object reference is required`.
5. The message references `Submission#0.GetValue(object)`.

The project does not reference any code from the surrounding repository.
