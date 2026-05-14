# Dynamic Local Function Behavior In Roslyn Scripts

## Summary

Some `dynamic` + lambda + local-function script shapes fail
Some very similar shapes succeed
The failure depends on the exact lowered form Roslyn produces

#### 1. Intermediate local from forwarding lambda to non-static local function

```csharp
var collection = new List<dynamic> { new { Id = 1, Title = "Test 1" } };
var myVal = collection.Select(x => GetValue(x)).First();
return myVal;

string GetValue(dynamic obj)
{
    return obj.Title;
}
```

Observed result:

- runtime failure
- `Microsoft.CSharp.RuntimeBinder.RuntimeBinderException`
- message includes:
  `An object reference is required for the non-static field, method, or property 'Submission#0.GetValue(object)'`

#### 2. Intermediate local from query syntax lowering

```csharp
var collection = new List<dynamic> { new { Id = 1, Title = "Test 1" } };
var myVal =
    (from x in collection
     select GetValue(x))
    .First();
return myVal;

string GetValue(dynamic obj)
{
    return obj.Title;
}
```

Observed result:

- runtime failure
- same `RuntimeBinderException` pattern as above

#### 3. Delegate assignment using a forwarding lambda

```csharp
var collection = new List<dynamic> { new { Id = 1, Title = "Test 1" } };
Func<dynamic, string> getValue = x => GetValue(x);
return getValue(collection.First());

string GetValue(dynamic obj)
{
    return obj.Title;
}
```

Observed result:

- runtime failure
- same `RuntimeBinderException` pattern as above

#### 4. Intermediate local from forwarding lambda to capturing local function

```csharp
var collection = new List<dynamic> { new { Id = 1, Title = "Test 1" } };
var suffix = "!";
var myVal = collection.Select(x => GetValue(x)).First();
return myVal;

string GetValue(dynamic obj)
{
    return obj.Title + suffix;
}
```

Observed result:

- runtime failure
- same `RuntimeBinderException` pattern as above

### Verified successes

These were observed to succeed in this repo.

#### 1. Direct return from forwarding lambda to non-static local function

```csharp
var collection = new List<dynamic> { new { Id = 1, Title = "Test 1" } };
return collection.Select(x => GetValue(x)).First();

string GetValue(dynamic obj)
{
    return obj.Title;
}
```

Observed result:

- success
- returns `"Test 1"`

This is the most important correction to the earlier document. The forwarding lambda alone is not sufficient to trigger the bug.

#### 2. Direct return from query syntax lowering

```csharp
var collection = new List<dynamic> { new { Id = 1, Title = "Test 1" } };
return
    (from x in collection
     select GetValue(x))
    .First();

string GetValue(dynamic obj)
{
    return obj.Title;
}
```

Observed result:

- success
- returns `"Test 1"`

#### 3. Direct return from `Where` predicate using forwarding lambda

```csharp
var collection = new List<dynamic>
{
    new { Id = 1, IsActive = true },
    new { Id = 2, IsActive = false },
};

return collection.Where(x => Matches(x)).Count();

bool Matches(dynamic obj)
{
    return obj.IsActive;
}
```

Observed result:

- success
- returns `1`

#### 4. Method group to non-static local function

```csharp
var collection = new List<dynamic> { new { Id = 1, Title = "Test 1" } };
return collection.Select(GetValue).First();

string GetValue(dynamic obj)
{
    return obj.Title;
}
```

Observed result:

- success
- returns `"Test 1"`

#### 5. Forwarding lambda to static local function

```csharp
var collection = new List<dynamic> { new { Id = 1, Title = "Test 1" } };
return collection.Select(x => GetValue(x)).First();

static string GetValue(dynamic obj)
{
    return obj.Title;
}
```

Observed result:

- success
- returns `"Test 1"`

#### 6. Inline dynamic member access

```csharp
var collection = new List<dynamic> { new { Id = 1, Title = "Test 1" } };
return collection.Select(x => (string)x.Title).First();
```

Observed result:

- success
- returns `"Test 1"`

