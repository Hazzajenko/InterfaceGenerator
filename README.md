# Interface Generator

A C# source generator that automatically creates interfaces from classes, making it easier to maintain class-interface pairs and reduce boilerplate code.

## Features

The Interface Generator automatically creates interfaces from classes with comprehensive support for:

- Properties (read-only, write-only, and read-write)
- Methods with various parameter types and return values
- Generic types and methods
- Generic constraints
- Nullable reference types
- Arrays (single-dimensional, jagged, and multi-dimensional)
- Nested types
- Tuple return types
- Asynchronous methods (Task and ValueTask)
- Parameter modifiers (ref, out, in)
- Optional parameters
- Full namespace qualification with `global::` prefix

## Usage

### Basic Usage

1. Add the namespace for the generator attributes:

```csharp
using InterfaceGenerator.Attributes;
```

2. Declare a partial interface that will be implemented:

```csharp
public partial interface IMyClass;
```

3. Add the `[GenerateInterface]` attribute to your class:

```csharp
[GenerateInterface]
public class MyClass : IMyClass
{
    public string Property { get; set; }
    public void Method() { }
}
```

The generator will automatically create the interface implementation with all public members.

### Ignoring Members

Use the `[GenerateIgnore]` attribute to exclude specific members from the generated interface:

```csharp
[GenerateInterface]
public class MyClass : IMyClass
{
    public string Property { get; set; }

    [GenerateIgnore]
    public void IgnoredMethod() { }
}
```

### Example

#### Before (Your Input Class)

```csharp
using InterfaceGenerator.Attributes;

namespace Example;

public partial interface IMyClass;

[GenerateInterface]
public class MyClass : IMyClass
{
    public string Name { get; set; }
    public int? Count { get; private set; }

    public List<T> GetItems<T>() where T : class, new()
    {
        return new List<T>();
    }

    public async Task<(string Name, int Value)> ProcessDataAsync(
        string input,
        Dictionary<string, object> parameters)
    {
        return ("test", 42);
    }

    [GenerateIgnore]
    public void InternalMethod()
    {
        // This won't appear in the interface
    }
}
```

#### After (Generated Interface)

```csharp
// <auto-generated/>
namespace Example;

#nullable enable
public partial interface IMyClass
{
    string Name { get; set; }
    int? Count { get; }

    global::System.Collections.Generic.List<T> GetItems<T>()
        where T : class, new();

    global::System.Threading.Tasks.Task<(string Name, int Value)> ProcessDataAsync(
        string input,
        global::System.Collections.Generic.Dictionary<string, object> parameters);
}
```

### Key Features Demonstrated

- Automatic interface generation with `[GenerateInterface]` attribute
- Property accessors preserved (get/set, get-only)
- Nullable reference types supported
- Generic methods with constraints
- Tuple return types
- Full qualification of system types with `global::`
- Members marked with `[GenerateIgnore]` are excluded

## Contributing

Contributions are welcome! Please feel free to submit pull requests.