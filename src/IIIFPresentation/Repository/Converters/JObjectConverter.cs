using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Repository.Converters;

/// <summary>
/// Conversion logic for JObject (on model) -> string (in db)
/// </summary>
public class JObjectConverter : ValueConverter<JObject, string>
{
    public JObjectConverter()
        : base(
            v => v.ToString(Formatting.None),
            v => JObject.Parse(v))
    {
    }
}

/// <summary>
/// Comparison logic for JObject values. Used by EF internals for determining when a field has changed
/// </summary>
public class JObjectComparer : ValueComparer<JObject>
{
    public JObjectComparer()
        : base(
            (o1, o2) => JToken.DeepEquals(o1, o2),
            o => o.ToString(Formatting.None).GetHashCode(),
            o => (JObject)o.DeepClone())
    {
    }
}