#### 7. Delegate assignment using method group

```csharp
var collection = new List<dynamic> { new { Id = 1, Title = "Test 1" } };
Func<dynamic, string> getValue = GetValue;
return getValue(collection.First());

string GetValue(dynamic obj)
{
    return obj.Title;
}
```

Observed result:

- success
- returns `"Test 1"`

#### 8. Static local function with explicit state parameter

```csharp
var collection = new List<dynamic> { new { Id = 1, Title = "Test 1" } };
var suffix = "!";
return collection.Select(x => GetValue(x, suffix)).First();

static string GetValue(dynamic obj, string suffix)
{
    return obj.Title + suffix;
}
```

Observed result:

- success
- returns `"Test 1!"`

#### 9. Named type with forwarding lambda

```csharp
public class Item
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
}

var collection = new List<Item> { new Item { Id = 1, Title = "Test 1" } };
return collection.Select(x => GetValue(x)).First();

string GetValue(Item obj)
{
    return obj.Title;
}
```

Observed result:

- success
- returns `"Test 1"`

## Practical Guidance Based On Observed Failing Cases

These shapes should be treated as risky until proven otherwise:

### 1. Storing the result of `collection.Select(x => LocalFn(x)).First()` in an intermediate variable before returning it

This is the clearest verified trigger.

### 2. Using query syntax when it lowers to the same "intermediate variable then return" shape

The query syntax itself is not the issue. The verified failing form includes the extra intermediate local.

### 3. Assigning a forwarding lambda to a delegate

```csharp
Func<dynamic, string> f = x => LocalFn(x);
```

This is a verified failing shape.




# IMPORTANT - Speculative part - tested, but with heavy codex help
Take the following with a large grain of salt - it is based on actual compilation, but the explanations were produced by codex and I would not take any of it to be true, unless you read the actual Roslyn source code yourself

## What Roslyn Actually Emits

The validation is possible by compiling the script with Roslyn and inspecting the emitted assembly in `samples/RoslynScriptInspector/`.

Two facts are stable across the inspected variants:

1. The script local function is emitted as an **instance method** on the generated script submission type:

```csharp
string GetValue(object obj)
```

In the emitted assembly this is:

```csharp
TYPE Submission#0
  METHOD System.String GetValue(System.Object obj)
```

2. The failure depends on what receiver Roslyn gives to the **dynamic call site inside the forwarding lambda**.

### Failing shape: static-type receiver

For the failing example above, the forwarding lambda is emitted on a cached `<>c` helper:

```csharp
TYPE Submission#0+<>c
  FIELD static Submission#0+<>c <>9
  FIELD static System.Func<object, object> <>9__0_0
  METHOD System.Object <<Initialize>>b__0_0(System.Object x)
```

The key emitted IL is:

```il
IL_0000: ldsfld ... CallSite<Func<CallSite, System.Type, System.Object, System.Object>> <>p__0
...
IL_0022: ldc.i4.s 33
...
IL_0035: call ... CallSiteBinder InvokeMember(...)
...
IL_0053: ldtoken Submission#0
IL_0058: call System.Type GetTypeFromHandle(...)
IL_005d: ldarg.1
IL_005e: callvirt System.Object Invoke(CallSite, System.Type, System.Object)
```

Equivalent lowered shape:

```csharp
private sealed class <>c
{
    public static readonly <>c <>9 = new();
    public static Func<object, object>? <>9__0_0;

    internal object <<Initialize>>b__0_0(object x)
    {
        return DynamicInvokeMember(
            receiver: typeof(Submission#0),
            argument: x,
            memberName: "GetValue",
            receiverFlags: UseCompileTimeType | IsStaticType);
    }
}
```

That is the bug in concrete form.

Roslyn has emitted the dynamic call as though the source had named the **type** `Submission#0` for a static call, not the **current submission instance** for an instance call.

### Passing shape: instance receiver

For the closely related passing variant:

