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
    /// Get specified property value from jObject. Property is required so <see cref="InvalidOperationException"/>
    /// thrown if not found
    /// </summary>
    public static T? GetRequiredValue<T>(this JObject jObject, string property) 
        => jObject.GetRequiredValue(property).Value<T>();
}
