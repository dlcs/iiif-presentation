using Newtonsoft.Json.Linq;

namespace Core.Helpers;

public static class JObjectX
{
    /// <summary>
    /// Get specified property value from jObject. Property is required so <see cref="InvalidOperationException"/>
    /// thrown if not found
    /// </summary>
    public static JToken GetRequiredValue(this JObject jObject, string property) 
        => !jObject.TryGetValue(property, out var val) 
            ? throw new InvalidOperationException($"Object missing '{property}' property") 
            : val;
    
    /// <summary>
    /// Get specified property value from jObject. Property is required so <see cref="FormatException"/>
    /// thrown if not in the specified type
    /// </summary>
    public static T GetRequiredValue<T>(this JObject jObject, string property) 
        => jObject.GetRequiredValue(property).Value<T>().ThrowIfNull(nameof(jObject));

    /// <summary>
    /// Try and get specified property value from jObject. This will throw a <see cref="FormatException"/> if the value
    /// is not of the specified type or null
    /// </summary>
    public static T? TryGetValue<T>(this JObject jObject, string property)
    {
        jObject.TryGetValue(property, out var value);

        return value == null ? default : value.Value<T>();
    }
}
