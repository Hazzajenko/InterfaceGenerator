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

There are two ways to use the Interface Generator:

#### 1. Class-First Approach

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

#### 2. Interface-First Approach

Alternatively, you can specify the target class directly on the interface:

```csharp
[GenerateInterface(typeof(MyClass))]
public partial interface IMyClass;
```

This approach is useful when you want to:

- Generate multiple interfaces from the same class
- Keep the class implementation clean of generator attributes
- Follow an interface-first design approach

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

#### Before (Your Input)

```csharp
using InterfaceGenerator.Attributes;

namespace Example;

// You can use either approach:
public partial interface IMyClass;                         // Class-first approach
// [GenerateInterface(typeof(MyClass))]                   // Interface-first approach
// public partial interface IMyClass;

[GenerateInterface]  // Only needed for class-first approach
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

- Two approaches to interface generation:
  - Class-first with `[GenerateInterface]` attribute on the class
  - Interface-first with `[GenerateInterface(typeof(TargetClass))]` on the interface
- Property accessors preserved (get/set, get-only)
- Nullable reference types supported
- Generic methods with constraints
- Tuple return types
- Full qualification of system types with `global::`
- Members marked with `[GenerateIgnore]` are excluded

## TODO

- Create and publish NuGet package
  - Add installation instructions to README
- Complete unit tests for source generator
  - Test different class/interface scenarios
  - Test edge cases and error conditions
  - Add test coverage reporting

## Contributing

Contributions are welcome! Please feel free to submit pull requests.