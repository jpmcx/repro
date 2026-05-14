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

### 4. Combining the intermediate-local shape with a capturing local function

Also verified to fail.

