using Microsoft.Extensions.Options;

namespace Test.Helpers.Helpers;

public class TestOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T>
{
    public T Get(string? name)
    {
        return CurrentValue;
    }

    public IDisposable OnChange(Action<T, string> listener)
    {
        throw new NotImplementedException();
    }

    public T CurrentValue { get; } = currentValue;
} 
