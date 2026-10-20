using System.Reflection;

namespace API.Converters;

/// <summary>
/// Copies the public read/write properties declared on <typeparamref name="T"/> (including inherited ones) from one
/// object to another. Properties of any derived type are ignored, so a derived instance can be the source or target
/// without its extra properties being read or overwritten.
/// </summary>
internal static class IIIFPropertyCopier
{
    public static void CopyProperties<T>(T source, T target) where T : class
    {
        foreach (var property in PropertyCache<T>.Properties)
        {
            property.SetValue(target, property.GetValue(source));
        }
    }

    private static class PropertyCache<T>
    {
        public static readonly PropertyInfo[] Properties = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
            .ToArray();
    }
}
