using Generators;

namespace InterfaceGenerator.Sample;

public partial interface ISampleInterface;

[GenerateInterface]
public class SampleInterface : ISampleInterface
{
    public string Property { get; set; }

    public void Method()
    {
    }

    public void MethodWithParameters(string parameter1, int parameter2)
    {
    }

    public void MethodWithReturnValue()
    {
    }

    public string MethodWithReturnValueAndParameters(string parameter1, int parameter2)
    {
        return string.Empty;
    }

    public void MethodWithGeneric<T>()
    {
    }

    public void MethodWithGenericAndParameters<T>(T parameter1, int parameter2)
    {
    }

    public T MethodWithGenericAndReturnValue<T>()
    {
        return default(T);
    }

    public T MethodWithGenericAndReturnValueAndParameters<T>(T parameter1, int parameter2)
    {
        return default(T);
    }

    public void MethodWithGenericAndGeneric<T, U>()
    {
    }

    public void MethodWithGenericAndGenericAndParameters<T, U>(T parameter1, U parameter2)
    {
    }

    public T MethodWithGenericAndGenericAndReturnValue<T, U>()
    {
        return default(T);
    }
}