using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// using InterfaceGenerator.Attributes;

namespace InterfaceGenerator.Sample;

public partial interface ISampleInterface;

// [GenerateInterface]
public class SampleInterface : ISampleInterface
{
    // Basic property cases
    public string Property { get; set; }
    public int ReadOnlyProperty { get; }
    private double _writeOnlyProperty;

    public double WriteOnlyProperty
    {
        set => _writeOnlyProperty = value;
    }

    public List<string> GenericProperty { get; set; }
    public Dictionary<int, List<string>> ComplexGenericProperty { get; set; }
    public string? NullableProperty { get; set; }
    public int? NullableValueTypeProperty { get; set; }

    // Array properties
    public int[] ArrayProperty { get; set; }
    public string[][] JaggedArrayProperty { get; set; }
    public int[,] MultiDimensionalArrayProperty { get; set; }

    // Basic method cases
    public void Method()
    {
    }

    public async Task MethodAsync()
    {
        await Task.Delay(1);
    }

    public ValueTask<int> ValueTaskMethod()
    {
        return new ValueTask<int>(1);
    }

    // Parameter variations
    public void MethodWithParameters(string parameter1, int parameter2)
    {
    }

    public void MethodWithOptionalParameters(string required, int optional = 42)
    {
    }

    public void MethodWithParams(params string[] values)
    {
    }

    public void MethodWithRefParameters(ref int value, out string text, in double number)
    {
        text = "test";
    }

    // Return value variations
    public string MethodWithReturnValue()
    {
        return "";
    }

    public string MethodWithReturnValueAndParameters(string parameter1, int parameter2)
    {
        return "";
    }

    public (string Name, int Age) MethodWithTupleReturn()
    {
        return ("", 0);
    }

    public Task<List<string>> MethodWithComplexReturnType()
    {
        return Task.FromResult(new List<string>());
    }

    // Generic method variations
    public void MethodWithGeneric<T>()
    {
    }

    public void MethodWithGenericAndParameters<T>(T parameter1, int parameter2)
    {
    }

    public T MethodWithGenericAndReturnValue<T>()
    {
        return default(T)!;
    }

    public T MethodWithGenericAndReturnValueAndParameters<T>(T parameter1, int parameter2)
    {
        return parameter1;
    }

    // Multiple type parameters
    public void MethodWithGenericAndGeneric<T, TU>()
    {
    }

    public void MethodWithGenericAndGenericAndParameters<T, TU>(T parameter1, TU parameter2)
    {
    }

    public T MethodWithGenericAndGenericAndReturnValue<T, TU>()
    {
        return default(T)!;
    }

    public (T Item1, TU Item2) MethodWithMultipleGenericsAndTupleReturn<T, TU>()
    {
        return default((T Item1, TU Item2));
    }

    // Generic constraints
    public void MethodWithConstraint<T>() where T : class
    {
    }

    public void MethodWithMultipleConstraints<T>() where T : class, new()
    {
    }

    public void MethodWithStructConstraint<T>() where T : struct
    {
    }

    public void MethodWithInterfaceConstraint<T>() where T : IDisposable
    {
    }

    public void MethodWithBaseClassConstraint<T>() where T : BaseClass
    {
    }

    public void MethodWithCombinedConstraints<T, TU>()
        where T : class, IDisposable, new()
        where TU : struct
    {
    }

    // Nested type scenarios
    public NestedClass.InnerType MethodWithNestedType()
    {
        return default(NestedClass.InnerType)!;
    }

    public void MethodWithNestedGeneric(NestedClass.InnerGeneric<int> param)
    {
    }

    // Event pattern methods
    public event EventHandler? SomeEvent;
    public event EventHandler<CustomEventArgs>? GenericEvent;

    // Nullable reference type scenarios
    public void MethodWithNullableParameter(string? nullableParam)
    {
    }

    public string? MethodWithNullableReturn()
    {
        return null;
    }

    public void MethodWithNullableGeneric<T>(T? param) where T : class
    {
    }

    public T? MethodWithNullableGenericReturn<T>() where T : class
    {
        return null;
    }

    // Attribute usage
    [Obsolete]
    public void ObsoleteMethod()
    {
    }

    public void MethodWithAttributeParameter(
        [System.Diagnostics.CodeAnalysis.NotNull]
        string notNull,
        [System.Diagnostics.CodeAnalysis.MaybeNull]
        out string? maybeNull)
    {
        maybeNull = null!;
    }
}

public class BaseClass
{
}

public class CustomEventArgs : EventArgs
{
}

public class NestedClass
{
    public class InnerType
    {
    }

    public class InnerGeneric<T>
    {
    }
}