```csharp
var collection = new List<dynamic> { new { Id = 1, Title = "Test 1" } };
return collection.Select(x => GetValue(x)).First();

string GetValue(dynamic obj)
{
    return obj.Title;
}
```

Roslyn emits the forwarding lambda as an **instance method on `Submission#0`**:

```csharp
TYPE Submission#0
  METHOD System.Object <<Initialize>>b__0_0(System.Object x)
```

The key emitted IL is:

```il
IL_0000: ldsfld ... CallSite<Func<CallSite, Submission#0, System.Object, System.Object>> <>p__0
...
IL_0022: ldc.i4.1
...
IL_004d: ldsfld ... <>p__0
IL_0052: ldarg.0
IL_0053: ldarg.1
IL_0054: callvirt System.Object Invoke(CallSite, Submission#0, System.Object)
```

Equivalent lowered shape:

```csharp
internal object <<Initialize>>b__0_0(object x)
{
    return DynamicInvokeMember(
        receiver: this,
        argument: x,
        memberName: "GetValue",
        receiverFlags: UseCompileTimeType);
}
```

That version works because the runtime binder receives an actual `Submission#0` instance.

## Why The Runtime Binder Fails

This is the runtime chain:

1. The local function `GetValue` exists only as an **instance member** on `Submission#0`.
2. In the failing shape, Roslyn emits the dynamic call site with receiver type `System.Type`, not `Submission#0`.
3. The receiver argument is marked with `UseCompileTimeType | IsStaticType` (`33`).
4. `IsStaticType` means the receiver is a `Type` that represents a type name used for a static call.
5. The runtime binder therefore tries to bind `GetValue` as a static member access on `Submission#0`.
6. But `Submission#0.GetValue(object)` is not static.
7. The binder throws:
   `An object reference is required for the non-static field, method, or property 'Submission#0.GetValue(object)'`

The important point is that the failure is not caused by `dynamic` member access inside `GetValue`.

That part works fine.

The failure happens **before** the body of `GetValue` matters. The call site is malformed because it targets the type instead of the submission instance.

## Why This Is A Roslyn Emission Bug

Roslyn's dynamic invocation binding path starts from the method-group receiver it has available and carries that receiver into the dynamic call shape.

In `Binder_Invocation.cs`, `BindDynamicInvocation(...)` reads the method-group receiver first:

- `receiver = methodGroup.ReceiverOpt;`

Source:

- https://github.com/dotnet/roslyn/blob/main/src/Compilers/CSharp/Portable/Binder/Binder_Invocation.cs

The relevant section is around lines 2915-2978 in the current `main` branch.

The runtime binder flag Roslyn emits is also significant:

- `CSharpArgumentInfoFlags.IsStaticType` means:
  "The argument is a `Type` indicating an actual type name used in source. Used only for target objects in static calls."

Source:

- https://learn.microsoft.com/en-us/dotnet/api/microsoft.csharp.runtimebinder.csharpargumentinfoflags?view=net-9.0

So the emitted failing call site is not ambiguous:

- receiver runtime type: `System.Type`
- receiver flags: `UseCompileTimeType | IsStaticType`
- target member: `"GetValue"`

That is a static-call shape.

But the target method Roslyn emitted is:

```csharp
Submission#0.GetValue(object)
```

and that method is instance-only.

That mismatch is exactly why the runtime failure occurs.

## Why The Example Fails But The Direct Return Variant Succeeds

The two variants compile to different lambda shapes:

- failing example:
  the forwarding lambda is emitted on `Submission#0+<>c`, and the dynamic receiver is `typeof(Submission#0)`
- passing direct-return example:
  the forwarding lambda is emitted on `Submission#0` itself, and the dynamic receiver is `this`

That difference is enough to explain the observed behavior:

- cached helper lambda on `<>c`:
  no submission instance is passed to the dynamic call site
- instance lambda on `Submission#0`:
  the submission instance is passed to the dynamic call site

The extra intermediate-local shape is therefore not just stylistic noise. In this script environment it is a trigger for different emission.












### 4. Combining the intermediate-local shape with a capturing local function

Also verified to fail.

